using BetterAmongUs.Data;
using BetterAmongUs.Helpers;
using BetterAmongUs.Modules;
using BetterAmongUs.Mono;
using BetterAmongUs.Patches.Gameplay.UI.Settings;
using Cpp2IL.Core.Extensions;
using TMPro;
using UnityEngine;

namespace BetterAmongUs.Managers;

/// <summary>
/// Manages in-game notifications for BetterAmongUs, including cheat detection alerts and system messages.
/// </summary>
internal static class BetterNotificationManager
{
    internal static GameObject? BAUNotificationManagerObj;
    internal static TextMeshPro? NameText;
    internal static TextMeshPro? TextArea => BAUNotificationManagerObj.transform.Find("Sizer/ChatText (TMP)").GetComponent<TextMeshPro>();
    internal static Dictionary<string, float> NotifyQueue = [];
    internal static float showTime = 0f;
    private static Camera? localCamera;
    internal static bool Notifying = false;

    // Sticky-cheat-alert state.
    // Alerts (both live cheat detections and lobby-warnings about known cheaters
    // who rejoined) accumulate on a stack, newest on top. The popup always
    // shows the top entry; popping the top reveals the previous entry. Each
    // KIND has its own dismiss keybind so pressing the wrong key is a no-op.
    //
    // Kinds:
    //   Detection    — live cheat caught via NotifyCheat. CTRL+P pops.
    //   LobbyWarning — a player already in CheatData (etc.) rejoined.
    //                  CTRL+Y pops.
    //
    // Audio alarm and in-chat alert were both removed: the visual popup is
    // impossible to miss now that the bubble background is stripped and the
    // popup sits at the top-left of the screen.
    private const float CheatPopupDurationSeconds = 60f * 60f; // 60 minutes
    private const string DetectionHintText =
        "<size=70%><color=#aaaaaa>[CTRL+P: dismiss]</color></size>\n";
    private const string LobbyWarningHintText =
        "<size=70%><color=#aaaaaa>[CTRL+Y: dismiss warning]</color></size>\n";

    internal enum CheatAlertKind { Detection, LobbyWarning }
    private readonly struct CheatAlertEntry
    {
        public readonly CheatAlertKind Kind;
        public readonly string Text;
        public CheatAlertEntry(CheatAlertKind kind, string text) { Kind = kind; Text = text; }
    }

    private static readonly List<CheatAlertEntry> cheatAlertStack = new();
    private static bool isCheatPopupShowing = false;

    private static bool IsCtrlDown() =>
        Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

    /// <summary>
    /// Displays a notification message in-game.
    /// </summary>
    /// <param name="text">The text to display in the notification.</param>
    /// <param name="Time">The duration in seconds to show the notification.</param>
    internal static void Notify(string text, float Time = 5f)
    {
        if (!BAUPlugin.BetterNotifications.Value) return;

        if (BAUNotificationManagerObj != null)
        {
            if (Notifying)
            {
                if (text == BAUNotificationManagerObj.transform.Find("Sizer/ChatText (TMP)").GetComponent<TextMeshPro>().text)
                    return;
                NotifyQueue[text] = Time;
                return;
            }

            showTime = Time;
            BAUNotificationManagerObj.SetActive(true);
            NameText.text = $"<color=#00ff44>{Translator.GetString("SystemNotification")}</color>";
            TextArea.text = text;
            Notifying = true;
        }
    }

    /// <summary>
    /// Pushes a live cheat-detection alert onto the stack and displays it.
    /// Dismissed with CTRL+P.
    /// </summary>
    private static void NotifySticky(string text)
    {
        if (!BAUPlugin.BetterNotifications.Value) return;
        if (BAUNotificationManagerObj == null) return;

        cheatAlertStack.Add(new CheatAlertEntry(CheatAlertKind.Detection, text));
        DisplayTopCheatAlert();
    }

    /// <summary>
    /// Pushes a "known cheater in lobby" warning onto the stack. Fired when a
    /// player whose data is in CheatData / SickoData / AUMData / KNData joins
    /// the lobby. Dismissed with CTRL+Y.
    /// </summary>
    /// <param name="playerName">In-game display name of the joining player.</param>
    /// <param name="friendCode">Friend code (e.g. "adeptglove#5361") or "" if unknown.</param>
    /// <param name="hashPuid">Hashed PUID for cross-account identification.</param>
    /// <param name="reason">The stored reason from BAU's cheat data file.</param>
    internal static void NotifyKnownCheaterInLobby(string playerName, string friendCode, string hashPuid, string reason)
    {
        if (!BAUPlugin.BetterNotifications.Value) return;
        if (BAUNotificationManagerObj == null) return;

        string fc = string.IsNullOrEmpty(friendCode) ? "(unknown)" : friendCode;
        string puid = string.IsNullOrEmpty(hashPuid) ? "(unknown)" : hashPuid;
        string r = string.IsNullOrEmpty(reason) ? "(no reason stored)" : reason;

        string text =
            $"<color=#ffae00><b>Known cheater rejoined lobby</b></color>\n" +
            $"Name: <color=#0097b5>{playerName}</color>\n" +
            $"Friend code: <color=#cccccc>{fc}</color>\n" +
            $"Hash PUID: <color=#888888>{puid}</color>\n" +
            $"Previous reason: <b><color=#fc0000>{r}</color></b>";

        cheatAlertStack.Add(new CheatAlertEntry(CheatAlertKind.LobbyWarning, text));
        DisplayTopCheatAlert();
    }

