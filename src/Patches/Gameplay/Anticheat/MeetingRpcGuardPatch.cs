using AmongUs.GameOptions;
using BetterAmongUs.Data.Config;
using BetterAmongUs.Managers;
using BetterAmongUs.Modules;
using BetterAmongUs.Modules.AntiCheat;
using BetterAmongUs.Modules.Support;
using BetterAmongUs.Patches.Gameplay.UI.Settings;
using BetterAmongUs.Utilities.Extension;
using HarmonyLib;
using Hazel;

namespace BetterAmongUs.Patches.Gameplay.Anticheat;

/// <summary>
/// Anti-cheat for RPCs sent to the meeting (MeetingHud) and to the map (ShipStatus).
/// </summary>
/// <remarks>
/// The anti-cheat pipeline in NetworkManager only sees RPCs sent to players (PlayerControl,
/// PlayerPhysics, CustomNetworkTransform). Meeting RPCs (VotingComplete 23, CloseMeeting 22,
/// QueueOverruleVotes 66) and the Influencer's SpiritGuideMessage (67, on ShipStatus) never went
/// through it, so these checks hook the game's own HandleRpc for those objects instead.
///
/// The game does not tell us who sent a meeting/map RPC. Where the message itself names a
/// player (the Judge id in an overrule) that player is flagged; otherwise the alert says the
/// sender is hidden.
/// </remarks>
[HarmonyPatch]
internal static class MeetingRpcGuardPatch
{
    private static bool Enabled =>
        BetterAntiCheat.IsEnabled
        && BAUConfigs.AntiCheat.Value
        && !BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_Anticheat)
        && BetterGameSettings.DetectInvalidRpcs.GetBool();

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.HandleRpc))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool MeetingHud_HandleRpc_Prefix(byte callId, MessageReader reader)
    {
        try
        {
            if (!Enabled || reader == null)
                return true;

            return callId switch
            {
                (byte)RpcCalls.QueueOverruleVotes => CheckQueueOverruleVotes(reader),
                (byte)RpcCalls.VotingComplete => CheckHostOnlyMeetingRpc("VotingComplete", "forced the meeting result (force skip / force eject)"),
                (byte)RpcCalls.CloseMeeting => CheckHostOnlyMeetingRpc("CloseMeeting", "force-closed the meeting"),
                _ => true,
            };
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, "MeetingRpcGuard");
            return true;
        }
    }

    /// <summary>
    /// QueueOverruleVotes (66): only a living Judge may overrule the vote.
    /// Payload (CmdQueueOverruleVotes): judge id (byte), target id (byte), nonce (ushort).
    /// The role we check is the one the host assigned and synced, so a cheat that only
    /// changes its own local role can't pass.
    /// </summary>
    private static bool CheckQueueOverruleVotes(MessageReader oldReader)
    {
        MessageReader reader = MessageReader.Get(oldReader);
        try
        {
            // Unexpected layout (e.g. a future game update added fields): don't guess, let it through.
            if (reader.BytesRemaining != 4)
            {
                BAUPlugin.Logger.Warning($"QueueOverruleVotes has {reader.BytesRemaining} bytes (expected 4) - not checked", "MeetingRpcGuard");
                return true;
            }

            byte judgeId = reader.ReadByte();
            byte targetId = reader.ReadByte();
            ushort nonce = reader.ReadUInt16();

            var judgeData = GameData.Instance != null ? GameData.Instance.GetPlayerById(judgeId) : null;

            string? problem = null;
            if (judgeData == null)
                problem = "unknown player";
            else if (judgeData.RoleType != RoleTypes.Judge)
                problem = "not a Judge";
            else if (judgeData.IsDead || judgeData.Disconnected)
                problem = "the Judge is dead";

            if (problem == null)
            {
                if (GameState.IsHost)
                {
                    JudgeTracker.Record(judgeId, targetId, nonce);
                }

                return true;
            }

            var claimed = judgeData != null ? judgeData.Object : null;
            if (claimed != null)
            {
                BetterNotificationManager.NotifyCheat(claimed, $"Fake Judge overrule ({problem})");
            }
            else
            {
                AlertHiddenSender("fake-judge", $"Fake Judge overrule blocked ({problem}, player id {judgeId}).");
            }

            BAUPlugin.Logger.LogCheat($"QueueOverruleVotes canceled: judge={judgeId} target={targetId} nonce={nonce} ({problem})");
            return false;
        }
        finally
        {
            reader.Recycle();
        }
    }

    /// <summary>
    /// VotingComplete (23) / CloseMeeting (22): only the host ends a meeting. The host applies these
    /// itself and never receives them from the network, so one arriving at the host is forged.
    /// </summary>
    private static bool CheckHostOnlyMeetingRpc(string rpcName, string what)
    {
        if (!GameState.IsHost)
            return true;

        AlertHiddenSender($"forged-{rpcName}", $"Forged {rpcName} blocked: a player {what}.");
        BAUPlugin.Logger.LogCheat($"{rpcName} received as host - canceled (forged)");
        return false;
    }

    /// <summary>
    /// Every client: a meeting result that says "overruled" while no living Judge is in the game was forged.
    /// (Can't be canceled on a non-host without freezing our own meeting, so this only alerts.)
    /// </summary>
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.VotingComplete))]
    [HarmonyPostfix]
    private static void MeetingHud_VotingComplete_Postfix(bool wasOverruled)
    {
        try
        {
            if (!wasOverruled || GameState.IsHost || !Enabled || GameData.Instance == null)
                return;

            bool livingJudge = GameData.Instance.AllPlayers.AnyIl2Cpp(p => p != null && p.RoleType == RoleTypes.Judge && !p.IsDead);
            if (!livingJudge)
            {
                AlertHiddenSender("forged-overrule", "Meeting result claims a Judge overrule, but no living Judge is in this game - most likely forged.");
                BAUPlugin.Logger.LogCheat("VotingComplete with wasOverruled=true but no living Judge");
            }
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, "MeetingRpcGuard");
        }
    }

    /// <summary>
    /// SpiritGuideMessage (67): Influencer pictures. They are sent to one living player only, and the
    /// game never says who sent them or checks that the sender really is an Influencer. One arriving
    /// while nobody in the game holds the Influencer role can only be forged, so it is blocked.
    /// </summary>
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.HandleRpc))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool ShipStatus_HandleRpc_Prefix(byte callId)
    {
        try
        {
            if (callId != (byte)RpcCalls.SpiritGuideMessage || !Enabled || GameData.Instance == null)
                return true;

            bool anyInfluencer = GameData.Instance.AllPlayers.AnyIl2Cpp(p => p != null && p.RoleType == RoleTypes.SpiritGuide);
            if (anyInfluencer)
                return true;

            AlertHiddenSender("fake-influencer", "Fake Influencer message blocked: picture message received, but nobody in this game is an Influencer.");
            BAUPlugin.Logger.LogCheat("SpiritGuideMessage received with no Influencer in the game - canceled (forged)");
            return false;
        }
        catch (Exception ex)
        {
            BAUPlugin.Logger.Error(ex, "MeetingRpcGuard");
            return true;
        }
    }

    private static void AlertHiddenSender(string key, string text)
    {
        BetterNotificationManager.NotifySticky(key,
            $"<color=#fc0000><b>{text}</b></color>\n<size=80%><color=#cccccc>The game hides who sent it, so no player can be named.</color></size>");
    }
}
