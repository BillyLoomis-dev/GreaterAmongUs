using BetterAmongUs.Data;
using BetterAmongUs.Data.Config;
using BetterAmongUs.Generated;
using BetterAmongUs.Modules;
using BetterAmongUs.MonoScripts.Extended;
using BetterAmongUs.Patches.Gameplay.UI.Settings;
using BetterAmongUs.Utilities;
using Cpp2IL.Core.Extensions;
using TMPro;
using UnityEngine;

namespace BetterAmongUs.Managers;

/// <summary>
/// Manages in-game notifications, including cheat detection alerts and system messages.
/// </summary>
/// <remarks>
/// GreaterAmongUs: cheat alerts are "sticky". They stack (newest on top), stay up for up to
/// 60 minutes and are dismissed with a key, so an alert can't be missed while you are busy:
/// <list type="bullet">
/// <item>Detection (a cheat caught live): CTRL+P dismisses the top alert.</item>
/// <item>Lobby warning (a known cheater joined, or a ban-list match): CTRL+Y dismisses the top alert.</item>
/// </list>
/// Pressing the key for the other kind does nothing, so you can't clear the wrong one by accident.
/// Alerts you haven't dismissed come back when you join the next lobby.
/// </remarks>
internal static class BetterNotificationManager
{
    private static GameObject? BAUNotificationManagerObj;
    private static TextMeshPro? NameText;
    private static TextMeshPro? TextArea;
    private readonly static Dictionary<string, float> NotifyQueue = [];
    private static float showTime = 0f;
    private static Camera? localCamera;
    private static bool Notifying = false;

    private const float CheatPopupDurationSeconds = 60f * 60f; // 60 minutes
    private const string DetectionHintText = "<size=70%><color=#aaaaaa>[CTRL+P: dismiss]</color></size>\n";
    private const string LobbyWarningHintText = "<size=70%><color=#aaaaaa>[CTRL+Y: dismiss warning]</color></size>\n";

    /// <summary>
    /// The kind of sticky alert, which decides the key that dismisses it.
    /// </summary>
    internal enum CheatAlertKind { Detection, LobbyWarning }

    private sealed class CheatAlertEntry(CheatAlertKind kind, string key, string text)
    {
        internal readonly CheatAlertKind Kind = kind;
        internal readonly string Key = key;
        internal readonly string Text = text;
        internal int Count = 1;
    }

    private static readonly List<CheatAlertEntry> cheatAlertStack = [];
    private static bool isCheatPopupShowing = false;

    private static bool IsCtrlDown() => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

    /// <summary>
    /// Initializes the BetterNotificationManager by creating and configuring a cloned instance of the game's chat notification system.
    /// </summary>
    internal static void Init()
    {
        if (BAUNotificationManagerObj == null)
        {
            var ChatNotifications = HudManager.Instance.Chat.chatNotification;
            if (ChatNotifications != null)
            {
                ChatNotifications.timeOnScreen = 1f;
                ChatNotifications.gameObject.SetActive(true);

                // Clone chat notification system for BAU notifications
                GameObject BAUNotification = UnityEngine.Object.Instantiate(ChatNotifications.gameObject);
                BAUNotification.name = "BAUNotification";
                BAUNotification.GetComponent<ChatNotification>().DestroyMono();

                // Remove unnecessary elements from the clone
                GameObject.Find($"{BAUNotification.name}/Sizer/PoolablePlayer").DestroyObj();
                GameObject.Find($"{BAUNotification.name}/Sizer/ColorText").DestroyObj();

                // Position notification at bottom-left corner
                BAUNotification.GetComponent<AspectPosition>().DistanceFromEdge = new Vector3(-1.57f, 5.3f, -15f);
                GameObject.Find($"{BAUNotification.name}/Sizer/NameText").transform.localPosition = new Vector3(-3.3192f, -0.0105f);

                // Cache TextMeshPro component for text updates
                NameText = GameObject.Find($"{BAUNotification.name}/Sizer/NameText").GetComponent<TextMeshPro>();
                UnityEngine.Object.DontDestroyOnLoad(BAUNotification);
                BAUNotificationManagerObj = BAUNotification;
                BAUNotification.SetActive(false);

                // Reset original chat notification settings
                ChatNotifications.timeOnScreen = 0f;
                ChatNotifications.gameObject.SetActive(false);

                // Configure text wrapping for multi-line notifications
                TextArea = BAUNotificationManagerObj.transform.Find("Sizer/ChatText (TMP)").GetComponent<TextMeshPro>();
                TextArea.enableWordWrapping = true;
                TextArea.m_firstOverflowCharacterIndex = 0;
                TextArea.overflowMode = TextOverflowModes.Overflow;
            }
        }
    }

