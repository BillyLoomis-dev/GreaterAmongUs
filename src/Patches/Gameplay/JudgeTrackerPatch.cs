using BetterAmongUs.Modules;
using HarmonyLib;

namespace BetterAmongUs.Patches.Gameplay;

/// <summary>
/// Feeds <see cref="JudgeTracker"/> so dead players can see who judged who.
/// </summary>
[HarmonyPatch]
internal static class JudgeTrackerPatch
{
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
    [HarmonyPostfix]
    private static void AmongUsClient_CoStartGame_Postfix()
    {
        JudgeTracker.Reset();
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    [HarmonyPostfix]
    private static void MeetingHud_Start_Postfix()
    {
        JudgeTracker.OnMeetingStart();
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
    [HarmonyPostfix]
    private static void MeetingHud_Update_Postfix(MeetingHud __instance)
    {
        try
        {
            JudgeTracker.Poll(__instance);
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, "JudgeTracker");
        }
    }

    // Read the queue once more right before the result is applied.
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.VotingComplete))]
    [HarmonyPrefix]
    private static void MeetingHud_VotingComplete_Prefix(MeetingHud __instance)
    {
        try
        {
            JudgeTracker.Poll(__instance);
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, "JudgeTracker");
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.VotingComplete))]
    [HarmonyPostfix]
    private static void MeetingHud_VotingComplete_Postfix(NetworkedPlayerInfo exiled, bool wasOverruled, ushort overruleNonce)
    {
        try
        {
            JudgeTracker.OnVotingComplete(exiled, wasOverruled, overruleNonce);
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, "JudgeTracker");
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
    [HarmonyPostfix]
    private static void MeetingHud_Close_Postfix()
    {
        JudgeTracker.FinalizeMeeting();
    }
}
