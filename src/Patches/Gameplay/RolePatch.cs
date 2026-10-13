using BetterAmongUs.Modules;
using BetterAmongUs.MonoScripts.Extended;
using BetterAmongUs.Utilities;
using HarmonyLib;
using InnerNet;

namespace BetterAmongUs.Patches.Gameplay;

[HarmonyPatch]
internal static class RolePatch
{
    [HarmonyPatch(typeof(NoisemakerRole), nameof(NoisemakerRole.OnDeath))]
    [HarmonyPrefix]
    private static bool NoisemakerRole_NotifyOfDeath_Prefix(NoisemakerRole __instance)
    {
        // Prevent duplicate noisemaker notifications
        if (__instance.Player.ExtendedData().RoleInfo.HasNoisemakerNotify)
        {
            return false;
        }

        // Mark that notification has been sent
        __instance.Player.ExtendedData().RoleInfo.HasNoisemakerNotify = true;

        return true;
    }

    // NOTE: SetJudgeOverrule does not run on every client (only where the overrule UI runs),
    // so JudgeTracker mainly reads the synced overrule queue; this is an extra source.
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.SetJudgeOverrule))]
    [HarmonyPostfix]
    private static void MeetingHud_SetJudgeOverrule_Postfix(MeetingHud __instance, PlayerId judgePlayerId, PlayerId targetPlayerId, ushort overruleNonce)
    {
        var judge = Utils.PlayerFromPlayerId(judgePlayerId);
        if (judge != null)
        {
            var extended = judge.ExtendedData();
            if (extended != null && extended.RoleInfo != null)
            {
                extended.RoleInfo.Judged = targetPlayerId;
            }
        }

        JudgeTracker.Record(judgePlayerId.Value, targetPlayerId.Value, overruleNonce);
    }
}