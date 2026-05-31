using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BetterAmongUs.Attributes;
using BetterAmongUs.Data;
using BetterAmongUs.Data.Json;
using BetterAmongUs.Enums;
using BetterAmongUs.Helpers;
using BetterAmongUs.Modules;
using BetterAmongUs.Modules.OptionItems;
using BetterAmongUs.Modules.Support;
using BetterAmongUs.Network;
using BetterAmongUs.Patches.Client;
using BetterAmongUs.Patches.Gameplay.UI.Settings;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using System.Reflection;
using UnityEngine;

namespace BetterAmongUs;

[BepInPlugin(ModInfo.PLUGIN_GUID, ModInfo.PLUGIN_NAME, ModInfo.PLUGIN_VERSION)]
[BepInProcess(ModInfo.AmongUs.PROCESS_NAME)]
internal class BAUPlugin : BasePlugin
{
    /// <summary>
    /// Gets the formatted version text for display.
    /// </summary>
    /// <param name="newLine">Whether to use newline separation for additional info.</param>
    /// <returns>Formatted version string.</returns>
    internal static string GetVersionText(bool newLine = false)
    {
        string text = string.Empty;

        string newLineText = newLine ? "\n" : " ";

        switch (ModInfo.ReleaseBuildType)
        {
            case ReleaseTypes.Release:
                text = $"v{BetterAmongUsVersion}";
                break;
            case ReleaseTypes.Beta:
                text = $"v{BetterAmongUsVersion}{newLineText}Beta {ModInfo.BETA_NUM}";
                break;
            case ReleaseTypes.Dev:
                text = $"v{BetterAmongUsVersion}{newLineText}Dev {ModInfo.CommitHash}-{ModInfo.BuildDate}";
                break;
            default:
                break;
        }

        if (ModInfo.IS_HOTFIX)
            text += $"{newLineText}Hotfix {ModInfo.HOTFIX_NUM}";

        return text;
    }

    /// <summary>
    /// Gets the Harmony instance used for patching.
    /// </summary>
    internal static Harmony Harmony { get; } = new Harmony(ModInfo.PLUGIN_GUID);

    /// <summary>
    /// Gets the BetterAmongUs version string.
    /// </summary>
    internal static string BetterAmongUsVersion => ModInfo.PLUGIN_VERSION;

    /// <summary>
    /// Gets the application version string.
    /// </summary>
    internal static string AppVersion => Application.version;

    /// <summary>
    /// Gets the Among Us version string from reference data.
    /// </summary>
    internal static string AmongUsVersion => ReferenceDataManager.Instance.Refdata.userFacingVersion;

    /// <summary>
    /// Gets platform-specific data.
    /// </summary>
    internal static PlatformSpecificData PlatformData => Constants.GetPlatformData();

    /// <summary>
    /// Gets the list of supported Among Us versions.
    /// </summary>
    internal static string[] SupportedAmongUsVersions =
    [
        "2026.3.31",
        "2025.11.18",
    ];

    /// <summary>
    /// Gets the list of all PlayerControl instances.
    /// </summary>
    internal static List<PlayerControl> AllPlayerControls = [];

    /// <summary>
    /// Gets the list of all alive PlayerControl instances.
    /// </summary>
    internal static List<PlayerControl> AllAlivePlayerControls => AllPlayerControls.Where(pc => pc.IsAlive()).ToList();

    /// <summary>
    /// Gets all DeadBody objects in the scene.
    /// </summary>
    internal static DeadBody[] AllDeadBodys => UnityEngine.Object.FindObjectsOfType<DeadBody>().ToArray();

    /// <summary>
    /// Gets all Vent objects in the scene.
    /// </summary>
    internal static Vent[] AllVents => UnityEngine.Object.FindObjectsOfType<Vent>();

    /// <summary>
    /// Gets the BepInEx logger instance.
    /// </summary>
    internal static ManualLogSource? Logger;