    /// <summary>
    /// Cleans up and destroys the notification manager.
    /// </summary>
    internal static void Detach()
    {
        NotifyQueue.Clear();
        cheatAlertStack.Clear();
        isCheatPopupShowing = false;

        if (BAUNotificationManagerObj == null)
            return;

        BAUNotificationManagerObj.DestroyObj();
    }

    /// <summary>
    /// Displays a notification message in-game.
    /// </summary>
    /// <param name="text">The text to display in the notification.</param>
    /// <param name="time">The duration in seconds to show the notification.</param>
    /// <param name="force">If notification should be forced even if better notifications is disabled.</param>
    internal static void Notify(string text, float time = 5f, bool force = false)
    {
        if (!BAUConfigs.BetterNotifications.Value && !force)
            return;

        if (BAUNotificationManagerObj == null || NameText == null || TextArea == null)
            return;

        if (Notifying)
        {
            if (text == TextArea.text)
                return;

            NotifyQueue[text] = time;
            return;
        }

        showTime = time;
        BAUNotificationManagerObj.SetActive(true);
        NameText.text = $"<color=#00ff44>{TranslationStrings.SystemNotification}</color>";
        TextArea.text = text;
        Notifying = true;
    }

    /// <summary>
    /// Pushes a live cheat-detection alert onto the sticky stack (CTRL+P dismisses it).
    /// The same player caught again for the same reason bumps a counter instead of
    /// adding another entry, so a spamming cheater can't bury older alerts.
    /// </summary>
    /// <param name="key">Dedup key (player + reason).</param>
    /// <param name="text">The alert text.</param>
    internal static void NotifySticky(string key, string text) => PushAlert(CheatAlertKind.Detection, key, text);

    /// <summary>
    /// Pushes a lobby warning onto the sticky stack (CTRL+Y dismisses it).
    /// </summary>
    /// <param name="key">Dedup key.</param>
    /// <param name="text">The warning text.</param>
    internal static void NotifyLobbyWarning(string key, string text) => PushAlert(CheatAlertKind.LobbyWarning, key, text);

    /// <summary>
    /// Pushes a "known cheater joined" warning. Fired when a player found in the saved cheat
    /// data (CheatData / SickoData / AUMData / KNData / MMCData) joins the lobby.
    /// Works for the host and for non-host players.
    /// </summary>
    /// <param name="playerName">In-game display name of the joining player.</param>
    /// <param name="friendCode">Friend code, or "" if unknown.</param>
    /// <param name="hashPuid">Hashed PUID for cross-account identification.</param>
    /// <param name="reason">The stored reason from the cheat data file.</param>
    internal static void NotifyKnownCheaterInLobby(string playerName, string friendCode, string hashPuid, string reason)
    {
        string fc = string.IsNullOrEmpty(friendCode) ? "(unknown)" : friendCode;
        string puid = string.IsNullOrEmpty(hashPuid) ? "(unknown)" : hashPuid;
        string r = string.IsNullOrEmpty(reason) ? "(no reason stored)" : reason;

        string text =
            $"<color=#ffae00><b>Known cheater joined the lobby</b></color>\n" +
            $"Name: <color=#0097b5>{playerName}</color>\n" +
            $"Friend code: <color=#cccccc>{fc}</color>\n" +
            $"Hash PUID: <color=#888888>{puid}</color>\n" +
            $"Previous reason: <b><color=#fc0000>{r}</color></b>";

        NotifyLobbyWarning($"known:{fc}:{puid}", text);
    }

    private static void PushAlert(CheatAlertKind kind, string key, string text)
    {
        if (!BAUConfigs.BetterNotifications.Value)
            return;

        // Note: no "popup not created yet" bail-out here. The alert is kept on the stack and
        // Update() shows it as soon as the popup exists.
        var existing = cheatAlertStack.FirstOrDefault(e => e.Kind == kind && e.Key == key);
        if (existing != null)
        {
            // Same alert again: count it and move it to the top.
            existing.Count++;
            cheatAlertStack.Remove(existing);
            cheatAlertStack.Add(existing);
        }
        else
        {
            cheatAlertStack.Add(new CheatAlertEntry(kind, key, text));
        }

        DisplayTopCheatAlert();
    }

