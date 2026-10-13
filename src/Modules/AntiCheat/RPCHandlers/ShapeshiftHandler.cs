using AmongUs.GameOptions;
using BetterAmongUs.Attributes;
using BetterAmongUs.Utilities;
using BetterAmongUs.Managers;
using Hazel;
using InnerNet;

namespace BetterAmongUs.Modules.AntiCheat.RPCHandlers;

[RegisterRPCHandler]
internal sealed class ShapeshiftHandler : RPCHandler
{
    internal override byte CallId => (byte)RpcCalls.Shapeshift;

    internal override bool HandleAntiCheatCancel(PlayerControl? sender, MessageReader reader)
    {
        var target = reader.ReadNetObject<PlayerControl>();
        var flag = reader.ReadBoolean();

        if (!sender.Is(RoleTypes.Shapeshifter) || !sender.IsAlive())
        {
            if (BetterNotificationManager.NotifyCheat(sender, GetFormatActionText()))
            {
                string issue = GetShapeshiftRoleIssue(sender);
                LogRpcInfo($"Invalid shapeshift: {issue}");
            }
            return false;
        }
        else if (!flag && !GameState.IsMeeting && !GameState.IsExilling && !sender.IsInVent())
        {
            // GreaterAmongUs: allowed and only logged. The host decides the animation, and the meeting /
            // vent state we see can be a moment behind (meeting just ended, vent just left), so this
            // flagged real Shapeshifters and showed who they were.
            LogRpcInfo($"Shapeshift without animation while not in vent (allowed, timing)");
        }

        return true;
    }

    private static string GetShapeshiftRoleIssue(PlayerControl sender)
    {
        if (!sender.Is(RoleTypes.Shapeshifter)) return "Sender not Shapeshifter role";
        if (!sender.IsAlive()) return "Sender not alive";

        return "Unknown shapeshift role issue";
    }
}
