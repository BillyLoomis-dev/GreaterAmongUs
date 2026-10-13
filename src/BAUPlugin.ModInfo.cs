using Semver;
using System.Reflection;

namespace BetterAmongUs;

internal partial class BAUPlugin
{
    /// <summary>
    /// Contains metadata and constants for the GreaterAmongUs mod (fork of BetterAmongUs).
    /// </summary>
    internal static class ModInfo
    {
        /// <summary>
        /// The base version number of the mod in MAJOR.MINOR.PATCH format.
        /// </summary>
        internal const string VERSION_NUMBER = "1.5.0";

        /// <summary>
        /// The beta release number. Increment this for each beta release.
        /// </summary>
        internal const string BETA_NUMBER = "0";

        /// <summary>
        /// Gets the full version string for the current build configuration.
        /// </summary>
#if RELEASE
        internal const string VERSION = VERSION_NUMBER;
#elif BETA
        internal const string VERSION = $"{VERSION_NUMBER}-beta-{BETA_NUMBER}";
#else
        internal const string VERSION = $"{VERSION_NUMBER}-dev";
#endif

        /// <summary>
        /// Gets the full version string with a v prefix for the current build configuration.
        /// </summary>
        internal const string VERSION_STRING = "v" + VERSION;

        /// <summary>
        /// Gets the parsed semantic version of the mod.
        /// </summary>
        internal static readonly SemVersion SemVersion = SemVersion.Parse(VERSION);

        /// <summary>
        /// Gets the Git commit hash from assembly metadata (empty when built without git).
        /// </summary>
        public static string CommitHash = GetAssemblyMetadata("CommitHash");

        /// <summary>
        /// Gets the build date from assembly metadata.
        /// </summary>
        public static string BuildDate = GetAssemblyMetadata("BuildDate");

        /// <summary>
        /// The name of the mod.
        /// </summary>
        internal const string PLUGIN_NAME = "GreaterAmongUs";

        /// <summary>
        /// The GUID (Globally Unique Identifier) of the mod.
        /// Different from BetterAmongUs so both can never be loaded as the same plugin.
        /// </summary>
        internal const string PLUGIN_GUID = "com.billyloomis-dev.greateramongus";

        /// <summary>
        /// The author shown in the console banner.
        /// </summary>
        internal const string AUTHOR = "BillyLoomis-dev";

        /// <summary>
        /// The upstream project this mod is based on (GPL v3.0).
        /// </summary>
        internal const string UPSTREAM_NAME = "BetterAmongUs by D1GQ";

        /// <summary>
        /// The upstream GitHub repository.
        /// </summary>
        internal const string UPSTREAM_GITHUB = "https://github.com/D1GQ/BetterAmongUs";

        /// <summary>
        /// Gets the list of supported Among Us versions.
        /// </summary>
        internal static string[] SupportedAmongUsVersions =
        [
            "2026.9.29"
        ];

        /// <summary>
        /// The GitHub repository URL for the mod.
        /// Hard-coded (not read from git) so a local clone's remote can never end up in the DLL.
        /// </summary>
        internal const string GITHUB = "https://github.com/BillyLoomis-dev/GreaterAmongUs";

        /// <summary>
        /// Indicator rather that BAU is running on Starlight for Android.
        /// </summary>
        internal static readonly bool Starlight = OperatingSystem.IsAndroid();

        /// <summary>
        /// Retrieves metadata from the assembly attributes.
        /// </summary>
        /// <param name="key">The metadata key to retrieve.</param>
        /// <returns>The metadata value, or an empty string if not found.</returns>
        private static string GetAssemblyMetadata(string key)
        {
            var attribute = Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == key);

            return attribute?.Value ?? string.Empty;
        }

        /// <summary>
        /// The assembly associated to this mod.
        /// </summary>
        internal static Assembly Assembly
        {
            get
            {
                if (field == null)
                {
                    field = Assembly.GetExecutingAssembly();
                }
                return field;
            }
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
}