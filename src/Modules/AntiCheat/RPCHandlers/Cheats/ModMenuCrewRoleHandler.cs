using BetterAmongUs.Attributes;
using BetterAmongUs.Data;
using BetterAmongUs.Data.Config;
using BetterAmongUs.Enums;
using BetterAmongUs.Generated;
using BetterAmongUs.Managers;
using BetterAmongUs.Modules.Support;
using BetterAmongUs.MonoScripts.Extended;
using BetterAmongUs.Patches.Gameplay.UI.Settings;
using BetterAmongUs.Utilities;
using Hazel;
using InnerNet;

namespace BetterAmongUs.Modules.AntiCheat.RPCHandlers.Cheats;

/// <summary>
/// Detects ModMenuCrew's role-validation signature RPC (205) (GreaterAmongUs).
/// BetterAmongUs already catches ModMenuCrew's 202 and chat (201) RPCs; this adds the third one.
/// </summary>
[RegisterRPCHandler]
internal sealed class ModMenuCrewRoleHandler : RPCHandler
{
    internal override byte CallId => unchecked((byte)CustomRPC.ModMenuCrewRoleValidation);

    internal override void HandleCheatRpcCheck(PlayerControl? sender, MessageReader reader)
    {
        if (sender == null || sender.Data == null)
            return;

        if (BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_Anticheat))
            return;

        if (!BAUConfigs.AntiCheat.Value || !BetterGameSettings.DetectCheatClients.GetBool())
            return;

        if (!BetterDataManager.Files.BetterDataFile.MMCData.Any(info => info.CheckPlayerData(sender.Data)))
        {
            sender.ReportPlayer(ReportReasons.Cheating_Hacking);
            BetterDataManager.Files.BetterDataFile.MMCData.Add(new(sender.ExtendedData()?.RealName ?? sender.Data.PlayerName, sender.GetHashPuid(), sender.Data.FriendCode, "ModMenuCrew Role RPC"));
            BetterDataManager.Files.BetterDataFile.Save();
            BetterNotificationManager.NotifyCheat(sender, TranslationStrings.AntiCheat_Cheat_MMC.LocalizedString, TranslationStrings.AntiCheat_HasBeenDetectedWithCheatClient.LocalizedString);
        }
    }
}
