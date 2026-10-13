using AmongUs.GameOptions;
using BetterAmongUs.MonoScripts.Extended;
using BetterAmongUs.Utilities;
using HarmonyLib;

namespace BetterAmongUs.Patches.Gameplay.Managers;

[HarmonyPatch]
internal static class RoleManagerPatch
{
    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SetRole))]
    [HarmonyPrefix]
    private static void RoleManager_SetRole_Prefix(RoleManager __instance, PlayerControl targetPlayer, RoleTypes roleType)
    {
        // Store the original role when player dies (for ghost role purposes)
        if (targetPlayer == null || targetPlayer.Data == null)
            return;

        if (roleType.IsGhostRole())
        {
            if (!targetPlayer.Data.RoleType.IsGhostRole())
            {
                var extended = targetPlayer.ExtendedData();
                if (extended != null && extended.RoleInfo != null)
                {
                    extended.RoleInfo.DeadDisplayRole = targetPlayer.Data.RoleType;
                    extended.RoleInfo.HasDeadDisplayRole = true;
                }
            }
        }
        else
        {
            // A new living role (new game): forget the previous game's role at death.
            var extended = targetPlayer.ExtendedData();
            if (extended != null && extended.RoleInfo != null)
            {
                extended.RoleInfo.HasDeadDisplayRole = false;
            }
        }
    }
}