    /// <summary>
    /// Renders the top-of-stack alert, or hides the popup when the stack is empty.
    /// </summary>
    private static void DisplayTopCheatAlert()
    {
        if (BAUNotificationManagerObj == null || NameText == null || TextArea == null)
            return;

        if (cheatAlertStack.Count == 0)
        {
            TextArea.text = "";
            BAUNotificationManagerObj.SetActive(false);
            Notifying = false;
            isCheatPopupShowing = false;
            showTime = 0f;
            return;
        }

        var top = cheatAlertStack[^1];
        string hint = top.Kind == CheatAlertKind.Detection ? DetectionHintText : LobbyWarningHintText;
        string pendingBadge = cheatAlertStack.Count > 1
            ? $"<size=70%><color=#ff9000>[{cheatAlertStack.Count} alerts - dismiss with the key shown above]</color></size>\n"
            : "";
        string repeat = top.Count > 1 ? $" <color=#ff9000>(x{top.Count})</color>" : "";

        showTime = CheatPopupDurationSeconds;
        isCheatPopupShowing = true;

        BAUNotificationManagerObj.SetActive(true);
        NameText.text = $"<color=#ff0000>{TranslationStrings.SystemNotification}</color>";
        TextArea.text = hint + pendingBadge + top.Text + repeat;
        Notifying = true;
    }

    /// <summary>
    /// Handles cheat detection notifications and actions.
    /// </summary>
    /// <param name="player">The player who was detected cheating.</param>
    /// <param name="reason">The reason for the cheat detection.</param>
    /// <param name="newText">Optional custom text to replace the default detection message.</param>
    /// <param name="kickPlayer">Whether to kick the detected player (blocked by host safety, see PlayerControlUtils.AutomaticKicksEnabled).</param>
    /// <param name="forceBan">Whether to force a ban regardless of settings.</param>
    /// <returns>True if the cheat detection was handled, false otherwise.</returns>
    internal static bool NotifyCheat(PlayerControl player, string reason, string newText = "", bool kickPlayer = true, bool forceBan = false)
    {
        // Bail only on missing data or self-detect. Known cheaters are NOT skipped:
        // something new they do this session must still alert the host live.
        // The cheat data entry is only added once (below).
        if (player == null)
            return false;

        if (player.Data == null)
            return false;

        if (player.IsLocalPlayer())
            return false;

        var Reason = reason;
        if (BetterGameSettings.CensorDetectionReason.GetBool())
        {
            Reason = string.Concat('*').Repeat(reason.Length);
        }

        string realName = player.ExtendedData()?.RealName ?? player.Data.PlayerName;
        string playerDetected = TranslationStrings.AntiCheat_PlayerDetected.LocalizedString;
        string unauthorizedAction = TranslationStrings.AntiCheat_UnauthorizedAction.LocalizedString;
        string byAntiCheat = TranslationStrings.AntiCheat_ByAntiCheat.LocalizedString;
        string playerDetectedLog = Translator.GetString(TranslationStrings.AntiCheat_PlayerDetected, useConsoleLanguage: true);
        string unauthorizedActionLog = Translator.GetString(TranslationStrings.AntiCheat_UnauthorizedAction, useConsoleLanguage: true);

        string text = $"{playerDetected}: <color=#0097b5>{realName}</color> {unauthorizedAction}: <b><color=#fc0000>{Reason}</color></b>";
        string rawText = $"{playerDetectedLog}: <color=#0097b5>{realName}</color> {unauthorizedActionLog}: <b><color=#fc0000>{reason}</color></b>";

        if (newText != "")
        {
            text = $"{playerDetected}: <color=#0097b5>{realName}</color> " + newText + $": <b><color=#fc0000>{Reason}</color></b>";
            rawText = $"{playerDetectedLog}: <color=#0097b5>{realName}</color> " + newText + $": <b><color=#fc0000>{reason}</color></b>";
        }

        // Save to the cheat data on the FIRST detection only (used by the lobby warning later).
        if (!BetterDataManager.Files.BetterDataFile.CheatData.Any(info => info.CheckPlayerData(player.Data)))
        {
            BetterDataManager.Files.BetterDataFile.CheatData.Add(new(realName, player.GetHashPuid(), player.Data.FriendCode, reason));
            BetterDataManager.Files.BetterDataFile.Save();
        }

        // Always alert (sticky, stacked, deduplicated per player + reason).
        NotifySticky($"cheat:{player.Data.FriendCode}:{player.GetHashPuid()}:{reason}", text);

        // Real name only: the name shown above the player can be a shapeshift disguise or carry role text.
        BAUPlugin.Logger.LogCheat($"Info: {realName} - {player.Data.FriendCode} - {player.GetHashPuid()}");
        BAUPlugin.Logger.LogCheat(Utils.RemoveHtmlText(rawText));

        if (GameState.IsHost && kickPlayer)
        {
            // Automatic kick: blocked by host safety, only logged.
            string kickMessage = string.Format(TranslationStrings.AntiCheat_KickMessage.LocalizedString, byAntiCheat, Reason);
            player.Kick(true, kickMessage, true, false, forceBan);
        }

        return true;
    }

