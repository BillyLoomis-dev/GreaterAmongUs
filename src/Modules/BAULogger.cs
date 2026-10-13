using BepInEx;
using BepInEx.Logging;
using BetterAmongUs.Utilities;

namespace BetterAmongUs.Modules;

/// <summary>
/// Provides logging for BetterAmongUs with various log levels and destinations.
/// </summary>
internal sealed class BAULogger(ManualLogSource manualLogSource)
{
    private readonly ManualLogSource _manualLogSource = manualLogSource;

    /// <summary>
    /// Logs a message with specified parameters.
    /// </summary>
    /// <param name="info">The message to log.</param>
    /// <param name="tag">The log tag/category.</param>
    /// <param name="logConsole">Whether to output to console.</param>
    /// <param name="color">The console color for the message.</param>
    /// <param name="hostOnly">Whether to log only when the client is host.</param>
    internal void Log(string info, string tag = "Log", bool logConsole = true, ConsoleColor color = ConsoleColor.White, bool hostOnly = false)
    {
        try
        {
            if (hostOnly && !GameState.IsHost)
                return;

            _manualLogSource.LogInfo($"[{tag}] {info}");
            if (logConsole)
            {
                ConsoleManager.SetConsoleColor(color);
                ConsoleManager.ConsoleStream.WriteLine($"{DateTime.Now:HH:mm} {BAUPlugin.ModInfo.PLUGIN_NAME}[{tag}]: {Utils.RemoveHtmlText(info)}");
            }
        }
        catch { }
    }

    /// <summary>
    /// Logs a header message with visual formatting.
    /// </summary>
    /// <param name="info">The header text.</param>
    /// <param name="tag">The log tag/category.</param>
    /// <param name="hostOnly">Whether to log only when the client is host.</param>
    /// <param name="logConsole">Whether to output to console.</param>
    internal void LogHeader(string info, string tag = "LogHeader", bool hostOnly = false, bool logConsole = true) => Log($"   >-------------- {info} --------------<", tag, hostOnly: hostOnly, logConsole: logConsole);

    /// <summary>
    /// Logs cheat detection messages with green console color.
    /// </summary>
    /// <param name="info">The cheat detection message.</param>
    /// <param name="tag">The log tag/category.</param>
    /// <param name="hostOnly">Whether to log only when the client is host.</param>
    /// <param name="logConsole">Whether to output to console.</param>
    internal void LogCheat(string info, string tag = "AntiCheat", bool hostOnly = false, bool logConsole = true) => Log(info, tag, color: ConsoleColor.Green, hostOnly: hostOnly, logConsole: logConsole);

    /// <summary>
    /// Logs error messages with red console color.
    /// </summary>
    /// <param name="info">The error message.</param>
    /// <param name="tag">The log tag/category.</param>
    /// <param name="hostOnly">Whether to log only when the client is host.</param>
    /// <param name="logConsole">Whether to output to console.</param>
    internal void Error(string info, string tag = "Error", bool hostOnly = false, bool logConsole = true) => Log(info, tag, color: ConsoleColor.Red, hostOnly: hostOnly, logConsole: logConsole);

    /// <summary>
    /// Logs exception details with red console color.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="tag">The log tag/category.</param>
    /// <param name="hostOnly">Whether to log only when the client is host.</param>
    /// <param name="logConsole">Whether to output to console.</param>
    internal void Error(Exception ex, string tag = "Error", bool hostOnly = false, bool logConsole = true) => Log(ex.ToString(), tag, color: ConsoleColor.Red, hostOnly: hostOnly, logConsole: logConsole);

    /// <summary>
    /// Logs warning messages with yellow console color.
    /// </summary>
    /// <param name="info">The warning message.</param>
    /// <param name="tag">The log tag/category.</param>
    /// <param name="hostOnly">Whether to log only when the client is host.</param>
    /// <param name="logConsole">Whether to output to console.</param>
    internal void Warning(string info, string tag = "Warning", bool hostOnly = false, bool logConsole = true) => Log(info, tag, color: ConsoleColor.Yellow, hostOnly: hostOnly, logConsole: logConsole);

    /// <summary>
    /// Logs a test message for debugging purposes.
    /// </summary>
    internal void Test()
    {
        Log("------------------> TEST <------------------", "TEST");
        InGame("TEST");
    }

