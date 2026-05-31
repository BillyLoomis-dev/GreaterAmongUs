using BetterAmongUs.Attributes;
using BetterAmongUs.Helpers;
using BetterAmongUs.Managers;
using BetterAmongUs.Mono;
using Hazel;

namespace BetterAmongUs.Modules.AntiCheat;

[RegisterRPCHandler]
internal sealed class SetLevelHandler : RPCHandler
{
    internal override byte CallId => (byte)RpcCalls.SetLevel;

    // NOTE: We deliberately do NOT compare reported level against a threshold
    // (formerly BetterGameSettings.DetectedLevelAbove). Innersloth has had
    // long-standing XP-overflow glitches that legitimately push player levels
    // far above 100 — there are known legit players above lv 1000. There is
    // no wire signal that distinguishes a glitch beneficiary from a spoofer
    // at the same level, so any threshold guarantees false positives.
    //
    // The only reliable level-related signal is "client sent SetLevel more
    // than once in the same session", which catches LIVE level-spoofing.
    // Vanilla AU only sends SetLevel once at session start; a second SetLevel
    // means the client is actively modifying its level mid-session. That is
    // what HandleAntiCheatCancel below catches.

    internal override bool HandleAntiCheatCancel(PlayerControl? sender, MessageReader reader)
    {
        if (sender.DataIsCollected() == true && sender.BetterData().AntiCheatInfo.HasSetLevel && !GameState.IsLocalGame && GameState.IsVanillaServer)
        {
            if (BetterNotificationManager.NotifyCheat(sender, GetFormatSetText()))
            {
                LogRpcInfo($"Player attempted to set level multiple times");
            }

            return false;
        }

        sender.BetterData().AntiCheatInfo.HasSetLevel = true;

        return true;
    }
}