    /// <summary>
    /// Clears all pending and active notifications, including the sticky cheat alerts.
    /// </summary>
    internal static void ClearNotifications()
    {
        NotifyQueue.Clear();
        cheatAlertStack.Clear();
        isCheatPopupShowing = false;
        showTime = 0f;
        Notifying = false;

        if (BAUNotificationManagerObj != null)
        {
            BAUNotificationManagerObj.SetActive(false);
        }
    }

    /// <summary>
    /// Updates the notification manager each frame.
    /// </summary>
    internal static void Update()
    {
        if (BAUNotificationManagerObj == null)
            return;

        if (TextArea == null)
            return;

        if (!localCamera)
        {
            if (HudManager.InstanceExists)
            {
                localCamera = HudManager.Instance.GetComponentInChildren<Camera>();
            }
            else
            {
                localCamera = Camera.main;
            }
        }

        // Bottom edge, 1.3 left of center, raised to 1.75 so the alert sits clear of the chat/buttons.
        BAUNotificationManagerObj.transform.position = AspectPosition.ComputeWorldPosition(localCamera, AspectPosition.EdgeAlignments.Bottom, new Vector3(-1.3f, 1.75f, localCamera.nearClipPlane + 0.1f));

        // Back in a lobby/game with alerts you never dismissed: show them again.
        if (GameState.IsInGame && cheatAlertStack.Count > 0 && !isCheatPopupShowing)
        {
            DisplayTopCheatAlert();
        }

        if (isCheatPopupShowing && cheatAlertStack.Count > 0 && IsCtrlDown())
        {
            var topKind = cheatAlertStack[^1].Kind;
            if ((topKind == CheatAlertKind.Detection && Input.GetKeyDown(KeyCode.P))
                || (topKind == CheatAlertKind.LobbyWarning && Input.GetKeyDown(KeyCode.Y)))
            {
                cheatAlertStack.RemoveAt(cheatAlertStack.Count - 1);
                DisplayTopCheatAlert();
            }
        }

        showTime -= Time.deltaTime;
        if (showTime <= 0f && GameState.IsInGame)
        {
            // Timeout: an alert ignored for 60 minutes clears the whole stack.
            if (isCheatPopupShowing)
            {
                cheatAlertStack.Clear();
                isCheatPopupShowing = false;
            }

            TextArea.text = "";
            BAUNotificationManagerObj.SetActive(false);
            Notifying = false;

            CheckNotifyQueue();
        }

        if (!GameState.IsInGame)
        {
            // Left the game: hide, but KEEP the sticky stack so it comes back in the next lobby.
            BAUNotificationManagerObj.SetActive(false);
            Notifying = false;
            isCheatPopupShowing = false;
            showTime = 0f;
        }
    }

    /// <summary>
    /// Checks and processes queued notifications.
    /// </summary>
    private static void CheckNotifyQueue()
    {
        if (NotifyQueue.Any())
        {
            var key = NotifyQueue.Keys.First();
            var value = NotifyQueue[key];
            Notify(key, value);
            NotifyQueue.Remove(key);
        }
    }
}
