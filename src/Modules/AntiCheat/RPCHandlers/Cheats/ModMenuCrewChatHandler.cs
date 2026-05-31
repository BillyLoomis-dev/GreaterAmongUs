using BetterAmongUs.Attributes;
using BetterAmongUs.Data;
using BetterAmongUs.Enums;
using BetterAmongUs.Helpers;
using BetterAmongUs.Managers;
using BetterAmongUs.Modules.Support;
using BetterAmongUs.Mono;
using BetterAmongUs.Patches.Gameplay.UI.Settings;
using Hazel;
using InnerNet;

namespace BetterAmongUs.Modules.AntiCheat;

[RegisterRPCHandler]
internal sealed class ModMenuCrewChatHandler : RPCHandler
{
    internal override byte CallId => unchecked((byte)CustomRPC.ModMenuCrewChatBypass);

    internal override void HandleCheatRpcCheck(PlayerControl? sender, MessageReader reader)
    {
        if (sender == null || sender.Data == null) return;
        if (!BAUPlugin.AntiCheat.Value || BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_Anticheat) || !BetterGameSettings.DetectCheatClients.GetBool()) return;

        if (!BetterDataManager.BetterDataFile.CheatData.Any(info => info.CheckPlayerData(sender.Data)))
        {
            sender.ReportPlayer(ReportReasons.Cheating_Hacking);
            BetterNotificationManager.NotifyCheat(sender, Translator.GetString("AntiCheat.Cheat.ModMenuCrew"), Translator.GetString("AntiCheat.HasBeenDetectedWithCheat2"));
        }
    }
}
