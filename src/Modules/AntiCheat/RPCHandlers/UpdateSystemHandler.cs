using BetterAmongUs.Attributes;
using BetterAmongUs.Helpers;
using BetterAmongUs.Managers;
using Hazel;
using UnityEngine;

namespace BetterAmongUs.Modules.AntiCheat;

[RegisterRPCHandler]
internal sealed class UpdateSystemHandler : RPCHandler
{
    internal override byte CallId => (byte)RpcCalls.UpdateSystem;

    internal SystemTypes CatchedSystemType;

    private readonly Dictionary<uint, Func<PlayerControl?, ISystemType, MessageReader, byte, bool>> systemHandlers;

    private static SabotageSystemType SabotageSystem => ShipStatus.Instance.Systems[SystemTypes.Sabotage].Cast<SabotageSystemType>();

    internal UpdateSystemHandler()
    {
        systemHandlers = new Dictionary<uint, Func<PlayerControl?, ISystemType, MessageReader, byte, bool>>
        {
            { (uint)SystemTypes.Sabotage, (sender, system, reader, count) => HandleSabotageSystem(sender, system.Cast<SabotageSystemType>(), reader) },
            { (uint)SystemTypes.Ventilation, (sender, system, reader, count) => HandleVentilationSystem(sender, system.Cast<VentilationSystem>(), count) },
            { (uint)SystemTypes.Electrical, (sender, system, reader, count) => HandleSwitchSystem(sender, system.Cast<SwitchSystem>(), count) },
            { (uint)SystemTypes.Comms, (sender, system, reader, count) => HandleCommsSystem(sender, system, count) },
            { (uint)SystemTypes.MushroomMixupSabotage, (sender, system, reader, count) => HandleMushroomMixupSabotageSystem(sender, system.Cast<MushroomMixupSabotageSystem>(), count) },
            { (uint)SystemTypes.Doors, (sender, system, reader, count) => HandleDoorsSystem(sender, system.Cast<DoorsSystemType>(), count) },
            { (uint)SystemTypes.Reactor, (sender, system, reader, count) => HandleReactorSystem(sender, system.Cast<ReactorSystemType>(), count) },
            { (uint)SystemTypes.Laboratory, (sender, system, reader, count) => HandleReactorSystem(sender, system.Cast<ReactorSystemType>(), count) },
            { (uint)SystemTypes.HeliSabotage, (sender, system, reader, count) => HandleHeliSabotageSystem(sender, system.Cast<HeliSabotageSystem>(), count) },
            { (uint)SystemTypes.LifeSupp, (sender, system, reader, count) => HandleLifeSuppSystem(sender, system.Cast<LifeSuppSystemType>(), count) }
        };
    }

    internal static bool CheckConsoleDistance<T>(PlayerControl? player, float distance = 2f) where T : PlayerTask, new()
    {
        if (player == null) return false;

        var playerPos = player.GetCustomPosition();
        var consolesPos = new T().FindConsolesPos();

        foreach (var consolePos in consolesPos)
        {
            if (Vector2.Distance(consolePos, playerPos) < distance)
                return true;
        }

        return false;
    }

    internal override bool HandleAntiCheatCancel(PlayerControl? sender, MessageReader reader)
    {
        if (GameState.IsHost && sender.IsHost()) return true;

        MessageReader oldReader = MessageReader.Get(reader);
        byte count = reader.ReadByte();

        if (ShipStatus.Instance.Systems.TryGetValue(CatchedSystemType, out ISystemType system))
        {
            uint systemKey = (uint)CatchedSystemType;

            if (systemHandlers.TryGetValue(systemKey, out var handler))
            {
                oldReader.Recycle();
                return handler.Invoke(sender, system, oldReader, count);
            }
        }

        oldReader.Recycle();

        return true;
    }

    // Helper: cancel an invalid sabotage/fix RPC AND surface it as a cheat detection
    // (popup + horn + chat + persistent CheatData entry + auto-kick).
    // Without this, UpdateSystemHandler used to silently drop the RPC with only a
    // generic log line ("RPC canceled by Anti-Cheat: Electrical - 0") and no name,
    // no audio, no popup — the user couldn't tell who did what.
    private static bool Flag(PlayerControl? sender, string what)
    {
        if (sender != null && sender.Data != null)
        {
            BetterNotificationManager.NotifyCheat(sender, $"Invalid sabotage/fix: {what}");
        }
        return false;
    }

    private static bool HandleSabotageSystem(PlayerControl? sender, SabotageSystemType sabotageSystem, MessageReader reader)
    {
        byte count = reader.ReadByte();

        if (!sender.IsImpostorTeam())
        {
            return Flag(sender, "non-impostor triggered sabotage");
        }

        if (sabotageSystem.Timer > 0f)
        {
            return Flag(sender, "sabotage during cooldown");
        }

        return true;
    }