    public override void Load()
    {
        try
        {
            foreach (var listener in BepInEx.Logging.Logger.Listeners)
            {
                if (listener.GetType().Name.ToLower().Contains("Unity"))
                {
                    BepInEx.Logging.Logger.Listeners.Remove(listener);
                    break;
                }
            }

            SetupConsole();
            RegisterAllMonoBehavioursInAssembly();
            IL2CPPChainloader.Instance.Finished += OnChainloaderFinished;
        }
        catch (Exception ex)
        {
            Logger_.Error(ex);
        }
    }

    /// <summary>
    /// Runs when the BepInEx Chainloader has finished.
    /// </summary>
    private void OnChainloaderFinished()
    {
        if (!BAUModdedSupportEvents.InvokeAll_OnBAULoad(this)) return;

        BAUModdedSupportFlags.Initialize();
        GithubAPI.Connect();
        LoadOptions();
        BetterDataManager.Initialize();
        Translator.Initialize();
        Harmony.PatchAll();
        GameSettingsPatch.SetupSettings(true);
        BAUModdedSupportEvents.InvokeAll_OnBAUOptionsLoaded([.. OptionItem.AllOptions.Cast<object>()]);
        InstanceAttribute.RegisterAll();
        OutfitData.Initialize();

        if (File.Exists(Path.Combine(BetterDataManager.filePathFolder, "greater-log.txt")))
            File.WriteAllText(Path.Combine(BetterDataManager.filePathFolder, "greater-previous-log.txt"), File.ReadAllText(Path.Combine(BetterDataManager.filePathFolder, "greater-log.txt")));

        File.WriteAllText(Path.Combine(BetterDataManager.filePathFolder, "greater-log.txt"), "");
        Logger_.Log($"{ModInfo.PLUGIN_NAME} successfully loaded!");

        string SupportedVersions = string.Join(" ", SupportedAmongUsVersions);
        Logger_.Log($"{ModInfo.PLUGIN_NAME} {BetterAmongUsVersion}-{ModInfo.BuildDate} - [{AppVersion} --> {SupportedVersions}] {Utils.GetPlatformName(PlatformData.Platform)}");
        // Plain ASCII dash (-) not em dash (—). The Windows console BAU spawns
        // is CP437/CP1252, which renders Unicode em dash as garbage like "ΓÇö".
        Logger_.Log($"based on {ModInfo.UPSTREAM_NAME} ({ModInfo.UPSTREAM_GITHUB}) - GPL v3.0");
    }

