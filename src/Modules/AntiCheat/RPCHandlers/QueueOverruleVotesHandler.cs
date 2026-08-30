using AmongUs.GameOptions;
using BetterAmongUs.Attributes;
using BetterAmongUs.Helpers;
using BetterAmongUs.Managers;
using Hazel;
using InnerNet;

namespace BetterAmongUs.Modules.AntiCheat;

/// <summary>
/// Catches a FAKE Judge overrule (the Judge update's QueueOverruleVotes RPC, id 66).
/// Only a genuine Judge may queue a vote overrule; a non-Judge sending it is hijacking the
/// Judge's power to force-eject anyone.
///
/// Validated host-side: QueueOverruleVotes is a client->host command, so the host is the
/// authority, and a cheater's LOCAL role spoof never changes the host-synced role we check
/// here. So even if a cheat lets a non-Judge *appear* to be a Judge, the instant they use
/// the power over the network we see their real role and drop it.
/// </summary>
[RegisterRPCHandler]
internal sealed class QueueOverruleVotesHandler : RPCHandler
{
    internal override byte CallId => (byte)RpcCalls.QueueOverruleVotes;

    internal override bool HandleAntiCheatCancel(PlayerControl? sender, MessageReader reader)
    {
        if (!GameState.IsHost) return true;                 // only the host is the authority here
        if (sender == null || sender.Data == null) return true;

        // The overrule power belongs to a living Judge alone. Anyone else queuing it is a cheat.
        if (!sender.Is(RoleTypes.Judge) || !sender.IsAlive())
        {
            BetterNotificationManager.NotifyCheat(sender, "Fake Judge overrule (not a Judge)");
            return false;   // cancel -> the fake overrule never takes effect
        }

        return true;
    }
}
