using System.Collections;
using BepInEx.Unity.IL2CPP.Utils;
using BetterAmongUs.Data;
using BetterAmongUs.Data.Config;
using BetterAmongUs.Generated;
using BetterAmongUs.Managers;
using BetterAmongUs.Modules;
using BetterAmongUs.Modules.AntiCheat;
using BetterAmongUs.Modules.Support;
using BetterAmongUs.MonoScripts.Extended;
using BetterAmongUs.Patches.Gameplay.UI;
using BetterAmongUs.Patches.Gameplay.UI.Settings;
using BetterAmongUs.Utilities;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace BetterAmongUs.Patches.Gameplay.Player;

[HarmonyPatch]
internal static class PlayerJoinAndLeftPatch
{
    // Players already warned about in this lobby (by client id), so nobody is warned twice.
    private static readonly HashSet<int> _lobbyWarnedClients = [];

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
    [HarmonyPostfix]
    private static void AmongUsClient_OnGameJoined_Postfix(AmongUsClient __instance)
    {
        // Fix host icon color display on modded servers
        if (!GameState.IsVanillaServer)
        {
            var host = AmongUsClient.Instance.GetHost().Character;
            host?.SetColor(-2);
            host?.SetColor(host.CurrentOutfit.ColorId);
        }

        // Client ids and player ids are reused in every lobby.
        _lobbyWarnedClients.Clear();
        PlayerControlUtils.ResetKickSafetyLog();
        BetterAntiCheat.ResetUnknownRpcNotices();

        // Also check the players who were already in the lobby when we joined.
        __instance.StartCoroutine(CoCheckPlayersAlreadyInLobby());

        BAUPlugin.Logger.Log($"Successfully joined {GameCode.IntToGameName(AmongUsClient.Instance.GameId)}", "OnGameJoinedPatch");
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerJoined))]
    [HarmonyPostfix]
    private static void AmongUsClient_OnPlayerJoined_Postfix(AmongUsClient __instance, ClientData data)
    {
        if (GameState.IsHost)
        {
            KickCooldownManager.Trigger();
        }

        // Host AND non-host: everyone gets the known-cheater / ban-list warning.
        __instance.StartCoroutine(CoCheckPlayerOnJoin(data));
    }

    private static IEnumerator CoCheckPlayersAlreadyInLobby()
    {
        yield return new WaitForSeconds(3f);

        foreach (var player in BAUPlugin.AllPlayerControls.ToArray())
        {
            if (player == null || player.Data == null || player.Data.IsIncomplete)
                continue;

            CheckJoinedPlayer(player);
        }
    }

    private static IEnumerator CoCheckPlayerOnJoin(ClientData data)
    {
        // Wait until the player's data is synced (give up after 30s, e.g. if they left again).
        float waited = 0f;
        while (data == null || !data.InScene || data.Character == null || data.Character.Data == null || data.Character.Data.IsIncomplete)
        {
            waited += Time.deltaTime;
            if (waited > 30f)
                yield break;

            yield return null;
        }

        if (GameState.IsInGame)
        {
            CheckJoinedPlayer(data.Character);
        }
    }

    /// <summary>
    /// Warns (sticky, CTRL+Y) when a player in the saved cheat data or on your ban lists joins.
    /// Nobody is kicked automatically (host safety): use /kick if you want them out.
    /// </summary>
    private static void CheckJoinedPlayer(PlayerControl player)
    {
        try
        {
            if (player == null || player.Data == null || player.IsLocalPlayer())
                return;

            if (!BAUConfigs.AntiCheat.Value)
                return;

            if (!_lobbyWarnedClients.Add(player.GetClientId()))
                return;

            string name = player.ExtendedData()?.RealName ?? player.Data.PlayerName ?? "(unknown)";
            string friendCode = player.Data.FriendCode ?? "";
            string hashPuid = player.GetHashPuid() ?? "";

            // Known cheater (saved by an earlier detection, in any lobby).
            var (isKnown, reason) = BetterDataManager.Files.BetterDataFile.CheckPlayerDataWithReason(player.Data);
            if (isKnown)
            {
                BetterNotificationManager.NotifyKnownCheaterInLobby(name, friendCode, hashPuid, reason);
                BAUPlugin.Logger.LogCheat($"Known cheater joined: {name} - {friendCode} - {hashPuid} ({reason})");
            }

            // Your own ban lists.
            if (BetterGameSettings.UseBanPlayerList.GetBool()
                && TextFileHandler.CompareStringMatch(BetterDataManager.Files.banPlayerListFilePath, [friendCode, hashPuid]))
            {
                BetterNotificationManager.NotifyLobbyWarning($"banplayer:{friendCode}:{hashPuid}",
                    $"<color=#ffae00><b>Player on your ban list joined</b></color>\nName: <color=#0097b5>{name}</color>\nFriend code: <color=#cccccc>{friendCode}</color>\n<size=80%>Use /kick if you want them out.</size>");
                BAUPlugin.Logger.LogCheat($"Ban list (player) match joined: {name} - {friendCode}");
            }

            if (BetterGameSettings.UseBanNameList.GetBool()
                && TextFileHandler.CompareStringRegexMatches(BetterDataManager.Files.banNameListFilePath, player.Data.PlayerName))
            {
                BetterNotificationManager.NotifyLobbyWarning($"banname:{friendCode}:{player.Data.PlayerName}",
                    $"<color=#ffae00><b>Name on your ban name list joined</b></color>\nName: <color=#0097b5>{name}</color>\nFriend code: <color=#cccccc>{friendCode}</color>\n<size=80%>Use /kick if you want them out.</size>");
                BAUPlugin.Logger.LogCheat($"Ban list (name) match joined: {name} - {friendCode}");
            }
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, "OnPlayerJoinedPatch");
        }
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerLeft))]
    [HarmonyPostfix]
    private static void AmongUsClient_OnPlayerLeft_Postfix(ClientData data, DisconnectReasons reason)
    {
        KickCooldownManager.Trigger();
        
        // Reclaim favorite color when player leaves in lobby
        if (GameState.IsLobby)
        {
            if (!BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_FavoriteColor))
            {
                var favColorId = (byte)BAUConfigs.FavoriteColor.Value;
                if (BAUConfigs.FavoriteColor.Value >= 0)
                {
                    if (PlayerControl.LocalPlayer.cosmetics.ColorId != favColorId && data.ColorId == favColorId)
                    {
                        PlayerControl.LocalPlayer.CmdCheckColor(favColorId);
                    }
                }
            }
        }

        // Update host icon in meeting
        MeetingHudPatch.UpdateHostIcon();
    }

    [HarmonyPatch(typeof(GameData))]
    [HarmonyPatch(nameof(GameData.HandleDisconnect))]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPatch([typeof(PlayerControl), typeof(DisconnectReasons)])]
    [HarmonyPrefix]
    private static void GameData_HandleDisconnect_Prefix(PlayerControl player, DisconnectReasons reason)
    {
        // Store disconnect reason in player's BetterData
        if (player.ExtendedData() != null)
        {
            player.ExtendedData().DisconnectReason = reason;
        }

        // Show custom disconnect notification
        BetterShowNotification(player.Data, reason);
    }

    [HarmonyPatch(typeof(GameData), nameof(GameData.ShowNotification))]
    [HarmonyPrefix]
    internal static bool GameData_ShowNotification_Prefix()
    {
        // Disable vanilla disconnect notifications (use BAU's instead)
        return false;
    }

    internal static void BetterShowNotification(NetworkedPlayerInfo playerData, DisconnectReasons reason = DisconnectReasons.Unknown, string forceReasonText = "")
    {
        if (playerData == null)
            return;

        // Prevent showing duplicate notifications
        if (playerData.ExtendedData().AntiCheatInfo.BannedByAntiCheat || playerData.ExtendedData().HasShowDcMsg)
            return;

        playerData.ExtendedData().HasShowDcMsg = true;

        string? playerName = playerData.ExtendedData().RealName;

        // Use custom reason text if provided
        if (forceReasonText != "")
        {
            var ReasonText = $"<color=#ff0>{playerData.ExtendedData().RealName}</color> {forceReasonText}";

            BAUPlugin.Logger.Log(ReasonText);

            HudManager.Instance.Notifier.AddDisconnectMessage(ReasonText);
        }
        else
        {
            string ReasonText;

            // Format disconnect message based on reason type
            switch (reason)
            {
                case DisconnectReasons.ExitGame:
                    ReasonText = TranslationStrings.DisconnectReason_Left.Format(playerName);
                    break;
                case DisconnectReasons.ClientTimeout:
                    ReasonText = TranslationStrings.DisconnectReason_Disconnect.Format(playerName);
                    break;
                case DisconnectReasons.Kicked:
                    ReasonText = TranslationStrings.DisconnectReason_Kicked.Format(playerName, AmongUsClient.Instance.GetHost().Character.Data.PlayerName);
                    break;
                case DisconnectReasons.Banned:
                    ReasonText = TranslationStrings.DisconnectReason_Banned.Format(playerName, AmongUsClient.Instance.GetHost().Character.Data.PlayerName);
                    break;
                case DisconnectReasons.Hacking:
                    ReasonText = TranslationStrings.DisconnectReason_Cheater.Format(playerName);
                    break;
                case DisconnectReasons.Error:
                    ReasonText = TranslationStrings.DisconnectReason_Error.Format(playerName);
                    break;
                case DisconnectReasons.Unknown:
                    ReasonText = TranslationStrings.DisconnectReason_Unknown.Format(playerName);
                    break;
                default:
                    ReasonText = TranslationStrings.DisconnectReason_Left.Format(playerName);
                    break;
            }

            BAUPlugin.Logger.Log(ReasonText);

            // Add formatted disconnect message to game UI
            HudManager.Instance.Notifier.AddDisconnectMessage(ReasonText);
        }
    }
}