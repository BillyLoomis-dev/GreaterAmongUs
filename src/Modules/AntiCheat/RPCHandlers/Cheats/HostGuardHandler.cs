using BetterAmongUs.Attributes;
using BetterAmongUs.Data.Config;
using BetterAmongUs.Enums;
using BetterAmongUs.Generated;
using BetterAmongUs.Managers;
using BetterAmongUs.Modules.Support;
using BetterAmongUs.MonoScripts.Extended;
using BetterAmongUs.Patches.Gameplay.UI.Settings;
using Hazel;

namespace BetterAmongUs.Modules.AntiCheat.RPCHandlers.Cheats;

/// <summary>
/// Detects the HostGuard mod's signature RPC (GreaterAmongUs).
/// HostGuard is NOT a cheat, it is a host-side anti-cheat: this is an info-only notice
/// (no report, no kick, nothing saved), shown once per player.
/// </summary>
[RegisterRPCHandler]
internal sealed class HostGuardHandler : RPCHandler
{
    internal override byte CallId => unchecked((byte)CustomRPC.HostGuard);

    // Notify once per player id per session.
    private static readonly HashSet<byte> NoticedPlayers = [];

    internal override void HandleCheatRpcCheck(PlayerControl? sender, MessageReader reader)
    {
        if (sender == null || sender.Data == null)
            return;

        if (BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_Anticheat))
            return;

        if (!BAUConfigs.AntiCheat.Value || !BetterGameSettings.DetectCheatClients.GetBool())
            return;

        if (!NoticedPlayers.Add(sender.PlayerId))
            return;

        string name = sender.ExtendedData()?.RealName ?? sender.Data.PlayerName;
        BetterNotificationManager.Notify(TranslationStrings.AntiCheat_Mod_HostGuard.Format(name));
        BAUPlugin.Logger.LogCheat($"{name} HostGuard mod signature detected (info only - not flagged)");
    }
}