    /// <summary>
    /// Sets up the console window for logging.
    /// </summary>
    private static void SetupConsole()
    {
        ConsoleManager.CreateConsole();
        ConsoleManager.ConfigPreventClose.Value = true;
        if (ConsoleManager.ConfigConsoleEnabled.Value) ConsoleManager.DetachConsole();
        ConsoleManager.ConfigConsoleEnabled.Value = false;
        ConsoleManager.SetConsoleTitle($"Among Us - {ModInfo.PLUGIN_NAME} Console");
        Logger = BepInEx.Logging.Logger.CreateLogSource(ModInfo.PLUGIN_GUID);
        var customLogListener = new CustomLogListener();
        BepInEx.Logging.Logger.Listeners.Add(customLogListener);
        ConsoleManager.SetConsoleColor(ConsoleColor.Green);
        // Console banner — figlet-style ASCII art for "GreaterAmongUs" plus
        // centered credit lines underneath. Each line is padded to the same
        // width so the right border never misaligns regardless of which line
        // is longest.
        {
            // "GreaterAmongUs" in Standard figlet font (5 visible rows).
            string[] artLines = new[]
            {
                @"   ____               _              _                            _   _    ",
                @"  / ___|_ __ ___  __ _| |_ ___ _ __  / \   _ __ ___   ___  _ __  | | | |___ ",
                @" | |  _| '__/ _ \/ _` | __/ _ \ '__|/ _ \ | '_ ` _ \ / _ \| '_ \ | | | / __|",
                @" | |_| | | |  __/ (_| | ||  __/ |  / ___ \| | | | | | (_) | | | || |_| \__ \",
                @"  \____|_|  \___|\__,_|\__\___|_| /_/   \_\_| |_| |_|\___/|_| |_| \___/|___/",
            };

            int artMax = artLines.Max(l => l.Length);
            int contentWidth = Math.Max(artMax + 2, 80);
            string h = new string('-', contentWidth);
            string blank = new string(' ', contentWidth);

            string Pad(string s) =>
                s.Length >= contentWidth ? s.Substring(0, contentWidth) : s.PadRight(contentWidth);

            string Center(string s)
            {
                if (s.Length >= contentWidth) return s.Substring(0, contentWidth);
                int leftPad = (contentWidth - s.Length) / 2;
                return s.PadLeft(s.Length + leftPad).PadRight(contentWidth);
            }

            // Strip the "https://" scheme from URLs for cleaner console output.
            string url     = ModInfo.GITHUB.Replace("https://", "");
            string urlUp   = ModInfo.UPSTREAM_GITHUB.Replace("https://", "");
            string credit  = $"{ModInfo.PLUGIN_NAME} v{ModInfo.PLUGIN_VERSION} by BillyLoomis-dev";
            string basedOn = $"based on {ModInfo.UPSTREAM_NAME} (GPL v3.0)";

            ConsoleManager.ConsoleStream.WriteLine($".{h}.");
            ConsoleManager.ConsoleStream.WriteLine($"|{blank}|");
            foreach (var line in artLines)
                ConsoleManager.ConsoleStream.WriteLine($"|{Pad(line)}|");
            ConsoleManager.ConsoleStream.WriteLine($"|{blank}|");
            ConsoleManager.ConsoleStream.WriteLine($"|{Center(credit)}|");
            ConsoleManager.ConsoleStream.WriteLine($"|{Center(url)}|");
            ConsoleManager.ConsoleStream.WriteLine($"|{blank}|");
            ConsoleManager.ConsoleStream.WriteLine($"|{Center(basedOn)}|");
            ConsoleManager.ConsoleStream.WriteLine($"|{Center(urlUp)}|");
            ConsoleManager.ConsoleStream.WriteLine($"|{blank}|");
            ConsoleManager.ConsoleStream.WriteLine($"'{h}'");
        }
    }

