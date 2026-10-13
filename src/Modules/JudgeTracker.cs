using AmongUs.GameOptions;
using BetterAmongUs.MonoScripts.Extended;
using BetterAmongUs.Utilities;
using BetterAmongUs.Utilities.Extension;

namespace BetterAmongUs.Modules;

/// <summary>
/// Tracks Judge overrules ("who judged who") for the current game, on the host and on every other client.
/// </summary>
/// <remarks>
/// The overrule TARGET is never sent in an RPC that a normal (non-host) player receives:
/// QueueOverruleVotes (66) goes to the host only, and VotingComplete (23) only carries an
/// overrule nonce. The target lives in MeetingHud.judgeOverrulesQueue, which the host syncs to
/// every client, so we read that queue while the meeting runs and match VotingComplete's nonce
/// against it. MeetingHud.SetJudgeOverrule (the hook BetterAmongUs uses) only runs on some
/// clients, so it is just an extra source here.
///
/// What players see (see <see cref="GetSuffix"/>):
/// - dead players (not the Influencer): "judging X" live during the meeting, then
///   "judged X" on the Judge and "judged by Y" on the target for the rest of the game;
/// - the Judge always sees their own choice;
/// - everyone sees the result in the end-of-game summary.
/// Living players never see who the Judge is through this.
/// </remarks>
internal static class JudgeTracker
{
    /// <summary>
    /// The Judge role color.
    /// </summary>
    internal const string JudgeHexColor = "#e3d12b";

    /// <summary>
    /// Player id meaning "unknown target" (the overrule happened but its target never reached us).
    /// </summary>
    private const byte UnknownTarget = byte.MaxValue;

    // Every overrule seen this game: nonce -> (judge, target).
    private static readonly Dictionary<ushort, (byte judge, byte target)> _byNonce = [];

    // Overrules queued in the current meeting: judge -> target.
    private static readonly Dictionary<byte, byte> _pending = [];

    // Applied overrules this game: judge -> targets, in order.
    private static readonly Dictionary<byte, List<byte>> _verdicts = [];

    // VotingComplete said "overruled" but the nonce wasn't resolved yet.
    private static bool _awaitingNonce;
    private static ushort _awaitingNonceValue;

    /// <summary>
    /// Clears everything (new game).
    /// </summary>
    internal static void Reset()
    {
        _byNonce.Clear();
        _pending.Clear();
        _verdicts.Clear();
        _awaitingNonce = false;
        _awaitingNonceValue = 0;
    }

    /// <summary>
    /// Called when a meeting starts.
    /// </summary>
    internal static void OnMeetingStart()
    {
        _pending.Clear();
        _awaitingNonce = false;
    }

    /// <summary>
    /// Records an overrule (from the synced queue, from SetJudgeOverrule, or from the host's own RPC check).
    /// </summary>
    /// <param name="judge">The Judge's player id.</param>
    /// <param name="target">The overruled player's id.</param>
    /// <param name="nonce">The overrule nonce that VotingComplete echoes back.</param>
    internal static void Record(byte judge, byte target, ushort nonce)
    {
        if (judge == byte.MaxValue)
            return;

        _byNonce[nonce] = (judge, target);

        if (!_pending.TryGetValue(judge, out var previous) || previous != target)
        {
            _pending[judge] = target;
            BAUPlugin.Logger.LogHidden($"{Name(judge)} (Judge) chose to overrule the vote on {Name(target)}", "EventLog", showInConsole: false);
        }

        // VotingComplete already came in but the queue sync arrived after it: finish now.
        if (_awaitingNonce && _awaitingNonceValue == nonce)
        {
            _awaitingNonce = false;
            AddVerdict(judge, target);
        }
    }

    /// <summary>
    /// Reads the replicated overrule queue of the current meeting.
    /// </summary>
    /// <param name="meetingHud">The meeting.</param>
    internal static void Poll(MeetingHud meetingHud)
    {
        if (meetingHud == null)
            return;

        var queue = meetingHud.judgeOverrulesQueue;
        if (queue == null || queue.Count == 0)
            return;

        for (int i = 0; i < queue.Count; i++)
        {
            var entry = queue[i];
            if (entry == null)
                continue;

            Record(entry.JudgePlayerId.Value, entry.OverruledPlayerId.Value, entry.OverruleNonce);
        }
    }