    /// <summary>
    /// Logs a message in-game via the disconnect message notifier.
    /// </summary>
    /// <param name="info">The message to display in-game.</param>
    /// <param name="hostOnly">Whether to log only when the client is host.</param>
    internal void InGame(string info, bool hostOnly = false)
    {
        if (hostOnly && !GameState.IsHost)
            return;

        if (HudManager.InstanceExists) HudManager.Instance.Notifier.AddDisconnectMessage(info);
        Log(info, "InGame", hostOnly: hostOnly);
    }

    /// <summary>
    /// Logs a private message with encryption for sensitive data.
    /// </summary>
    /// <param name="info">The sensitive message to log.</param>
    /// <param name="tag">The log tag/category.</param>
    /// <param name="hostOnly">Whether to log only when the client is host.</param>
    internal void LogPrivate(string info, string tag = "Log", bool hostOnly = false)
    {
        try
        {
            if (hostOnly && !GameState.IsHost)
                return;

            string encrypted = Encryptor.Encrypt(info);
            Log(BAUPlugin.Constants.ENCRYPTED_LOG_PREFIX + encrypted + BAUPlugin.Constants.ENCRYPTED_LOG_POSTFIX, tag, false);
        }
        catch { }
    }

    // ---------------------------------------------------------------------------------------
    // Hidden-until-the-match-ends logging (GreaterAmongUs)
    // ---------------------------------------------------------------------------------------

    private readonly List<(string tag, string info, bool console)> _heldLines = [];
    private int _droppedHeldLines;
    private const int MaxHeldLines = 5000;

    /// <summary>
    /// True while a match is running (from the intro to the end of the game), not in the lobby
    /// and not in freeplay. The game switches to "Ended" before it reports the end of the match.
    /// </summary>
    internal static bool IsMatchRunning =>
        AmongUsClient.Instance != null
        && AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started
        && !GameState.IsFreePlay;

    /// <summary>
    /// Called when the match is over: everything held back is written.
    /// (Also called again from the end screen; it does nothing when there is nothing left.)
    /// </summary>
    internal void OnMatchEnd()
    {
        ReleaseHeldLogs();
    }

    /// <summary>
    /// Called when you leave or lose the connection. When the match ended normally everything was already written
    /// (<see cref="OnMatchEnd"/>), so anything still held means you left before the end: the match
    /// may still be running for the others, so those lines are thrown away instead of written.
    /// (Not decided from the game state: it is already reset when you leave.)
    /// </summary>
    internal void OnLeaveGame()
    {
        DiscardHeldLogs("Left the match before it ended");
    }

    /// <summary>
    /// Logs something that would give a player an advantage if seen during the match: who vented,
    /// shapeshifted, vanished, tried to kill or protect, ghost chat, Judge choices, or anything the
    /// anti-cheat quietly blocked without it being cheating.
    /// While a match is running the line is ONLY kept in memory - it is not shown in the console and
    /// not written to any file, so nobody can watch it live. It is written when the match ends
    /// (<see cref="ReleaseHeldLogs"/>). Outside a match it is written right away.
    /// Real cheat detections do not use this; they are shown live so the host can act.
    /// </summary>
    /// <param name="info">The line.</param>
    /// <param name="tag">The log tag.</param>
    /// <param name="showInConsole">When written: true = console and log file, false = encrypted log only (read it with /dump in the lobby).</param>
    /// <param name="hostOnly">Only log when the client is host (checked now, not when written).</param>
    internal void LogHidden(string info, string tag = "EventLog", bool showInConsole = true, bool hostOnly = false)
    {
        if (hostOnly && !GameState.IsHost)
            return;

        if (IsMatchRunning)
        {
            if (_heldLines.Count < MaxHeldLines)
                _heldLines.Add((tag, $"{DateTime.Now:HH:mm:ss} {info}", showInConsole));
            else
                _droppedHeldLines++;
            return;
        }

        WriteHidden(info, tag, showInConsole);
    }

    private void WriteHidden(string info, string tag, bool showInConsole)
    {
        if (showInConsole)
            LogCheat(info, tag);
        else
            LogPrivate(info, tag);
    }