    private static bool HandleVentilationSystem(PlayerControl? sender, VentilationSystem ventilationSystem, byte count)
    {

        return true;
    }

    private static bool HandleSwitchSystem(PlayerControl? sender, SwitchSystem switchSystem, byte count)
    {
        if (count == 128) // Direct sabotage call from client, which is not possible, only the host should have this count when HandleSabotageSystem it's called
        {
            return Flag(sender, "Electrical: direct-sabotage byte from client");
        }

        if (!switchSystem.IsActive)
        {
            return Flag(sender, "Electrical: flip while not sabotaged");
        }

        if (!CheckConsoleDistance<ElectricTask>(sender))
        {
            return Flag(sender, "Electrical: not at console (remote fix)");
        }

        return true;
    }

    private static bool HandleCommsSystem(PlayerControl? sender, ISystemType system, byte count)
    {
        if (system == null) return false;

        try
        {
            var hqHudSystem = system.Cast<HqHudSystemType>();
            return HandleHqHudSystem(sender, hqHudSystem, count);
        }
        catch
        {

        }

        try
        {
            var hudOverrideSystem = system.Cast<HudOverrideSystemType>();
            return HandleHudOverrideSystem(sender, hudOverrideSystem, count);
        }
        catch
        {

        }

        return true;
    }

    private static bool HandleHqHudSystem(PlayerControl? sender, HqHudSystemType hqHudSystem, byte count)
    {
        if ((count & 128) > 0)
        {
            return Flag(sender, "Comms (HQ): direct-sabotage byte from client");
        }
        if (!hqHudSystem.IsActive)
        {
            return Flag(sender, "Comms (HQ): flip while not sabotaged");
        }
        if (!CheckConsoleDistance<HqHudOverrideTask>(sender, 2f))
        {
            return Flag(sender, "Comms (HQ): not at console (remote fix)");
        }
        return true;
    }

    private static bool HandleHudOverrideSystem(PlayerControl? sender, HudOverrideSystemType hudOverrideSystem, byte count)
    {
        if (count == 128)
        {
            return Flag(sender, "Comms: direct-sabotage byte from client");
        }
        if (!hudOverrideSystem.IsActive)
        {
            return Flag(sender, "Comms: flip while not sabotaged");
        }
        if (!CheckConsoleDistance<HudOverrideTask>(sender, 2f))
        {
            return Flag(sender, "Comms: not at console (remote fix)");
        }
        return true;
    }

    private static bool HandleMushroomMixupSabotageSystem(PlayerControl? sender, MushroomMixupSabotageSystem mushroomMixupSabotage, byte count)
    {
        if (count == 1)
        {
            return Flag(sender, "MushroomMixup: direct-sabotage byte from client");
        }
        if (mushroomMixupSabotage.IsActive)
        {
            return Flag(sender, "MushroomMixup: triggered while already active");
        }
        return true;
    }

    private static bool HandleDoorsSystem(PlayerControl? sender, DoorsSystemType doorsSystem, byte count)
    {
        if (count == 128)
        {
            return Flag(sender, "Doors: direct-sabotage byte from client");
        }
        return true;
    }

    private static bool HandleReactorSystem(PlayerControl? sender, ReactorSystemType reactorSystem, byte count)
    {
        if (count == 128 || count == 16)
        {
            return Flag(sender, "Reactor: direct-sabotage byte from client");
        }
        if (!reactorSystem.IsActive)
        {
            return Flag(sender, "Reactor: hold while not sabotaged");
        }
        if (count.HasAnyBit(64))
        {
            foreach (var tuple in reactorSystem.UserConsolePairs)
            {
                if (tuple.Item1 == sender.PlayerId)
                {
                    return Flag(sender, "Reactor: duplicate hand-hold from same player");
                }
            }
        }
        return true;
    }

    private static bool HandleHeliSabotageSystem(PlayerControl? sender, HeliSabotageSystem heliSabotageSystem, byte count)
    {
        if (count == 128)
        {
            return Flag(sender, "HeliSabotage: direct-sabotage byte from client");
        }
        if (!heliSabotageSystem.IsActive)
        {
            return Flag(sender, "HeliSabotage: hold while not sabotaged");
        }
        if (!CheckConsoleDistance<HeliCharlesTask>(sender))
        {
            return Flag(sender, "HeliSabotage: not at console (remote fix)");
        }
        return true;
    }

    private static bool HandleLifeSuppSystem(PlayerControl? sender, LifeSuppSystemType lifeSuppSystem, byte count)
    {
        if (count == 128)
        {
            return Flag(sender, "LifeSupp: direct-sabotage byte from client");
        }
        if (!lifeSuppSystem.IsActive)
        {
            return Flag(sender, "LifeSupp: input while not sabotaged");
        }
        return true;
    }
}