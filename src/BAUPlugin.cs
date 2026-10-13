#pragma warning disable CS0162

using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BetterAmongUs.Attributes;
using BetterAmongUs.Data;
using BetterAmongUs.Data.Config;
using BetterAmongUs.Data.Json;
using BetterAmongUs.Managers;
using BetterAmongUs.Modules;
using BetterAmongUs.Modules.OptionItems;
using BetterAmongUs.Modules.Support;
using BetterAmongUs.Network;
using BetterAmongUs.Patches.Client;
using BetterAmongUs.Patches.Gameplay.UI.Settings;
using BetterAmongUs.Utilities;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BetterAmongUs;

[BepInPlugin(ModInfo.PLUGIN_GUID, ModInfo.PLUGIN_NAME, ModInfo.VERSION)]
[BepInProcess(ModInfo.AmongUs.PROCESS_NAME)]
internal partial class BAUPlugin : BasePlugin
{
    /// <summary>
    /// Gets the BAUPlugin instance.
    /// </summary>
    internal static BAUPlugin? Instance { get; private set; }

    /// <summary>
    /// Gets the Harmony instance used for patching.
    /// </summary>
    internal static Harmony Harmony { get; } = new Harmony(ModInfo.PLUGIN_GUID);

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
    internal static PlatformSpecificData PlatformData => global::Constants.GetPlatformData();

    /// <summary>
    /// Gets the list of all PlayerControl instances.
    /// </summary>
    internal static List<PlayerControl> AllPlayerControls = [];

    /// <summary>
    /// Gets the list of all alive PlayerControl instances.
    /// </summary>
    internal static List<PlayerControl> AllAlivePlayerControls => [.. AllPlayerControls.Where(pc => pc.IsAlive())];

    /// <summary>
    /// Gets all DeadBody objects in the scene.
    /// </summary>
    internal static DeadBody[] AllDeadBodys => [.. UnityEngine.Object.FindObjectsOfType<DeadBody>()];

    /// <summary>
    /// Gets all Vent objects in the scene.
    /// </summary>
    internal static Vent[] AllVents => UnityEngine.Object.FindObjectsOfType<Vent>();

    /// <summary>
    /// Gets the BAU logger instance.
    /// </summary>
    internal static BAULogger Logger { get; private set; } = null!;

    /// <summary>
    /// Gets the BepInEx logger instance.
    /// </summary>
    private static ManualLogSource? _manualLogSource;