    /// <summary>
    /// Renders the top-of-stack alert (or hides the popup if empty). Uses the
    /// kind-specific hint footer.
    /// </summary>
    private static void DisplayTopCheatAlert()
    {
        if (BAUNotificationManagerObj == null) return;

        if (cheatAlertStack.Count == 0)
        {
            BAUNotificationManagerObj.transform.Find("Sizer/ChatText (TMP)").GetComponent<TextMeshPro>().text = "";
            BAUNotificationManagerObj.SetActive(false);
            Notifying = false;
            isCheatPopupShowing = false;
            showTime = 0f;
            return;
        }

        CheatAlertEntry top = cheatAlertStack[cheatAlertStack.Count - 1];
        string hint = top.Kind == CheatAlertKind.Detection ? DetectionHintText : LobbyWarningHintText;
        string pendingBadge = cheatAlertStack.Count > 1
            ? $"<size=70%><color=#ff9000>[{cheatAlertStack.Count} pending — clear with the dismiss key shown above]</color></size>\n"
            : "";

        showTime = CheatPopupDurationSeconds;
        isCheatPopupShowing = true;

        BAUNotificationManagerObj.SetActive(true);
        NameText.text = $"<color=#ff0000>{Translator.GetString("SystemNotification")}</color>";
        // Hint goes ABOVE the main message; pending-count badge sits between
        // the hint and the body. Both have trailing newlines.
        TextArea.text = hint + pendingBadge + top.Text;
        Notifying = true;
    }

    /// <summary>
    /// Handles cheat detection notifications and actions.
    /// </summary>
    /// <param name="player">The player who was detected cheating.</param>
    /// <param name="reason">The reason for the cheat detection.</param>
    /// <param name="newText">Optional custom text to replace the default detection message.</param>
    /// <param name="kickPlayer">Whether to kick the detected player.</param>
    /// <param name="forceBan">Whether to force a ban regardless of settings.</param>
    /// <returns>True if the cheat detection was handled, false otherwise.</returns>
    internal static bool NotifyCheat(PlayerControl player, string reason, string newText = "", bool kickPlayer = true, bool forceBan = false)
    {
        // Bail only on missing data or self-detect. We deliberately do NOT
        // short-circuit on IsCheater() here — known cheaters who do new
        // suspicious things during the same session MUST still trigger
        // popup + alarm + chat alert so the host knows it's happening live.
        // CheatData dedup happens further down (we only add an entry once),
        // but the user-visible alert fires every time.
        if (player?.Data == null) return false;
        if (player.IsLocalPlayer()) return false;

        var Reason = reason;
        if (BetterGameSettings.CensorDetectionReason.GetBool())
        {
            Reason = string.Concat('*').Repeat(reason.Length);
        }

        string playerDetected = Translator.GetString("AntiCheat.PlayerDetected");
        string unauthorizedAction = Translator.GetString("AntiCheat.UnauthorizedAction");
        string playerDetectedLog = Translator.GetString("AntiCheat.PlayerDetected", useConsoleLanguage: true);
        string unauthorizedActionLog = Translator.GetString("AntiCheat.UnauthorizedAction", useConsoleLanguage: true);

        string text = $"{playerDetected}: <color=#0097b5>{player?.BetterData().RealName}</color> {unauthorizedAction}: <b><color=#fc0000>{Reason}</color></b>";
        string rawText = $"{playerDetectedLog}: <color=#0097b5>{player?.BetterData().RealName}</color> {unauthorizedActionLog}: <b><color=#fc0000>{reason}</color></b>";

        if (newText != "")
        {
            text = $"{playerDetected}: <color=#0097b5>{player?.BetterData().RealName}</color> " + newText + $": <b><color=#fc0000>{Reason}</color></b>";
            rawText = $"{playerDetectedLog}: <color=#0097b5>{player?.BetterData().RealName}</color> " + newText + $": <b><color=#fc0000>{reason}</color></b>";
        }

        // Persist to CheatData on FIRST detection only — used by the lobby
        // warning popup on future joins. Don't add duplicate entries for
        // repeat detections of the same player.
        bool isNewDetection = !BetterDataManager.BetterDataFile.CheatData.Any(info => info.CheckPlayerData(player.Data));
        if (isNewDetection)
        {
            BetterDataManager.BetterDataFile.CheatData.Add(new(player?.BetterData().RealName ?? player.Data.PlayerName, player.GetHashPuid(), player.Data.FriendCode, reason));
            BetterDataManager.BetterDataFile.Save();
        }

        // ALWAYS push a new sticky alert per detection (stacked).
        // User clears with CTRL+P per entry.
        NotifySticky(text);

        Logger_.LogCheat($"{player.cosmetics.nameText.text} Info: {player.Data.PlayerName} - {player.Data.FriendCode} - {player.GetHashPuid()}");
        Logger_.LogCheat(Utils.RemoveHtmlText(rawText));

        // NOTE: Kick() is hard-disabled in PlayerControlHelper.Kick — it no
        // longer calls AmongUsClient.KickPlayer (would trigger Innersloth's
        // anti-abuse self-ban on the host). We leave the call site here so
        // anyone re-enabling auto-kick gets a single line to uncomment.
        if (GameState.IsHost && kickPlayer)
        {
            string kickMessage = string.Format(Translator.GetString("AntiCheat.KickMessage"), Translator.GetString("AntiCheat.ByAntiCheat"), Reason);
            player.Kick(true, kickMessage, true, false, forceBan);
        }

        return true;
    }

