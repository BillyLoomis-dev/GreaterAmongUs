using BetterAmongUs.Attributes;
using BetterAmongUs.Utilities;
using Hazel;

namespace BetterAmongUs.Modules.AntiCheat.RPCHandlers;

[RegisterRPCHandler]
internal sealed class StartAppearHandler : RPCHandler
{
    internal override byte CallId => (byte)RpcCalls.StartAppear;

    internal override bool HandleAntiCheatCancel(PlayerControl? sender, MessageReader reader)
    {
        bool shouldAnimate = reader.ReadBoolean();
        if (RegisterRPCHandlerAttribute.GetInstance<StartVanishHandler>().RoleCheck(sender) == false)
        {
            return false;
        }

        if (!shouldAnimate && (!sender.IsInVent() && !GameState.IsMeeting && !GameState.IsExilling))
        {
            // GreaterAmongUs: allowed and only logged. The game makes Phantoms appear without animation
            // when a meeting starts, and our meeting / vent state can be a moment behind, so this
            // flagged real Phantoms (and showed who they were).
            LogRpcInfo($"Phantom appeared without animation while not in vent (allowed, timing)");
        }

        return true;
    }
}
