using AmongUs.GameOptions;
using BetterAmongUs.Modules;
using BetterAmongUs.MonoScripts.Extended;

namespace BetterAmongUs.Utilities;

/// <summary>
/// Provides helper methods for working with Among Us roles and their properties.
/// </summary>
internal static class RoleUtils
{
    /// <summary>
    /// Role type to role prefab lookup, filled from RoleManager.AllRoles on demand.
    /// </summary>
    /// <remarks>
    /// GreaterAmongUs: not a Lazy. A Lazy built before every role registered (seen with Judge)
    /// kept returning null for that role forever, and a Lazy built while RoleManager didn't exist
    /// yet cached the exception. A miss now simply re-reads the live role list.
    /// </remarks>
    private static readonly Dictionary<RoleTypes, RoleBehaviour> roleLookup = [];

    /// <summary>
    /// Gets the RoleBehaviour associated with a RoleTypes enum value.
    /// </summary>
    /// <param name="role">The role type to look up.</param>
    /// <returns>The RoleBehaviour if found, null otherwise.</returns>
    internal static RoleBehaviour? GetBehaviourPrefab(this RoleTypes role)
    {
        if (roleLookup.TryGetValue(role, out var behaviour) && behaviour != null)
        {
            return behaviour;
        }

        if (!RoleManager.InstanceExists)
            return null;

        var allRoles = RoleManager.Instance.AllRoles;
        if (allRoles == null)
            return null;

        foreach (var r in allRoles)
        {
            if (r != null)
            {
                roleLookup[r.Role] = r;
            }
        }

        return roleLookup.TryGetValue(role, out behaviour) ? behaviour : null;
    }

    /// <summary>
    /// Determines whether a role type belongs to the impostor team.
    /// </summary>
    /// <param name="role">The role type to check.</param>
    /// <returns>True if the role is part of the impostor team, false otherwise.</returns>
    internal static bool IsImpostorRole(this RoleTypes role)
    {
        var prefab = role.GetBehaviourPrefab();
        return prefab != null && prefab.TeamType is RoleTeamTypes.Impostor;
    }

    /// <summary>
    /// Determines whether the specified role is considered a ghost role.
    /// </summary>
    /// <param name="role">The role type to check.</param>
    /// <returns>true if the role is classified as a ghost role; otherwise, false.</returns>
    internal static bool IsGhostRole(this RoleTypes role) =>
        RoleManager.IsGhostRole(role);

    /// <summary>
    /// Gets the display name of a role type.
    /// </summary>
    /// <param name="role">The role type to get the name for.</param>
    /// <returns>The display name of the role, or "???" if not found.</returns>
    internal static string GetRoleName(this RoleTypes role)
    {
        if (role is RoleTypes.ImpostorGhost)
        {
            return RoleTypes.Impostor.GetBehaviourPrefab()?.NiceName ?? "???";
        }
        else if (role is RoleTypes.CrewmateGhost)
        {
            return RoleTypes.Crewmate.GetBehaviourPrefab()?.NiceName ?? "???";
        }

        return role.GetBehaviourPrefab()?.NiceName ?? "???";
    }

    /// <summary>
    /// Gets the hexadecimal color code associated with a role type.
    /// </summary>
    /// <param name="role">The role type to get the color for.</param>
    /// <returns>The hexadecimal color string, or empty string if not found.</returns>
    internal static string GetRoleHex(this RoleTypes role)
    {
        if (RoleColor.TryGetValue(role, out var color))
        {
            return color;
        }

        // Any role without an entry (a future role) uses the game's own role color.
        var prefab = role.GetBehaviourPrefab();
        if (prefab != null)
        {
            return prefab.NameColor.ColorToHex();
        }

        return "#ffffff";
    }

    /// <summary>
    /// Gets the role information for a specified player, optionally including task progress.
    /// </summary>
    /// <param name="player">The player to get role information for.</param>
    /// <param name="displayTask">Whether to display the player's task completion progress.</param>
    /// <returns>Returns role info or <see cref="string.Empty"/> if the player's role information cannot be displayed.</returns>
    internal static string GetRoleInfo(this PlayerControl player, bool displayTask)
    {
        return player.Data.GetRoleInfo(displayTask);
    }