    /// <summary>
    /// Called after VotingComplete applies the meeting result (runs on every client).
    /// </summary>
    internal static void OnVotingComplete(NetworkedPlayerInfo? exiled, bool wasOverruled, ushort overruleNonce)
    {
        if (!wasOverruled)
            return;

        string ejected = exiled != null ? Name(exiled.PlayerId) : "no one";

        if (_byNonce.TryGetValue(overruleNonce, out var overrule))
        {
            AddVerdict(overrule.judge, overrule.target);
            BAUPlugin.Logger.LogHidden($"{Name(overrule.judge)} (Judge) judged {Name(overrule.target)} -> {ejected} was ejected", "EventLog", showInConsole: false);
            return;
        }

        // The queue sync may still be on its way; Record() finishes this when it arrives,
        // and FinalizeMeeting() falls back to the known Judge if it never does.
        _awaitingNonce = true;
        _awaitingNonceValue = overruleNonce;
        BAUPlugin.Logger.LogHidden($"A Judge overruled the vote -> {ejected} was ejected (target not synced yet)", "EventLog", showInConsole: false);
    }

    /// <summary>
    /// Called when the meeting closes: resolves an overrule whose target never arrived.
    /// </summary>
    internal static void FinalizeMeeting()
    {
        if (_awaitingNonce)
        {
            _awaitingNonce = false;

            // Only one Judge can have done it if there is only one living Judge.
            List<NetworkedPlayerInfo> judges = [];
            if (GameData.Instance != null)
            {
                judges = GameData.Instance.AllPlayers.WhereIl2Cpp(p => p != null && p.RoleType == RoleTypes.Judge);
            }

            if (judges.Count == 1)
            {
                AddVerdict(judges[0].PlayerId, UnknownTarget);
            }
        }

        _pending.Clear();
    }

    private static void AddVerdict(byte judge, byte target)
    {
        if (!_verdicts.TryGetValue(judge, out var targets))
        {
            targets = [];
            _verdicts[judge] = targets;
        }

        if (targets.Count == 0 || targets[^1] != target)
        {
            targets.Add(target);
        }

        _pending.Remove(judge);

        // Keep BetterAmongUs' own field in sync.
        if (target != UnknownTarget && GameData.Instance != null)
        {
            var judgeData = GameData.Instance.GetPlayerById(judge);
            var extended = judgeData != null ? judgeData.ExtendedData() : null;
            if (extended != null && extended.RoleInfo != null)
            {
                extended.RoleInfo.Judged = target;
            }
        }
    }

    /// <summary>
    /// Whether the local player may see who judged who (dead, and not the Influencer who could pass it on).
    /// </summary>
    internal static bool LocalCanSeeJudgements()
    {
        var local = PlayerControl.LocalPlayer;
        if (local == null || local.Data == null)
            return false;

        return local.Data.IsDead && local.Data.RoleType != RoleTypes.SpiritGuide;
    }

    /// <summary>
    /// Gets the "(judged X)" / "(judged by Y)" text for a player, already colored, or an empty string.
    /// </summary>
    /// <param name="playerId">The player the text is for.</param>
    /// <param name="showAll">True to show everything (dead viewer, end-of-game summary). False shows only the local Judge's own choice.</param>
    internal static string GetSuffix(byte playerId, bool showAll)
    {
        var parts = new List<string>();

        bool isSelf = PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.PlayerId == playerId;
        if (showAll || isSelf)
        {
            if (_verdicts.TryGetValue(playerId, out var targets) && targets.Count > 0)
            {
                foreach (var target in targets)
                {
                    parts.Add(target == UnknownTarget ? "overruled the vote" : $"judged {Name(target)}");
                }
            }
            else if (_pending.TryGetValue(playerId, out var pendingTarget))
            {
                parts.Add($"judging {Name(pendingTarget)}");
            }
        }

        if (showAll)
        {
            foreach (var (judge, targets) in _verdicts)
            {
                if (targets.Contains(playerId))
                {
                    parts.Add($"judged by {Name(judge)}");
                }
            }

            foreach (var (judge, target) in _pending)
            {
                if (target == playerId && !_verdicts.ContainsKey(judge))
                {
                    parts.Add($"being judged by {Name(judge)}");
                }
            }
        }

        return parts.Count == 0 ? string.Empty : $" <color={JudgeHexColor}>({string.Join(", ", parts)})</color>";
    }

    private static string Name(byte playerId)
    {
        if (playerId == UnknownTarget)
            return "?";

        if (GameData.Instance != null)
        {
            var data = GameData.Instance.GetPlayerById(playerId);
            if (data != null)
            {
                var extended = data.ExtendedData();
                return extended != null ? extended.RealName : data.PlayerName;
            }
        }

        return $"#{playerId}";
    }
}
