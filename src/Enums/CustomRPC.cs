namespace BetterAmongUs.Enums;

/// <summary>
/// Defines custom Remote Procedure Call (RPC) identifiers used in BetterAmongUs for cheat detection and communication.
/// </summary>
internal enum CustomRPC : int
{
    // Cheat RPC's
    /// <summary>
    /// RPC identifier for Sicko cheat detection.
    /// </summary>
    Sicko = 420, // Results in 164

    /// <summary>
    /// RPC identifier for AUM (Among Us Menu) cheat detection.
    /// </summary>
    AUM = 42069, // Results in 85

    /// <summary>
    /// RPC identifier for AUM chat communication.
    /// </summary>
    AUMChat = 101,

    /// <summary>
    /// RPC identifier for KillNetwork cheat detection.
    /// </summary>
    KillNetwork = 250,

    /// <summary>
    /// RPC identifier for KillNetwork chat communication.
    /// </summary>
    KillNetworkChat = 119,

    /// <summary>
    /// RPC identifier for GoatNetClient cheat detection. Original symbolic value 666; wire byte 154.
    /// </summary>
    GoatNet = 666, // Results in 154

    /// <summary>
    /// RPC identifier for HostGuard mod detection. HostGuard is a host-side anti-cheat,
    /// not a cheat itself — treated as info-only (notify but don't flag/kick).
    /// </summary>
    HostGuard = 176,

    /// <summary>
    /// RPC identifier for ModMenuCrew chat-bypass cheat signature.
    /// </summary>
    ModMenuCrewChatBypass = 201,

    /// <summary>
    /// RPC identifier for ModMenuCrew role-validation cheat signature.
    /// </summary>
    ModMenuCrewRoleValidation = 205,


    // Better Among Us
    /// <summary>
    /// Legacy RPC for BetterAmongUs checks (currently unused).
    /// </summary>
    LegacyBetterCheck = 150, // Unused
    /// <summary>
    /// RPC for sending a shared secret to another player.
    /// </summary>
    SendSecretToPlayer,
    /// <summary>
    /// RPC for checking the hash of a shared secret received from another player.
    /// </summary>
    CheckSecretHashFromPlayer,
}