    /// <summary>
    /// Gets the role information for a specified player, optionally including task progress.
    /// </summary>
    /// <param name="playerData">The player data to get role information for.</param>
    /// <param name="displayTask">Whether to display the player's task completion progress.</param>
    /// <returns>Returns role info or <see cref="string.Empty"/> if the player's role information cannot be displayed.</returns>
    /// <remarks>
    /// Who sees other players' roles (GreaterAmongUs):
    /// - living players: only themselves and their impostor teammates;
    /// - dead players and Guardian Angels: everyone (Guardian Angel can't talk to the living);
    /// - the Influencer (Spirit Guide): nobody else, because their picture messages reach living players.
    /// Dead viewers also see who judged who (<see cref="JudgeTracker"/>).
    /// </remarks>
    internal static string GetRoleInfo(this NetworkedPlayerInfo playerData, bool displayTask)
    {
        if (!GameState.IsInGamePlay)
        {
            return string.Empty;
        }

        if (playerData == null || PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null)
        {
            return string.Empty;
        }

        bool canSeeOthers = LocalCanSeeOtherRoles();

        if (playerData.PlayerId != PlayerControl.LocalPlayer.PlayerId && !playerData.IsImpostorTeammate() && !canSeeOthers)
        {
            return string.Empty;
        }

        var displayRole = playerData.GetDisplayRole();
        var teamColor = playerData.Role != null
            ? playerData.Role.TeamColor
            : (displayRole.IsImpostorRole() ? Colors.ImpostorRed : Colors.CrewmateBlue);
        string roleInfo = displayRole.GetRoleName().ToColor(teamColor);

        // A dead player who got a ghost role: show both, e.g. "Judge > Guardian Angel".
        if (playerData.RoleType is RoleTypes.GuardianAngel or RoleTypes.SpiritGuide && displayRole != playerData.RoleType)
        {
            roleInfo += $" <color=#cbcbcb>></color> {playerData.RoleType.GetRoleName().ToColor(playerData.RoleType.GetRoleHex())}";
        }

        if (!playerData.IsImpostorTeam() && playerData.Tasks.Count > 0)
        {
            if (displayTask)
            {
                int completedTasks = 0;
                foreach (var task in playerData.Tasks)
                {
                    if (task.Complete)
                        completedTasks++;
                }
                roleInfo += $" <color=#cbcbcb>({completedTasks}/{playerData.Tasks.Count})</color>";
            }
        }

        roleInfo += JudgeTracker.GetSuffix(playerData.PlayerId, canSeeOthers);

        return roleInfo;
    }

    /// <summary>
    /// Whether the local player may see other players' roles: dead (including Guardian Angel),
    /// but not the Influencer (Spirit Guide).
    /// </summary>
    internal static bool LocalCanSeeOtherRoles() => JudgeTracker.LocalCanSeeJudgements();

    /// <summary>
    /// Gets the role to show for a player: their real role, also after death
    /// (a dead Judge shows "Judge", not "Crewmate").
    /// </summary>
    /// <param name="playerData">The player data.</param>
    /// <returns>The role to display.</returns>
    internal static RoleTypes GetDisplayRole(this NetworkedPlayerInfo playerData)
    {
        var roleType = playerData.RoleType;
        if (!roleType.IsGhostRole())
        {
            return roleType;
        }

        var extended = playerData.ExtendedData();
        if (extended != null && extended.RoleInfo != null && extended.RoleInfo.HasDeadDisplayRole)
        {
            return extended.RoleInfo.DeadDisplayRole;
        }

        // Role at death unknown: fall back to the plain team role.
        return roleType is RoleTypes.ImpostorGhost ? RoleTypes.Impostor
            : roleType is RoleTypes.CrewmateGhost ? RoleTypes.Crewmate
            : roleType;
    }

    /// <summary>
    /// Dictionary mapping role types to their hexadecimal color codes.
    /// </summary>
    internal static Dictionary<RoleTypes, string> RoleColor => new()
    {
        { RoleTypes.CrewmateGhost, Colors.CrewmateBlue.ColorToHex() },
        { RoleTypes.GuardianAngel, "#8cffff" },
        { RoleTypes.SpiritGuide, "#ff0066" },
        { RoleTypes.Crewmate, Colors.CrewmateBlue.ColorToHex() },
        { RoleTypes.Scientist, "#00d9d9" },
        { RoleTypes.Engineer, "#8f8f8f" },
        { RoleTypes.Noisemaker, "#fc7c7c" },
        { RoleTypes.Tracker, "#59f002" },
        { RoleTypes.Detective, "#0027ff" },
        { RoleTypes.Judge, "#e3d12b" },
        { RoleTypes.ImpostorGhost, Colors.ImpostorRed.ColorToHex() },
        { RoleTypes.Impostor, Colors.ImpostorRed.ColorToHex() },
        { RoleTypes.Shapeshifter, "#f06102" },
        { RoleTypes.Phantom, "#d100b9" },
        { RoleTypes.Viper, "#367400" }
    };
}