using BetterAmongUs.Helpers;
using BetterAmongUs.Mono;
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
        if (__instance.Player.BetterData().RoleInfo.HasNoisemakerNotify)
        {
            return false;
        }

        __instance.Player.BetterData().RoleInfo.HasNoisemakerNotify = true;

        return true;
    }

    // Judge overrule tracking: record who each Judge overruled, for the dead-only role reveal.
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.SetJudgeOverrule))]
    [HarmonyPostfix]
    private static void MeetingHud_SetJudgeOverrule_Postfix(MeetingHud __instance, PlayerId judgePlayerId, PlayerId targetPlayerId, ushort overruleNonce)
    {
        var roleInfo = Utils.PlayerFromPlayerId(judgePlayerId.Value)?.BetterData()?.RoleInfo;
        if (roleInfo != null)
        {
            roleInfo.JudgedId = targetPlayerId.Value;
        }
    }
}