    /// <summary>
    /// Registers all MonoBehaviour classes for IL2CPP injection.
    /// </summary>
    private static void RegisterAllMonoBehavioursInAssembly()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var monoBehaviourTypes = assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(MonoBehaviour)) && !type.IsAbstract)
            .OrderBy(type => type.Name);

        foreach (var type in monoBehaviourTypes)
        {
            try
            {
                ClassInjector.RegisterTypeInIl2Cpp(type);
            }
            catch (Exception ex)
            {
                Logger_.Error($"Failed to register MonoBehaviour: {type.FullName}\n{ex}");
            }
        }
    }

    /// <summary>
    /// Gets or sets the configuration entry for private only lobby setting.
    /// </summary>
    internal static ConfigEntry<bool>? PrivateOnlyLobby { get; private set; }

    /// <summary>
    /// Gets or sets the configuration entry for anti-cheat setting.
    /// </summary>
    internal static ConfigEntry<bool>? AntiCheat { get; private set; }

    /// <summary>
    /// Gets or sets the configuration entry for sending Better RPC setting.
    /// </summary>
    internal static ConfigEntry<bool>? SendBetterRpc { get; private set; }

    /// <summary>
    /// Gets or sets the configuration entry for better notifications setting.
    /// </summary>
    internal static ConfigEntry<bool>? BetterNotifications { get; private set; }

    /// <summary>
    /// Gets or sets the configuration entry for force own language setting.
    /// </summary>
    internal static ConfigEntry<bool>? ForceOwnLanguage { get; private set; }

    /// <summary>
    /// Gets or sets the configuration entry for chat dark mode setting.
    /// </summary>
    internal static ConfigEntry<bool>? ChatDarkMode { get; private set; }

    /// <summary>
    /// Gets or sets the configuration entry for chat in gameplay setting.
    /// </summary>
    internal static ConfigEntry<bool>? ChatInGameplay { get; private set; }

    /// <summary>
    /// Gets or sets the configuration entry for lobby player info setting.
    /// </summary>
    internal static ConfigEntry<bool>? LobbyPlayerInfo { get; private set; }

    /// <summary>
    /// Gets or sets the configuration entry for disable lobby theme setting.
    /// </summary>
    internal static ConfigEntry<bool>? DisableLobbyTheme { get; private set; }

    /// <summary>
    /// Gets or sets the configuration entry for unlock FPS setting.
    /// </summary>
    internal static ConfigEntry<bool>? UnlockFPS { get; private set; }

    /// <summary>
    /// Gets or sets the configuration entry for show FPS setting.
    /// </summary>
    internal static ConfigEntry<bool>? ShowFPS { get; private set; }

    /// <summary>
    /// Gets or sets the configuration entry for command prefix setting.
    /// </summary>
    internal static ConfigEntry<string>? CommandPrefix { get; set; }

    /// <summary>
    /// Gets or sets the configuration entry for favorite color setting.
    /// </summary>
    internal static ConfigEntry<int>? FavoriteColor { get; set; }

    /// <summary>
    /// Gets or sets the configuration entry for the settings preset.
    /// </summary>
    internal static ConfigEntry<int>? SettingsPreset { get; private set; }

    /// <summary>
    /// Loads configuration options from BepInEx config file.
    /// </summary>
    private void LoadOptions()
    {
        PrivateOnlyLobby = Config.Bind("Mod", "PrivateOnlyLobby", false);
        AntiCheat = Config.Bind("Greater Options", "AntiCheat", true);
        SendBetterRpc = Config.Bind("Greater Options", "SendBetterRpc", false);
        BetterNotifications = Config.Bind("Greater Options", "BetterNotifications", true);
        ForceOwnLanguage = Config.Bind("Greater Options", "ForceOwnLanguage", false);
        ChatDarkMode = Config.Bind("Greater Options", "ChatDarkMode", true);
        ChatInGameplay = Config.Bind("Greater Options", "ChatInGameplay", false);
        LobbyPlayerInfo = Config.Bind("Greater Options", "LobbyPlayerInfo", true);
        DisableLobbyTheme = Config.Bind("Greater Options", "DisableLobbyTheme", true);
        UnlockFPS = Config.Bind("Greater Options", "UnlockFPS", false);
        ShowFPS = Config.Bind("Greater Options", "ShowFPS", false);
        CommandPrefix = Config.Bind("Client Options", "CommandPrefix", "/");
        FavoriteColor = Config.Bind("Mod", "FavoriteColor", -1);
        SettingsPreset = Config.Bind("Mod", "SettingsPreset", 0);

        BAUModdedSupportEvents.InvokeAll_OnBAUConfigEntriesLoaded([
            PrivateOnlyLobby, AntiCheat, SendBetterRpc,
            BetterNotifications, ForceOwnLanguage, ChatDarkMode,
            ChatInGameplay, LobbyPlayerInfo, DisableLobbyTheme,
            UnlockFPS, ShowFPS, CommandPrefix,
            FavoriteColor, SettingsPreset
        ]);

        OptionsMenuBehaviourPatch.UpdateFrameRate();
    }

    /// <summary>
    /// Gets the persistent data path for Among Us.
    /// </summary>
    /// <returns>The persistent data path string.</returns>
    internal static string GetDataPathToAmongUs() => Application.persistentDataPath;

    /// <summary>
    /// Gets the game installation path for Among Us.
    /// </summary>
    /// <returns>The game installation path string.</returns>
    internal static string GetGamePathToAmongUs() => Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath;
}