using BetterAmongUs.Enums;
using System.Reflection;

namespace BetterAmongUs;

/// <summary>
/// Contains metadata and constants for the GreaterAmongUs mod.
/// GreaterAmongUs is a fork of BetterAmongUs by D1GQ, distributed under GPL v3.0.
/// </summary>
internal static class ModInfo
{
    /// <summary>
    /// Gets the release type of the current build.
    /// </summary>
    internal static readonly ReleaseTypes ReleaseBuildType = ReleaseTypes.Release;

    /// <summary>
    /// Gets the Git commit hash from assembly metadata.
    /// </summary>
    public static string CommitHash = GetAssemblyMetadata("CommitHash");

    /// <summary>
    /// Gets the build date from assembly metadata.
    /// </summary>
    public static string BuildDate = GetAssemblyMetadata("BuildDate");

    /// <summary>
    /// The beta number for beta releases.
    /// </summary>
    internal const string BETA_NUM = "0";

    /// <summary>
    /// The hotfix number for hotfix releases. Unused for v1.4 (IS_HOTFIX=false).
    /// </summary>
    internal const string HOTFIX_NUM = "0";

    /// <summary>
    /// Indicates whether this is a hotfix release. Clean re-version for v1.4.
    /// </summary>
    internal const bool IS_HOTFIX = false;

    /// <summary>
    /// The display name of this mod (the fork).
    /// </summary>
    internal const string PLUGIN_NAME = "GreaterAmongUs";

    /// <summary>
    /// The BepInEx plugin GUID. Unique to this fork so it does not collide
    /// with the upstream BetterAmongUs install if both are present.
    /// </summary>
    internal const string PLUGIN_GUID = "com.billyloomis-dev.greateramongus";

    /// <summary>
    /// The version of GreaterAmongUs.
    /// </summary>
    internal const string PLUGIN_VERSION = "1.4";

    /// <summary>
    /// The GitHub repository URL for this fork. In-game banners and the
    /// bug-report link point here so issues land in this fork's tracker.
    /// </summary>
    internal const string GITHUB = "https://github.com/BillyLoomis-dev/GreaterAmongUs";

    /// <summary>
    /// The upstream project this fork is derived from. Displayed alongside the
    /// version banner so the GPL chain of derivation stays visible to users.
    /// </summary>
    internal const string UPSTREAM_GITHUB = "https://github.com/D1GQ/BetterAmongUs";

    /// <summary>
    /// Human-readable name of the upstream project. Used for "based on X" credits.
    /// </summary>
    internal const string UPSTREAM_NAME = "BetterAmongUs by D1GQ";

    /// <summary>
    /// Retrieves metadata from the assembly attributes.
    /// </summary>
    /// <param name="key">The metadata key to retrieve.</param>
    /// <returns>The metadata value, or an empty string if not found.</returns>
    private static string GetAssemblyMetadata(string key)
    {
        var attribute = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key);

        return attribute?.Value ?? string.Empty;
    }

    /// <summary>
    /// Contains constants for Among Us.
    /// </summary>
    internal static class AmongUs
    {
        /// <summary>
        /// The process name of the Among Us executable.
        /// </summary>
        internal const string PROCESS_NAME = "Among Us.exe";
    }
}
