using BetterAmongUs.Modules.Support;
using BetterAmongUs.Patches.Client;

namespace BetterAmongUs.Data.Config;

/// <summary>
/// Manages configuration entries for the Better Among Us.
/// </summary>
internal static class BAUConfigs
{
    /// <summary>
    /// Gets the configuration entry for anti-cheat setting.
    /// </summary>
    internal static BAUConfigEntry<bool> AntiCheat { get; } = new("Greater Options", "AntiCheat", true);

    /// <summary>
    /// Gets the configuration entry for sending Better RPC setting.
    /// </summary>
    internal static BAUConfigEntry<bool> SendBetterRpc { get; } = new("Greater Options", "SendBetterRpc", false);

    /// <summary>
    /// Gets the configuration entry for better notifications setting.
    /// </summary>
    internal static BAUConfigEntry<bool> BetterNotifications { get; } = new("Greater Options", "BetterNotifications", true);

    /// <summary>
    /// Gets the configuration entry for force own language setting.
    /// </summary>
    internal static BAUConfigEntry<bool> ForceOwnLanguage { get; } = new("Greater Options", "ForceOwnLanguage", false);

    /// <summary>
    /// Gets the configuration entry for chat dark mode setting.
    /// </summary>
    internal static BAUConfigEntry<bool> ChatDarkMode { get; } = new("Greater Options", "ChatDarkMode", true);

    /// <summary>
    /// Gets the configuration entry for chat in gameplay setting.
    /// </summary>
    internal static BAUConfigEntry<bool> ChatInGameplay { get; } = new("Greater Options", "ChatInGameplay", false);

    /// <summary>
    /// Gets the configuration entry for lobby player info setting.
    /// </summary>
    internal static BAUConfigEntry<bool> LobbyPlayerInfo { get; } = new("Greater Options", "LobbyPlayerInfo", true);

    /// <summary>
    /// Gets the configuration entry for disable lobby theme setting.
    /// </summary>
    internal static BAUConfigEntry<bool> DisableLobbyTheme { get; } = new("Greater Options", "DisableLobbyTheme", true);

    /// <summary>
    /// Gets the configuration entry for unlock FPS setting.
    /// </summary>
    internal static BAUConfigEntry<bool> UnlockFPS { get; } = new("Greater Options", "UnlockFPS", false);

    /// <summary>
    /// Gets the configuration entry for show FPS setting.
    /// </summary>
    internal static BAUConfigEntry<bool> ShowFPS { get; } = new("Greater Options", "ShowFPS", false);

    /// <summary>
    /// Gets the configuration entry for minimap icons setting.
    /// </summary>
    internal static BAUConfigEntry<bool> MinimapIcons { get; } = new("Greater Options", "MinimapIcons", true);

    /// <summary>
    /// Gets the configuration entry for new minimap colors setting.
    /// </summary>
    internal static BAUConfigEntry<bool> BetterMinimapColors { get; } = new("Greater Options", "BetterMinimapColors", true);

    /// <summary>
    /// Gets the configuration entry for vent color groups setting.
    /// </summary>
    internal static BAUConfigEntry<bool> VentColorGroups { get; } = new("Greater Options", "VentColorGroups", true);

    /// <summary>
    /// Gets the configuration entry for better color blind text setting.
    /// </summary>
    internal static BAUConfigEntry<bool> BetterColorblindText { get; } = new("Greater Options", "BetterColorblindText", true);

    /// <summary>
    /// Gets the configuration entry for compress settings file setting.
    /// </summary>
    internal static BAUConfigEntry<bool> CompressSettingFiles { get; } = new("Greater Options", "CompressSettingFiles", false);

    /// <summary>
    /// Gets the configuration entry for command prefix setting.
    /// </summary>
    internal static BAUConfigEntry<string> CommandPrefix { get; } = new("Mod", "CommandPrefix", "/");

    /// <summary>
    /// Gets the configuration entry for favorite color setting.
    /// </summary>
    internal static BAUConfigEntry<int> FavoriteColor { get; } = new("Mod", "FavoriteColor", -1);

    /// <summary>
    /// Gets the configuration entry for the settings preset.
    /// </summary>
    internal static BAUConfigEntry<int> SettingsPreset { get; } = new("Mod", "SettingsPreset", 0);

    /// <summary>
    /// Loads configuration options from BepInEx config file.
    /// </summary>
    internal static void LoadConfigs()
    {
        BAUModdedSupportEvents.OnBAUConfigEntriesLoadedEvent.InvokeAll([
            AntiCheat, SendBetterRpc, BetterNotifications,
            ForceOwnLanguage, ChatDarkMode, ChatInGameplay, LobbyPlayerInfo,
            DisableLobbyTheme, UnlockFPS, ShowFPS,
            MinimapIcons, VentColorGroups, CommandPrefix,
            FavoriteColor, SettingsPreset
        ]);

        OptionsMenuBehaviourPatch.UpdateFrameRate();
    }
}