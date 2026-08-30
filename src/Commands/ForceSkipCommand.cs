using BetterAmongUs.Attributes;
using BetterAmongUs.Modules;

namespace BetterAmongUs.Commands;

[RegisterCommand]
internal sealed class ForceSkipCommand : BaseCommand
{
    internal override string Name => "forceskip";
    internal override string Description => "Force skips a meeting in progress";
    internal override bool CanRunCommand(out string reason)
    {
        if (!GameState.IsHost)
        {
            reason = "Can only run as host";
            return false;
        }

        if (!GameState.IsMeeting)
        {
            reason = "Can only run in meeting";
            return false;
        }

        return base.CanRunCommand(out reason);
    }

    internal override void Run()
    {
        if (GameState.IsHost)
        {
            foreach (var client in AmongUsClient.Instance.allClients)
            {
                MeetingHud.Instance.RpcClearVote((byte)client.Id);
            }
            MeetingHud.Instance.RpcClose();
        }
    }
}
