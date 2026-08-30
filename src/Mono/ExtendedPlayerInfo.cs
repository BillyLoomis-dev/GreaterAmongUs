using AmongUs.GameOptions;
using BetterAmongUs.Helpers;
using BetterAmongUs.Managers;
using BetterAmongUs.Modules;
using Il2CppInterop.Runtime.Attributes;
using InnerNet;
using UnityEngine;

namespace BetterAmongUs.Mono;

/// <summary>
/// Extended player information with additional data and anti-cheat features.
/// </summary>
internal sealed class ExtendedPlayerInfo : MonoBehaviour, IMonoExtension<NetworkedPlayerInfo>
{
    internal ExtendedPlayerInfo()
    {
        HandshakeHandler = new(this);
    }

    /// <summary>
    /// Gets or sets the base NetworkedPlayerInfo instance.
    /// </summary>
    public NetworkedPlayerInfo? BaseMono { get; set; }

    /// <summary>
    /// Gets the NetworkedPlayerInfo instance.
    /// </summary>
    internal NetworkedPlayerInfo? _Data => BaseMono;

    private bool hasSet = false;

    /// <summary>
    /// Initializes the extended player info.
    /// </summary>
    /// <param name="data">The NetworkedPlayerInfo to extend.</param>
    [HideFromIl2Cpp]
    internal void SetInfo(NetworkedPlayerInfo data)
    {
        if (hasSet) return;
        _PlayerId = data.PlayerId;
        hasSet = true;
    }

    private float timeAccumulator = 0f;

    private void Awake()
    {
        if (!this.RegisterExtension()) return;
        HandshakeHandler.WaitSendSecretToPlayer();
    }

    private void OnDestroy()
    {
        this.UnregisterExtension();
    }

    /// <summary>
    /// Updates anti-cheat monitoring and state tracking.
    /// </summary>
    internal void Update()
    {
        var time = Time.deltaTime;

        AntiCheatInfo.TimeSinceLastTask += time;

        if (AntiCheatInfo.RPCSentPS > 0)
        {
            bool flag = _Data.IsCheater();

            if (AntiCheatInfo.RPCSentPS >= ExtendedAntiCheatInfo.MAX_RPC_SENT && !flag)
            {
                BetterNotificationManager.NotifyCheat(_Data.Object,
                    Translator.GetString("AntiCheat.Reason.RPCSentPS"),
                    Translator.GetString("AntiCheat.UnauthorizedAction")
                );
                Logger_.LogCheat($"{_Data.Object.BetterData().RealName} {AntiCheatInfo.RPCSentPS} Sent.");
            }

            timeAccumulator += time;
            if (timeAccumulator >= 0.25f - 0.005 * AntiCheatInfo.RPCSentPS)
            {
                AntiCheatInfo.RPCSentPS -= 1;
                timeAccumulator = 0f;
            }
        }
    }

    /// <summary>
    /// Gets the handshake handler for this player.
    /// </summary>
    [HideFromIl2Cpp]
    internal HandshakeHandler HandshakeHandler { get; }

    /// <summary>
    /// Gets the player ID.
    /// </summary>
    [HideFromIl2Cpp]
    internal byte _PlayerId { get; private set; }

    /// <summary>
    /// Gets the player's real name.
    /// </summary>
    internal string RealName => _Data?.PlayerName ?? "???";

    /// <summary>
    /// Gets or sets the last name set for this player.
    /// </summary>
    internal string NameSetAsLast { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this player is a BetterAmongUs user.
    /// </summary>
    internal bool IsBetterUser { get; set; } = false;

    /// <summary>
    /// Gets or sets whether this player is a verified BetterAmongUs user.
    /// </summary>
    internal bool IsVerifiedBetterUser { get; set; } = false;

    /// <summary>
    /// Gets or sets whether disconnect message has been shown.
    /// </summary>
    internal bool HasShowDcMsg { get; set; } = false;

    /// <summary>
    /// Gets or sets the disconnect reason.
    /// </summary>
    internal DisconnectReasons DisconnectReason { get; set; } = DisconnectReasons.Unknown;

    /// <summary>
    /// Gets the extended role information.
    /// </summary>
    [HideFromIl2Cpp]
    internal ExtendedRoleInfo? RoleInfo { get; } = new();

    /// <summary>
    /// Gets the extended anti-cheat information.
    /// </summary>
    [HideFromIl2Cpp]
    internal ExtendedAntiCheatInfo? AntiCheatInfo { get; } = new();
}

/// <summary>
/// Contains anti-cheat monitoring information for a player.
/// </summary>
internal class ExtendedAntiCheatInfo
{
    /// <summary>
    /// Maximum allowed RPCs per second.
    /// </summary>
    internal const int MAX_RPC_SENT = 50;

