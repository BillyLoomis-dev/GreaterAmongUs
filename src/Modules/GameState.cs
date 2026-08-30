using AmongUs.GameOptions;

namespace BetterAmongUs.Modules;

/// <summary>
/// Provides static properties to check various game states and conditions.
/// </summary>
internal static class GameState
{
    /**********Check Game Status***********/
    /// <summary>
    /// Gets whether any players exist in the game.
    /// </summary>
    internal static bool InGame => BAUPlugin.AllPlayerControls.Any();

    /// <summary>
    /// Gets whether the current game mode is Normal or NormalFools.
    /// </summary>
    internal static bool IsNormalGame => GameOptionsManager.Instance.CurrentGameOptions.GameMode is GameModes.Normal or GameModes.NormalFools;

    /// <summary>
    /// Gets whether the current game mode is HideNSeek or SeekFools.
    /// </summary>
    internal static bool IsHideNSeek => GameOptionsManager.Instance != null && GameOptionsManager.Instance.CurrentGameOptions.GameMode is GameModes.HideNSeek or GameModes.SeekFools;

    /// <summary>
    /// Gets whether the Skeld map is currently active.
    /// </summary>
    internal static bool SkeldIsActive => (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId == MapNames.Skeld;

    /// <summary>
    /// Gets whether the MiraHQ map is currently active.
    /// </summary>
    internal static bool MiraHQIsActive => (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId == MapNames.MiraHQ;

    /// <summary>
    /// Gets whether the Polus map is currently active.
    /// </summary>
    internal static bool PolusIsActive => (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId == MapNames.Polus;

    /// <summary>
    /// Gets whether the Dleks map is currently active.
    /// </summary>
    internal static bool DleksIsActive => (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId == MapNames.Dleks;

    /// <summary>
    /// Gets whether the Airship map is currently active.
    /// </summary>
    internal static bool AirshipIsActive => (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId == MapNames.Airship;

    /// <summary>
    /// Gets whether the Fungle map is currently active.
    /// </summary>
    internal static bool FungleIsActive => (MapNames)GameOptionsManager.Instance.CurrentGameOptions.MapId == MapNames.Fungle;

    /// <summary>
    /// Gets the ID of the currently active map.
    /// </summary>
    internal static byte GetActiveMapId => GameOptionsManager.Instance.CurrentGameOptions.MapId;

    /// <summary>
    /// Checks whether a specific system type is currently active on the map.
    /// </summary>
    /// <param name="type">The system type to check.</param>
    /// <returns>True if the system is active, false otherwise.</returns>
    internal static bool IsSystemActive(SystemTypes type)
    {
        if (IsHideNSeek || !ShipStatus.Instance.Systems.TryGetValue(type, out var system))
        {
            return false;
        }

        int mapId = GetActiveMapId;

        return type switch
        {
            SystemTypes.Electrical when mapId != 5 => !system.Cast<SwitchSystem>()?.IsActive == false,
            SystemTypes.Reactor when mapId != 2 => system.Cast<ReactorSystemType>()?.IsActive ?? false,
            SystemTypes.Laboratory when mapId == 2 => system.Cast<ReactorSystemType>()?.IsActive ?? false,
            SystemTypes.LifeSupp when mapId is 0 or 3 => system.Cast<LifeSuppSystemType>()?.IsActive ?? false,
            SystemTypes.HeliSabotage when mapId == 4 => system.Cast<HeliSabotageSystem>()?.IsActive ?? false,
            SystemTypes.Comms when mapId is 1 or 5 => system.Cast<HqHudSystemType>()?.IsActive ?? false,
            SystemTypes.Comms => system.Cast<HudOverrideSystemType>()?.IsActive ?? false,
            SystemTypes.MushroomMixupSabotage when mapId == 5 => system.Cast<MushroomMixupSabotageSystem>()?.IsActive ?? false,
            _ => false
        };
    }

    /// <summary>
    /// Checks if any critical (death-causing) sabotage is currently active.
    /// </summary>
    /// <returns>True if a critical sabotage is active, false otherwise.</returns>
    internal static bool IsCriticalSabotageActive()
    {
        var deathSabotages = new[]
        {
        SystemTypes.Reactor,
        SystemTypes.Laboratory,
        SystemTypes.LifeSupp,
        SystemTypes.HeliSabotage,
    };

        return deathSabotages.Any(IsSystemActive);
    }

    /// <summary>
    /// Checks if any non-critical sabotage is currently active.
    /// </summary>
    /// <returns>True if a non-critical sabotage is active, false otherwise.</returns>
    internal static bool IsNoneCriticalSabotageActive()
    {
        var noneDeathSabotages = new[]
        {
        SystemTypes.Electrical,
        SystemTypes.Comms,
        SystemTypes.MushroomMixupSabotage
    };

        return noneDeathSabotages.Any(IsSystemActive);
    }

    /// <summary>
    /// Checks if any sabotage (critical or non-critical) is currently active.
    /// </summary>
    /// <returns>True if any sabotage is active, false otherwise.</returns>
    internal static bool IsAnySabotageActive()
    {
        var allSabotages = new[]
        {
        SystemTypes.Electrical,
        SystemTypes.Reactor,
        SystemTypes.Laboratory,
        SystemTypes.LifeSupp,
        SystemTypes.HeliSabotage,
        SystemTypes.Comms,
        SystemTypes.MushroomMixupSabotage
    };

        return allSabotages.Any(IsSystemActive);
    }

    /// <summary>
    /// Gets whether the player is in a game.
    /// </summary>
    internal static bool IsInGame => InGame;

    /// <summary>
    /// Gets whether the player is in the lobby (joined but game not started).
    /// </summary>
    internal static bool IsLobby => AmongUsClient.Instance?.GameState == InnerNet.InnerNetClient.GameStates.Joined;

    /// <summary>
    /// Gets whether the intro cutscene is currently playing.
    /// </summary>
    internal static bool IsInIntro => IntroCutscene.Instance != null;

    /// <summary>
    /// Gets whether gameplay is currently active (not in lobby, intro, or ended).
    /// </summary>
    internal static bool IsInGamePlay => InGame && IsShip && !IsLobby && !IsInIntro || IsFreePlay;

    /// <summary>
    /// Gets whether the game has ended.
    /// </summary>
    internal static bool IsEnded => AmongUsClient.Instance?.GameState == InnerNet.InnerNetClient.GameStates.Ended;

    /// <summary>
    /// Gets whether the player is not joined to any game.
    /// </summary>
    internal static bool IsNotJoined => AmongUsClient.Instance?.GameState == InnerNet.InnerNetClient.GameStates.NotJoined;

    /// <summary>
    /// Gets whether the current game is an online game.
    /// </summary>
    internal static bool IsOnlineGame => AmongUsClient.Instance?.NetworkMode == NetworkModes.OnlineGame;

    /// <summary>
    /// Gets whether the player is connected to a vanilla (official) server.
    /// </summary>
    internal static bool IsVanillaServer
    {
        get
        {
            if (!IsOnlineGame) return false;

            string region = ServerManager.Instance.CurrentRegion.Name;
            return region == "North America" || region == "Europe" || region == "Asia";
        }
    }

    /// <summary>
    /// Gets whether the current game is a local game.
    /// </summary>
    internal static bool IsLocalGame => AmongUsClient.Instance?.NetworkMode == NetworkModes.LocalGame;

    /// <summary>
    /// Gets whether the current game is in free play mode.
    /// </summary>
    internal static bool IsFreePlay => AmongUsClient.Instance?.NetworkMode == NetworkModes.FreePlay;

    /// <summary>
    /// Gets whether the player is in a task (not in a meeting).
    /// </summary>
    internal static bool IsInTask => InGame && MeetingHud.Instance == null;

    /// <summary>
    /// Gets whether a meeting is currently active.
    /// </summary>
    internal static bool IsMeeting => InGame && MeetingHud.Instance != null;

    /// <summary>
    /// Gets whether voting is currently in progress during a meeting.
    /// </summary>
    internal static bool IsVoting => IsMeeting && MeetingHud.Instance?.state is MeetingHud.MeetingStates.Voted or MeetingHud.MeetingStates.NotVoted;

    /// <summary>
    /// Gets whether the meeting is proceeding to results.
    /// </summary>
    internal static bool IsProceeding => IsMeeting && MeetingHud.Instance?.state is MeetingHud.MeetingStates.Proceeding;

    /// <summary>
    /// Gets whether a player is being exiled.
    /// </summary>
    internal static bool IsExilling => ExileController.Instance != null && !(AirshipIsActive && Minigame.Instance != null && Minigame.Instance.isActiveAndEnabled);

    /// <summary>
    /// Gets whether the game start countdown is active.
    /// </summary>
    internal static bool IsCountDown => GameStartManager.InstanceExists && GameStartManager.Instance.startState == GameStartManager.StartingStates.Countdown;

    /// <summary>
    /// Gets whether a ship (map) is currently loaded.
    /// </summary>
    internal static bool IsShip => ShipStatus.Instance != null;

    /// <summary>
    /// Gets whether the local player is the host.
    /// </summary>
    internal static bool IsHost => AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

    /// <summary>
    /// Gets whether the lobby is private-only (requires specific settings).
    /// </summary>
    internal static bool IsPrivateOnlyLobby => (BAUPlugin.PrivateOnlyLobby.Value || AmongUsClient.Instance.AmLocalHost) && IsHost;

    /// <summary>
    /// Gets whether the local player can move.
    /// </summary>
    internal static bool IsCanMove => PlayerControl.LocalPlayer?.CanMove is true;

    /// <summary>
    /// Gets whether the local player is dead.
    /// </summary>
    internal static bool IsDead => PlayerControl.LocalPlayer?.Data?.IsDead is true;
}