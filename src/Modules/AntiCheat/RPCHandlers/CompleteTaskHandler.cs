using BetterAmongUs.Attributes;
using BetterAmongUs.Utilities;
using BetterAmongUs.Managers;
using Hazel;
using BetterAmongUs.Utilities.Extension;
using BetterAmongUs.MonoScripts.Extended;

namespace BetterAmongUs.Modules.AntiCheat.RPCHandlers;

[RegisterRPCHandler]
internal sealed class CompleteTaskHandler : RPCHandler
{
    internal override byte CallId => (byte)RpcCalls.CompleteTask;

    /// <summary>Tasks finished closer together than this count as "fast".</summary>
    private const float FastTaskGap = 1.25f;

    /// <summary>
    /// How many fast gaps in a row are needed to report (2 gaps = 3 tasks).
    /// One fast gap is normal with lag: two finished tasks can arrive in the same moment.
    /// </summary>
    private const int FastGapsToReport = 2;

    internal override void HandleAntiCheat(PlayerControl? sender, MessageReader reader)
    {
        var taskId = reader.ReadPackedUInt32();
        var info = sender?.ExtendedData()?.AntiCheatInfo;
        if (sender == null || sender.Data == null || info == null)
            return;

        string? issue = null;

        if (sender.IsImpostorTeam())
        {
            issue = "Impostor completing tasks";
        }
        else if (!sender.Data.Tasks.AnyIl2Cpp(task => task.Id == taskId))
        {
            issue = $"Task ID {taskId} not assigned to player";
        }
        else if (info.LastTaskId == taskId)
        {
            issue = $"Task ID {taskId} completed twice";
        }
        else
        {
            // GreaterAmongUs: BetterAmongUs reported any two tasks less than 1.25 s apart, which lag
            // does by itself (both arrive together). Now it takes 3 or more tasks in a row.
            if (info.TimeSinceLastTask < FastTaskGap)
                info.FastTaskStreak++;
            else
                info.FastTaskStreak = 0;

            if (info.FastTaskStreak >= FastGapsToReport)
                issue = $"Tasks too fast ({info.FastTaskStreak + 1} tasks in a row less than {FastTaskGap}s apart)";
        }

        if (issue != null)
        {
            if (BetterNotificationManager.NotifyCheat(sender, GetFormatActionText()))
            {
                LogRpcInfo($"Invalid task completion: {issue}");
            }
        }

        info.TimeSinceLastTask = 0f;
        info.LastTaskId = taskId;
    }

    /// <summary>
    /// Clears the task tracking when a match starts, so the last task of the previous match
    /// can't count as "completed twice".
    /// </summary>
    internal static void ResetForNewMatch()
    {
        if (GameData.Instance == null)
            return;

        foreach (var data in GameData.Instance.AllPlayers)
        {
            if (data == null)
                continue;

            var info = data.ExtendedData()?.AntiCheatInfo;
            if (info == null)
                continue;

            info.LastTaskId = ExtendedAntiCheatInfo.NoTaskId;
            info.TimeSinceLastTask = 5f;
            info.FastTaskStreak = 0;
        }
    }
}
