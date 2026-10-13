using AmongUs.Data.Player;
using HarmonyLib;

namespace BetterAmongUs.Patches.Client;

[HarmonyPatch]
internal static class DisconnectPenaltyPatch
{
    [HarmonyPatch(typeof(PlayerBanData), nameof(PlayerBanData.IsBanned), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool PlayerBanData_IsBanned_Prefix(PlayerBanData __instance, ref bool __result)
    {
        // Reset ban points to zero to prevent disconnect penalties
        __instance.BanPoints = 0f;
        __instance.banPoints = 0f;

        // Always return false (not banned) regardless of actual ban status
        __result = false;
        return false;
    }
}