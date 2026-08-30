using BetterAmongUs.Helpers;
using BetterAmongUs.Modules;
using BetterAmongUs.Modules.Support;
using BetterAmongUs.Mono;
using HarmonyLib;
using System.Text;
using TMPro;
using UnityEngine;

namespace BetterAmongUs.Patches.Gameplay.Managers;

[HarmonyPatch]
internal static class GameSummaryPatch
{
    [HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
    [HarmonyPostfix]
    private static void EndGameManager_SetEverythingUp_Postfix(EndGameManager __instance)
    {
        LogGameEnd();

        if (!BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_EndGameSummary))
        {
            CreateGameSummary(__instance);
        }
    }

    private static void LogGameEnd()
    {
        Logger_.LogHeader($"Game Has Ended - {Enum.GetName(typeof(MapNames), GameState.GetActiveMapId)}/{GameState.GetActiveMapId}", "GamePlayManager");
        Logger_.LogHeader("Game Summary Start", "GameSummary");
    }

    private static void CreateGameSummary(EndGameManager endGameManager)
    {
        var summaryObject = CreateSummaryObject(endGameManager);
        var summaryText = summaryObject.GetComponent<TextMeshPro>();

        if (summaryText == null) return;

        ConfigureSummaryText(summaryText);

        var (winTeam, winTag, winColor) = GetWinInfo();
        Logger_.Log($"{winTeam}: {winTag}", "GameSummary");

        var summaryHeader = BuildSummaryHeader(winTeam, winTag, winColor);
        var playerList = BuildPlayerList();

        summaryText.text = $"{summaryHeader}\n\n<size=58%>{playerList}</size>";
        Logger_.LogHeader("Game Summary End", "GameSummary");
    }

    private static GameObject CreateSummaryObject(EndGameManager endGameManager)
    {
        var summaryObject = UnityEngine.Object.Instantiate(
            endGameManager.WinText.gameObject,
            endGameManager.WinText.transform.parent
        );

        summaryObject.name = "SummaryObj (TMP)";
        summaryObject.transform.SetSiblingIndex(0);

        var camera = HudManager.InstanceExists
            ? HudManager.Instance.GetComponentInChildren<Camera>()
            : Camera.main;

        var position = AspectPosition.ComputeWorldPosition(
            camera,
            AspectPosition.EdgeAlignments.LeftTop,
            new Vector3(1f, 0.2f, -5f)
        );

        summaryObject.transform.position = position;
        summaryObject.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);

