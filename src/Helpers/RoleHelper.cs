using AmongUs.GameOptions;

namespace BetterAmongUs.Helpers;

/// <summary>
/// Provides helper methods for working with Among Us roles and their properties.
/// </summary>
internal static class RoleHelper
{
    /// <summary>
    /// Provides a lazily initialized lookup dictionary that maps each role type to its corresponding role behavior.
    /// </summary>
    private static readonly Lazy<Dictionary<RoleTypes, RoleBehaviour>> roleLookup =
        new(() =>
        {
            var dict = new Dictionary<RoleTypes, RoleBehaviour>();
            foreach (var r in RoleManager.Instance.AllRoles)
            {
                dict[r.Role] = r;
            }
            return dict;
        });

    /// <summary>
    /// Gets the RoleBehaviour associated with a RoleTypes enum value.
    /// </summary>
    /// <param name="role">The role type to look up.</param>
    /// <returns>The RoleBehaviour if found, null otherwise.</returns>
    internal static RoleBehaviour? GetBehaviourPrefab(this RoleTypes role)
    {
        var lookup = roleLookup.Value;
        if (lookup.TryGetValue(role, out var behaviour))
        {
            return behaviour;
        }

        // Cache miss: the lazy lookup can be built before every role registers (e.g. Judge on the
        // 2026.8.18 update). Re-query the live role list once and cache it, so we never return null for
        // a role that actually exists -- otherwise callers like IsImpostorTeam NPE on it every frame.
        var manager = RoleManager.Instance;
        if (manager != null && manager.AllRoles != null)
        {
            foreach (var r in manager.AllRoles)
            {
                if (r != null && r.Role == role)
                {
                    lookup[role] = r;
                    return r;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether a role type belongs to the impostor team.
    /// </summary>
    /// <param name="role">The role type to check.</param>
    /// <returns>True if the role is part of the impostor team, false otherwise.</returns>
    internal static bool IsImpostorRole(this RoleTypes role) =>
        role.GetBehaviourPrefab()?.TeamType is RoleTeamTypes.Impostor;

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

        // Durable fallback: any role not explicitly mapped (e.g. Judge, and any future role) uses the
        // game's own role color, so it renders correctly without needing a hardcoded entry per update.
        var prefab = role.GetBehaviourPrefab();
        if (prefab != null)
        {
            return prefab.NameColor.ColorToHex();
        }

        return string.Empty;
    }

    /// <summary>
    /// Dictionary mapping role types to their hexadecimal color codes.
    /// </summary>
    internal static Dictionary<RoleTypes, string> RoleColor => new()
    {
        { RoleTypes.CrewmateGhost, Colors.CrewmateBlue.ColorToHex() },
        { RoleTypes.GuardianAngel, "#8cffff" },
        { RoleTypes.Crewmate, Colors.CrewmateBlue.ColorToHex() },
        { RoleTypes.Scientist, "#00d9d9" },
        { RoleTypes.Engineer, "#8f8f8f" },
        { RoleTypes.Noisemaker, "#fc7c7c" },
        { RoleTypes.Tracker, "#59f002" },
        { RoleTypes.Detective, "#0027FF" },
        { RoleTypes.ImpostorGhost, Colors.ImpostorRed.ColorToHex() },
        { RoleTypes.Impostor, Colors.ImpostorRed.ColorToHex() },
        { RoleTypes.Shapeshifter, "#f06102" },
        { RoleTypes.Phantom, "#d100b9" },
        { RoleTypes.Viper, "#367400" }
    };
}