    /// <summary>
    /// Updates the notification manager each frame.
    /// </summary>
    internal static void Update()
    {
        if (BAUNotificationManagerObj != null)
        {
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

            // Original BAU anchor (bottom edge, -1.3 left of center) with the
            // Y offset raised — 1.75 world units up from bottom.
            BAUNotificationManagerObj.transform.position = AspectPosition.ComputeWorldPosition(localCamera, AspectPosition.EdgeAlignments.Bottom, new Vector3(-1.3f, 1.75f, localCamera.nearClipPlane + 0.1f));

            // Persistence-across-games: if we just re-entered a game/lobby and
            // the stack still has alerts from an AFK session that ended without
            // being dismissed, surface the top entry again. DisplayTopCheatAlert
            // refreshes the 60-min timer, so a stale popup won't instantly
            // expire on re-entry. Only fires when we ARE in-game (the game-end
            // branch below hides the popup but preserves the stack until then).
            if (GameState.IsInGame && cheatAlertStack.Count > 0 && !isCheatPopupShowing)
            {
                DisplayTopCheatAlert();
            }

            // CTRL+P — pop the top of the stack IF it's a Detection. Wrong-key
            // for the kind on top = no-op (prevents accidentally clearing a
            // LobbyWarning with the Detection key or vice versa).
            if (isCheatPopupShowing && cheatAlertStack.Count > 0
                && cheatAlertStack[cheatAlertStack.Count - 1].Kind == CheatAlertKind.Detection
                && IsCtrlDown() && Input.GetKeyDown(KeyCode.P))
            {
                cheatAlertStack.RemoveAt(cheatAlertStack.Count - 1);
                DisplayTopCheatAlert();
            }

            // CTRL+Y — pop the top of the stack IF it's a LobbyWarning.
            if (isCheatPopupShowing && cheatAlertStack.Count > 0
                && cheatAlertStack[cheatAlertStack.Count - 1].Kind == CheatAlertKind.LobbyWarning
                && IsCtrlDown() && Input.GetKeyDown(KeyCode.Y))
            {
                cheatAlertStack.RemoveAt(cheatAlertStack.Count - 1);
                DisplayTopCheatAlert();
            }

            showTime -= Time.deltaTime;
            if (showTime <= 0f && GameState.IsInGame)
            {
                // Timeout — drain the whole stack at once (no per-entry timeout).
                cheatAlertStack.Clear();
                BAUNotificationManagerObj.transform.Find("Sizer/ChatText (TMP)").GetComponent<TextMeshPro>().text = "";
                BAUNotificationManagerObj.SetActive(false);
                Notifying = false;
                isCheatPopupShowing = false;

                CheckNotifyQueue();
            }

            if (!GameState.IsInGame)
            {
                // Game over — hide the popup but PRESERVE the alert stack so
                // any AFK-missed detections resurface when the user joins
                // another lobby/game. The re-show branch at the top of
                // Update() handles resurrection on re-entry. The stack is
                // in-memory only, so quitting the AU process still wipes it.
                BAUNotificationManagerObj.SetActive(false);
                Notifying = false;
                isCheatPopupShowing = false;
                showTime = 0f;
            }
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
