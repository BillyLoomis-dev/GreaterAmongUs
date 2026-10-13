using Hazel;
using InnerNet;

namespace BetterAmongUs.Structs;

/// <summary>
/// Represents data for an RPC (Remote Procedure Call) message.
/// </summary>
/// <param name="sender">The object that sent the RPC.</param>
/// <param name="sendOption">The send option used for the RPC.</param>
/// <param name="targetId">The target player ID for the RPC.</param>
/// <param name="callId">The ID of the RPC call.</param>
/// <param name="reader">The message reader containing the RPC data.</param>
internal readonly struct RPCData(InnerNetObject sender, SendOption sendOption, int targetId, RpcCalls callId, MessageReader reader)
{
    /// <summary>
    /// Gets the object that sent the RPC.
    /// </summary>
    public readonly InnerNetObject Sender = sender;

    /// <summary>
    /// Gets the send option used for the RPC.
    /// </summary>
    public readonly SendOption SendOption = sendOption;

    /// <summary>
    /// Gets the target player ID for the RPC.
    /// </summary>
    public readonly int TargetId = targetId;

    /// <summary>
    /// Gets the ID of the RPC call.
    /// </summary>
    public readonly RpcCalls CalledRpc = callId;

    /// <summary>
    /// Gets the message reader containing the RPC data.
    /// </summary>
    public readonly MessageReader Reader = reader;
}