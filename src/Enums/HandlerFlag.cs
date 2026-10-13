namespace BetterAmongUs.Enums;

/// <summary>
/// Defines handler flags used to control the processing flow of network messages and cheat detection.
/// </summary>
internal enum HandlerFlag
{
    /// <summary>
    /// Indicates the message should be handled normally.
    /// </summary>
    Handle,

    /// <summary>
    /// Indicates the message should be checked for cheat RPC signatures.
    /// </summary>
    CheatRpcCheck,

    /// <summary>
    /// Indicates that cheat detection processing should be cancelled.
    /// </summary>
    AntiCheatCancel,

    /// <summary>
    /// Indicates the message should be processed by the anti-cheat system.
    /// </summary>
    AntiCheat,

    /// <summary>
    /// Indicates the host is using BetterAmongUs.
    /// </summary>
    BetterHost,

    /// <summary>
    /// Indicates the message contains GameData tag information.
    /// </summary>
    HandleGameDataTag
}