    /// <summary>
    /// Writes everything that was held back during the match. Call only when the match is over.
    /// </summary>
    private void ReleaseHeldLogs()
    {
        if (_heldLines.Count == 0 && _droppedHeldLines == 0)
            return;

        var lines = _heldLines.ToList();
        int dropped = _droppedHeldLines;
        _heldLines.Clear();
        _droppedHeldLines = 0;

        LogHeader("Match log (hidden while the match was running)", "MatchLog");
        foreach (var (tag, info, showInConsole) in lines)
        {
            WriteHidden(info, tag, showInConsole);
        }
        if (dropped > 0)
        {
            Log($"{dropped} more lines were not kept (limit {MaxHeldLines})", "MatchLog");
        }
        LogHeader("Match log end", "MatchLog");
    }

    /// <summary>
    /// Throws away what was held back without writing it (only the number of lines is logged).
    /// </summary>
    private void DiscardHeldLogs(string reason)
    {
        int count = _heldLines.Count + _droppedHeldLines;
        _heldLines.Clear();
        _droppedHeldLines = 0;

        if (count > 0)
        {
            Log($"{reason}: {count} hidden log lines were discarded", "MatchLog");
        }
    }

    /// <summary>
    /// Decrypts all encrypted sections within a log file text.
    /// </summary>
    /// <param name="logText">The raw log file content containing encrypted sections marked with prefix and postfix characters.</param>
    /// <returns>The log text with all encrypted sections decrypted and markers removed.</returns>
    internal static string DecryptLogs(string logText)
    {
        string newLog = string.Empty;
        string[] logArray = logText.Split([Environment.NewLine], StringSplitOptions.None);

        foreach (string text in logArray)
        {
            if (text.Contains(BAUPlugin.Constants.ENCRYPTED_LOG_PREFIX) &&
                text.Contains(BAUPlugin.Constants.ENCRYPTED_LOG_POSTFIX))
            {
                int start = text.IndexOf(BAUPlugin.Constants.ENCRYPTED_LOG_PREFIX);
                int end = text.IndexOf(BAUPlugin.Constants.ENCRYPTED_LOG_POSTFIX);

                string before = text[..start];
                string encrypted = text.Substring(start + 1, end - start - 1);
                string after = text[(end + 1)..];

                try
                {
                    string decrypted = Encryptor.Decrypt(encrypted);
                    newLog += before + decrypted + after + "\n";
                }
                catch
                {
                    newLog += text + "\n";
                }
            }
            else
            {
                newLog += text + "\n";
            }
        }

        return newLog;
    }
}

/// <summary>
/// Custom log listener for BepInEx that forwards logs to the BetterAmongUs logging system.
/// </summary>
internal class CustomLogListener(BAULogger bauLogger) : ILogListener
{
    private readonly BAULogger _bauLogger = bauLogger;

    /// <summary>
    /// Gets or sets the log levels to filter.
    /// </summary>
    public LogLevel LogLevelFilter { get; set; } = LogLevel.Info | LogLevel.Warning | LogLevel.Error;

    /// <summary>
    /// Handles log events from BepInEx.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="eventArgs">The log event arguments.</param>
    public void LogEvent(object sender, LogEventArgs eventArgs)
    {
        // Skip Unity's own log spam AND events from our OWN log source. Our source is named
        // after the plugin GUID; if it isn't skipped, this listener re-logs its own output
        // forever and the game freezes. (Upstream matched the text "betteramongus", which no
        // longer matches after the GUID rename, so compare against the GUID itself.)
        if (eventArgs.Source.SourceName.ToLower().Contains("unity")
            || eventArgs.Source.SourceName.Equals(BAUPlugin.ModInfo.PLUGIN_GUID, StringComparison.OrdinalIgnoreCase))
            return;

        if (eventArgs.Level is LogLevel.None or LogLevel.Info)
        {
            _bauLogger.Log(eventArgs.Data.ToString(), "BepInEx." + eventArgs.Source.SourceName, logConsole: false);
        }
        else if (eventArgs.Level is LogLevel.Warning)
        {
            _bauLogger.Warning(eventArgs.Data.ToString(), "BepInEx." + eventArgs.Source.SourceName, logConsole: false);
        }
        else if (eventArgs.Level is LogLevel.Error or LogLevel.Fatal)
        {
            _bauLogger.Error(eventArgs.Data.ToString(), "BepInEx." + eventArgs.Source.SourceName, logConsole: false);
        }
    }

    /// <summary>
    /// Disposes the log listener.
    /// </summary>
    public void Dispose() { }

    /// <summary>
    /// Flushes the log listener.
    /// </summary>
    public void Flush() { }
}