using BetterAmongUs.Data;
using BetterAmongUs.Helpers;
using BetterAmongUs.Managers;
using BetterAmongUs.Modules;
using BetterAmongUs.Mono;
using BetterAmongUs.Patches.Gameplay.UI;
using BetterAmongUs.Patches.Gameplay.UI.Settings;
using HarmonyLib;
using InnerNet;

namespace BetterAmongUs.Patches.Gameplay.Player;

[HarmonyPatch]
internal static class PlayerJoinAndLeftPatch
{
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
    [HarmonyPostfix]
    private static void AmongUsClient_OnGameJoined_Postfix()
    {
        // Fix host icon color display on modded servers
        if (!GameState.IsVanillaServer)
        {
            var host = AmongUsClient.Instance.GetHost().Character;
            host?.SetColor(-2);
            host?.SetColor(host.CurrentOutfit.ColorId);
        }

        Logger_.Log($"Successfully joined {GameCode.IntToGameName(AmongUsClient.Instance.GameId)}", "OnGameJoinedPatch");
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerJoined))]
    [HarmonyPostfix]
    private static void AmongUsClient_OnPlayerJoined_Postfix(ClientData data)
    {
        // Schedule join-time checks 2.5s after the player joins so their data is fully synced.
        LateTask.Schedule(() =>
        {
            var player = Utils.PlayerFromClientId(data.Id);
            if (player == null || player.Data == null) return;

            // Known-cheater lobby warning: if the joining player matches any
            // entry in BAU's persistent cheat data (CheatData / SickoData /
            // AUMData / KNData), surface a sticky popup with their identity
            // and the stored reason. The user dismisses it with CTRL+Y.
            // Fires regardless of host-status so non-host BAU users also get
            // warned when they recognize someone from a past lobby.
            try
            {
                var match = BetterDataManager.BetterDataFile.CheckPlayerDataWithReason(player.Data);
                if (match.check)
                {
                    BetterNotificationManager.NotifyKnownCheaterInLobby(
                        player.BetterData()?.RealName ?? player.Data.PlayerName ?? "(unknown)",
                        player.Data.FriendCode ?? "",
                        player.GetHashPuid() ?? "",
                        match.reason ?? "");
                }
            }
            catch (Exception ex) { Logger_.Error(ex, "OnPlayerJoinedPatch.KnownCheaterCheck"); }

            // Old BanPlayerList / BanNameList auto-kick paths intentionally
            // removed.
            //
            // (1) Auto-kick is disabled globally (see PlayerControlHelper.Kick)
            //     because Innersloth's anti-abuse self-bans the HOST when BAU
            //     calls KickPlayer rapidly. Those checks called Kick() which
            //     now no-ops — but it still left misleading "X has been banned
            //     due to ban player list" log lines for innocent players.
            //
            // (2) The original BanPlayerList check also had a logic bug — it
            //     OR'd ALL players' friend codes / hashPUIDs and kicked the
            //     JOINING player if ANY of them matched the file, instead of
            //     kicking the matched player. That false-fired on every join
            //     whenever any old auto-banned entry was still in the lobby.
            //
            // If you want manual ban enforcement, use AU's native BanMenu /
            // kick button. CheatData-driven lobby warnings (CTRL+Y popup
            // above) cover the "known cheater is back" use case.
        }, 2.5f, "OnPlayerJoinedPatch", false);
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerLeft))]
    [HarmonyPostfix]
    private static void AmongUsClient_OnPlayerLeft_Postfix(ClientData data, DisconnectReasons reason)
    {
        // Reclaim favorite color when player leaves in lobby
        if (GameState.IsLobby)
        {
            var favColorId = (byte)BAUPlugin.FavoriteColor.Value;
            if (BAUPlugin.FavoriteColor.Value >= 0)
            {
                if (PlayerControl.LocalPlayer.cosmetics.ColorId != favColorId && data.ColorId == favColorId)
                {
                    PlayerControl.LocalPlayer.CmdCheckColor(favColorId);
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
        if (player.BetterData() != null)
        {
            player.BetterData().DisconnectReason = reason;
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
        // Prevent showing duplicate notifications
        if (playerData.BetterData().AntiCheatInfo.BannedByAntiCheat || playerData.BetterData().HasShowDcMsg) return;
        playerData.BetterData().HasShowDcMsg = true;

        string? playerName = playerData.BetterData().RealName;

        // Use custom reason text if provided
        if (forceReasonText != "")
        {
            var ReasonText = $"<color=#ff0>{playerData.BetterData().RealName}</color> {forceReasonText}";

            Logger_.Log(ReasonText);

            HudManager.Instance.Notifier.AddDisconnectMessage(ReasonText);
        }
        else
        {
            string ReasonText;

            // Format disconnect message based on reason type
            switch (reason)
            {
                case DisconnectReasons.ExitGame:
                    ReasonText = string.Format(Translator.GetString("DisconnectReason.Left"), playerName);
                    break;
                case DisconnectReasons.ClientTimeout:
                    ReasonText = string.Format(Translator.GetString("DisconnectReason.Disconnect"), playerName);
                    break;
                case DisconnectReasons.Kicked:
                    ReasonText = string.Format(Translator.GetString("DisconnectReason.Kicked"), playerName, AmongUsClient.Instance.GetHost().Character.Data.PlayerName);
                    break;
                case DisconnectReasons.Banned:
                    ReasonText = string.Format(Translator.GetString("DisconnectReason.Banned"), playerName, AmongUsClient.Instance.GetHost().Character.Data.PlayerName);
                    break;
                case DisconnectReasons.Hacking:
                    ReasonText = string.Format(Translator.GetString("DisconnectReason.Cheater"), playerName);
                    break;
                case DisconnectReasons.Error:
                    ReasonText = string.Format(Translator.GetString("DisconnectReason.Error"), playerName);
                    break;
                case DisconnectReasons.Unknown:
                    ReasonText = string.Format(Translator.GetString("DisconnectReason.Unknown"), playerName);
                    break;
                default:
                    ReasonText = string.Format(Translator.GetString("DisconnectReason.Left"), playerName);
                    break;
            }

            Logger_.Log(ReasonText);

            // Add formatted disconnect message to game UI
            HudManager.Instance.Notifier.AddDisconnectMessage(ReasonText);
        }
    }
}