    /// <summary>
    /// Gets or sets whether the player is banned by anti-cheat.
    /// </summary>
    internal bool BannedByAntiCheat { get; set; } = false;

    /// <summary>
    /// Gets or sets the list of AUM chat messages.
    /// </summary>
    internal List<string> AUMChats { get; set; } = [];

    /// <summary>
    /// Gets or sets the RPCs sent per second.
    /// </summary>
    internal int RPCSentPS { get; set; } = 0;

    /// <summary>
    /// Gets or sets the number of times attempted to kill.
    /// </summary>
    internal int TimesAttemptedKilled { get; set; } = 0;

    /// <summary>
    /// Gets or sets the number of open sabotages.
    /// </summary>
    internal int OpenSabotageNum { get; set; } = 0;

    /// <summary>
    /// Gets whether the player is fixing panel sabotage.
    /// </summary>
    internal bool IsFixingPanelSabotage => OpenSabotageNum != 0;

    /// <summary>
    /// Gets or sets the time since last task.
    /// </summary>
    internal float TimeSinceLastTask { get; set; } = 5f;

    /// <summary>
    /// Gets or sets the last task ID.
    /// </summary>
    internal uint LastTaskId { get; set; } = 999;

    /// <summary>
    /// Gets or sets whether the player has set their name.
    /// </summary>
    internal bool HasSetName { get; set; }

    /// <summary>
    /// Gets or sets whether the player has set their level.
    /// </summary>
    internal bool HasSetLevel { get; set; }
}

/// <summary>
/// Contains extended role information for a player.
/// </summary>
internal class ExtendedRoleInfo
{
    /// <summary>
    /// Gets or sets the number of kills.
    /// </summary>
    internal int Kills { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether noisemaker notification is enabled.
    /// </summary>
    internal bool HasNoisemakerNotify { get; set; } = false;

    /// <summary>
    /// Gets or sets the role to display when dead.
    /// </summary>
    internal RoleTypes DeadDisplayRole { get; set; }

    /// <summary>
    /// If this player is a Judge, the PlayerId of whoever they overruled (byte.MaxValue = none yet).
    /// Set by the SetJudgeOverrule patch; shown only in the dead-only role reveal.
    /// </summary>
    internal byte JudgedId { get; set; } = byte.MaxValue;
}

/// <summary>
/// Extension methods for accessing extended player data.
/// </summary>
internal static class PlayerControlDataExtension
{
    /// <summary>
    /// Gets extended player data from a PlayerControl.
    /// </summary>
    /// <param name="player">The PlayerControl instance.</param>
    /// <returns>The ExtendedPlayerInfo, or null if not found.</returns>
    internal static ExtendedPlayerInfo? BetterData(this PlayerControl player)
    {
        return MonoExtensionManager.Get<ExtendedPlayerInfo>(player.Data);
    }

    /// <summary>
    /// Waits for extended player data to be available, then calls a callback.
    /// </summary>
    /// <param name="player">The PlayerControl instance.</param>
    /// <param name="callback">The callback to execute with the extended data.</param>
    internal static void BetterDataWait(this PlayerControl player, Action<ExtendedPlayerInfo> callback)
    {
        MonoExtensionManager.RunWhenNotNull<ExtendedPlayerInfo>(player, () => player?.BetterData(), callback);
    }

    /// <summary>
    /// Gets extended player data from a NetworkedPlayerInfo.
    /// </summary>
    /// <param name="data">The NetworkedPlayerInfo instance.</param>
    /// <returns>The ExtendedPlayerInfo, or null if not found.</returns>
    internal static ExtendedPlayerInfo? BetterData(this NetworkedPlayerInfo data)
    {
        return MonoExtensionManager.Get<ExtendedPlayerInfo>(data);
    }

    /// <summary>
    /// Gets extended player data from a ClientData.
    /// </summary>
    /// <param name="data">The ClientData instance.</param>
    /// <returns>The ExtendedPlayerInfo, or null if not found.</returns>
    internal static ExtendedPlayerInfo? BetterData(this ClientData data)
    {
        var player = Utils.PlayerFromClientId(data.Id);
        return MonoExtensionManager.Get<ExtendedPlayerInfo>(player.Data);
    }
}