        return summaryObject;
    }

    private static void ConfigureSummaryText(TextMeshPro text)
    {
        text.autoSizeTextContainer = false;
        text.enableAutoSizing = false;
        text.lineSpacing = -25f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = Color.white;
    }

    private static (string Team, string Tag, string Color) GetWinInfo()
    {
        return EndGameResult.CachedGameOverReason switch
        {
            GameOverReason.CrewmatesByTask => (
                Translator.GetString(StringNames.Crewmates),
                Translator.GetString("Game.Summary.Result.TasksCompletion"),
                "#8cffff"
            ),
            GameOverReason.CrewmatesByVote => (
                Translator.GetString(StringNames.Crewmates),
                Translator.GetString("Game.Summary.Result.ImpostersVotedOut"),
                "#8cffff"
            ),
            GameOverReason.ImpostorDisconnect => (
                Translator.GetString(StringNames.Crewmates),
                Translator.GetString("Game.Summary.Result.ImpostorsDisconnected"),
                "#8cffff"
            ),
            GameOverReason.ImpostorsByKill => (
                Translator.GetString(StringNames.ImpostorsCategory),
                Translator.GetString("Game.Summary.Result.CrewOutnumbered"),
                "#f00202"
            ),
            GameOverReason.ImpostorsBySabotage => (
                Translator.GetString(StringNames.ImpostorsCategory),
                Translator.GetString("Game.Summary.Result.Sabotage"),
                "#f00202"
            ),
            GameOverReason.ImpostorsByVote => (
                Translator.GetString(StringNames.ImpostorsCategory),
                Translator.GetString("Game.Summary.Result.CrewOutnumbered"),
                "#f00202"
            ),
            GameOverReason.CrewmateDisconnect => (
                Translator.GetString(StringNames.ImpostorsCategory),
                Translator.GetString("Game.Summary.Result.CrematesDisconnected"),
                "#f00202"
            ),
            GameOverReason.HideAndSeek_CrewmatesByTimer => (
                Translator.GetString("Game.Summary.Hiders"),
                Translator.GetString("Game.Summary.Result.TimeOut"),
                "#8cffff"
            ),
            GameOverReason.HideAndSeek_ImpostorsByKills => (
                Translator.GetString("Game.Summary.Seekers"),
                Translator.GetString("Game.Summary.Result.NoSurvivors"),
                "#f00202"
            ),
            _ => ("Unknown", "Unknown", "#ffffff")
        };
    }

    private static string BuildSummaryHeader(string winTeam, string winTag, string winColor)
    {
        return $"<align=\"center\"><size=150%>   {Translator.GetString("GameSummary")}</size></align>" +
               $"\n\n<size=90%><color={winColor}>{winTeam} {Translator.GetString("Game.Summary.Won")}</color></size>" +
               $"\n<size=60%>\n{Translator.GetString("Game.Summary.By")} {winTag}</size>";
    }

    private static NetworkedPlayerInfo[] GetSortedPlayers()
    {
        return GameData.Instance.AllPlayers
            .ToArray()
            .OrderBy(p => p.Disconnected)
            .ThenBy(p => p.IsDead)
            .ThenBy(p => !p.Role.IsImpostor)
            .ToArray();
    }

    private static StringBuilder BuildPlayerList()
    {
        var playersData = GetSortedPlayers();
        var stringBuilder = new StringBuilder();

        foreach (var playerData in playersData)
        {
            var playerLine = BuildPlayerLine(playerData);
            stringBuilder.AppendLine($"- {playerLine}\n");
            Logger_.Log(playerLine.Replace("\n", " "), "GameSummary");
        }

        return stringBuilder;
    }

    private static string BuildPlayerLine(NetworkedPlayerInfo playerData)
    {
        var name = $"<color={Utils.Color32ToHex(Palette.PlayerColors[playerData.DefaultOutfit.ColorId])}>{playerData.BetterData().RealName}</color>";
        var roleInfo = BuildRoleInfo(playerData);
        var deathReason = BuildDeathReason(playerData);

        return $"{name} {roleInfo} {deathReason}";
    }

    private static string BuildRoleInfo(NetworkedPlayerInfo playerData)
    {
        var themeColor = Utils.GetTeamHexColor(playerData.Role.TeamType);
        var theme = (string text) => $"<color={themeColor}>{text}</color>";

        // Dead players carry a ghost role (Crewmate/Impostor ghost, Guardian Angel), which hides their real
        // role. Use the role captured at death (DeadDisplayRole) so the post-game summary shows their TRUE
        // role -- a dead Judge reads "(Judge)", a dead Engineer "(Engineer)", etc.
        var displayRole = playerData.RoleType.IsGhostRole()
            ? (playerData.BetterData()?.RoleInfo?.DeadDisplayRole ?? playerData.RoleType)
            : playerData.RoleType;
        var roleName = theme(displayRole.GetRoleName());

        if (playerData.Role.IsImpostor)
        {
            var kills = playerData.BetterData().RoleInfo.Kills;
            return $"({roleName}) → {theme($"{Translator.GetString("Kills")}: {kills}")}";
        }

        var completedTasks = playerData.Tasks.WhereIl2Cpp(task => task.Complete).Count;
        var totalTasks = playerData.Tasks.Count;
        return $"({roleName}) → {theme($"{Translator.GetString("Tasks")}: {completedTasks}/{totalTasks}")}";
    }

    private static string BuildDeathReason(NetworkedPlayerInfo playerData)
    {
        if (playerData.Disconnected)
            return $"『<color=#838383><b>{Translator.GetString("DC")}</b></color>』";

        if (!playerData.IsDead)
            return $"『<color=#80ff00><b>{Translator.GetString("Alive")}</b></color>』";

        if (playerData.IsDead)
            return $"『<color=#ff0600><b>{Translator.GetString("Dead")}</b></color>』";

        return $"『<color=#838383><b>Unknown</b></color>』";
    }
}