    public override void Load()
    {
        Instance = this;

        _manualLogSource = BepInEx.Logging.Logger.CreateLogSource(ModInfo.PLUGIN_GUID);
        Logger = new BAULogger(_manualLogSource);

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

            if (!ModInfo.Starlight)
            {
                SetupConsole();
            }

            RegisterInIl2Cpp.Initialize();
            IL2CPPChainloader.Instance.Finished += OnChainloaderFinished;
        }
        catch (Exception ex)
        {
            Logger.Error(ex);
        }
    }

    /// <summary>
    /// Runs when the BepInEx Chainloader has finished.
    /// </summary>
    private void OnChainloaderFinished()
    {
        if (BAUModdedSupportEvents.OnBAULoadEvent.InvokeAll(this).Any(b => b == false))
            return;

        BAUModdedSupportFlags.Initialize();
        GithubAPI.Connect();
        BAUConfigs.LoadConfigs();
        BetterDataManager.Initialize();
        AudioOverrideManager.Initialize();
        Translator.Initialize();
        Harmony.PatchAll();
        GameSettingsPatch.SetupSettings(true);
        BAUModdedSupportEvents.OnBAUOptionsLoadedEvent.InvokeAll([.. OptionItem.AllOptions.Cast<object>()]);
        AutoRegisterAttribute.Initialize();
        OutfitData.Initialize();
        SceneManager.add_sceneLoaded((Action<Scene, LoadSceneMode>)OnSceneLoaded);

        Logger.Log($"{ModInfo.PLUGIN_NAME} successfully loaded!");

        string SupportedVersions = string.Join(" ", ModInfo.SupportedAmongUsVersions);
        Logger.Log($"{ModInfo.PLUGIN_NAME} {ModInfo.VERSION_STRING}-{ModInfo.BuildDate} - [{AppVersion} --> {SupportedVersions}] {Utils.GetPlatformName(PlatformData.Platform)}");
        // Plain ASCII dash (-), not an em dash: the Windows console is CP437/CP1252.
        Logger.Log($"based on {ModInfo.UPSTREAM_NAME} ({ModInfo.UPSTREAM_GITHUB}) - GPL v3.0");
    }

    /// <summary>
    /// Unloads the mod to switch to vanilla.
    /// </summary>
    internal void UnloadBAU()
    {
        ConsoleManager.DetachConsole();
        BetterNotificationManager.Detach();
        ClientPatch.Unpatch();
        Harmony.UnpatchAll();
        ModManager.Instance.ModStamp.gameObject.SetActive(false);
        SceneChanger.ChangeScene("MainMenu");
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (AmongUsClient.Instance != null)
        {
            if (scene.name == AmongUsClient.Instance.MainMenuScene)
            {
                BAUModdedSupportFlags.ClearTempFlags();
            }
        }
    }

    /// <summary>
    /// Sets up the console window for logging.
    /// </summary>
    private static void SetupConsole()
    {
        Encryptor.Initialize();
        ConsoleManager.CreateConsole();
        ConsoleManager.ConfigPreventClose.Value = true;
        if (ConsoleManager.ConfigConsoleEnabled.Value) ConsoleManager.DetachConsole();
        ConsoleManager.ConfigConsoleEnabled.Value = false;
        ConsoleManager.SetConsoleTitle($"Among Us - {ModInfo.PLUGIN_NAME} Console");
        var customLogListener = new CustomLogListener(Logger);
        BepInEx.Logging.Logger.Listeners.Add(customLogListener);
        ConsoleManager.SetConsoleColor(ConsoleColor.Green);
        WriteBanner();
    }

    /// <summary>
    /// Writes the GreaterAmongUs banner (figlet art + credit lines) to the console.
    /// Every line is padded to the same width so the right border never misaligns.
    /// </summary>
    private static void WriteBanner()
    {
        string[] artLines =
        [
            @"   ____               _              _                            _   _    ",
            @"  / ___|_ __ ___  __ _| |_ ___ _ __  / \   _ __ ___   ___  _ __  | | | |___ ",
            @" | |  _| '__/ _ \/ _` | __/ _ \ '__|/ _ \ | '_ ` _ \ / _ \| '_ \ | | | / __|",
            @" | |_| | | |  __/ (_| | ||  __/ |  / ___ \| | | | | | (_) | | | || |_| \__ \",
            @"  \____|_|  \___|\__,_|\__\___|_| /_/   \_\_| |_| |_|\___/|_| |_| \___/|___/",
        ];

        int contentWidth = Math.Max(artLines.Max(l => l.Length) + 2, 80);
        string h = new('-', contentWidth);
        string blank = new(' ', contentWidth);

        string Pad(string s) => s.Length >= contentWidth ? s[..contentWidth] : s.PadRight(contentWidth);
        string Center(string s)
        {
            if (s.Length >= contentWidth) return s[..contentWidth];
            int leftPad = (contentWidth - s.Length) / 2;
            return s.PadLeft(s.Length + leftPad).PadRight(contentWidth);
        }

        string url = ModInfo.GITHUB.Replace("https://", "");
        string urlUp = ModInfo.UPSTREAM_GITHUB.Replace("https://", "");
        string credit = $"{ModInfo.PLUGIN_NAME} {ModInfo.VERSION_STRING} by {ModInfo.AUTHOR}";
        string basedOn = $"based on {ModInfo.UPSTREAM_NAME} (GPL v3.0)";

        var stream = ConsoleManager.ConsoleStream;
        stream.WriteLine($".{h}.");
        stream.WriteLine($"|{blank}|");
        foreach (var line in artLines)
            stream.WriteLine($"|{Pad(line)}|");
        stream.WriteLine($"|{blank}|");
        stream.WriteLine($"|{Center(credit)}|");
        stream.WriteLine($"|{Center(url)}|");
        stream.WriteLine($"|{blank}|");
        stream.WriteLine($"|{Center(basedOn)}|");
        stream.WriteLine($"|{Center(urlUp)}|");
        stream.WriteLine($"|{blank}|");
        stream.WriteLine($"'{h}'");
    }
}