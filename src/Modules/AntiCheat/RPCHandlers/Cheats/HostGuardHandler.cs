using BetterAmongUs.Attributes;
using BetterAmongUs.Enums;
using BetterAmongUs.Helpers;
using BetterAmongUs.Managers;
using BetterAmongUs.Modules.Support;
using BetterAmongUs.Mono;
using BetterAmongUs.Patches.Gameplay.UI.Settings;
using Hazel;

namespace BetterAmongUs.Modules.AntiCheat;

/// <summary>
/// Detects the HostGuard host-side anti-cheat mod's signature RPC.
/// HostGuard is NOT a cheat — it's a defensive mod. We surface its presence
/// as an info-only notice (no report, no kick, no CheatData persistence),
/// mirroring how NCAU treats BetterAmongUs's own signature at callId 150.
/// </summary>
[RegisterRPCHandler]
internal sealed class HostGuardHandler : RPCHandler
{
    internal override byte CallId => unchecked((byte)CustomRPC.HostGuard);

    // Dedupe — notify once per player per session.
    private static readonly HashSet<byte> NoticedPlayers = [];

    internal override void HandleCheatRpcCheck(PlayerControl? sender, MessageReader reader)
    {
        if (sender == null || sender.Data == null) return;
        if (!BAUPlugin.AntiCheat.Value || BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_Anticheat) || !BetterGameSettings.DetectCheatClients.GetBool()) return;

        if (!NoticedPlayers.Add(sender.PlayerId)) return;

        string name = sender.BetterData()?.RealName ?? sender.Data.PlayerName;
        string text = string.Format(Translator.GetString("AntiCheat.Mod.HostGuard"), name);
        BetterNotificationManager.Notify(text);
        Logger_.LogCheat($"{name} HostGuard mod signature detected (info only — not flagged)");
    }
}
