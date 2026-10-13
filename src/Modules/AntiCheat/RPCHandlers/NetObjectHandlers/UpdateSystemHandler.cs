using BetterAmongUs.Attributes;
using BetterAmongUs.Data.Config;
using BetterAmongUs.Managers;
using BetterAmongUs.Modules.Support;
using BetterAmongUs.MonoScripts.Extended;
using BetterAmongUs.Utilities;
using Hazel;
using UnityEngine;

namespace BetterAmongUs.Modules.AntiCheat.RPCHandlers.NetObjectHandlers;

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
            { (uint)SystemTypes.Sabotage, (sender, system, reader, count) => HandleSabotageSystem(sender, system.Cast<SabotageSystemType>()) },
            { (uint)SystemTypes.Ventilation, (sender, system, reader, count) => HandleVentilationSystem(sender, system.Cast<VentilationSystem>(), reader) },
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
        return GetConsoleDistance<T>(player) < distance;
    }

    /// <summary>
    /// Distance from the player to the nearest console of the task type (float.MaxValue if unknown).
    /// </summary>
    internal static float GetConsoleDistance<T>(PlayerControl? player) where T : PlayerTask, new()
    {
        if (player == null) return float.MaxValue;

        var playerPos = player.GetCustomPosition();
        var consolesPos = new T().FindConsolesPos();

        float best = float.MaxValue;
        foreach (var consolePos in consolesPos)
        {
            best = Math.Min(best, Vector2.Distance(consolePos, playerPos));
        }

        return best;
    }

    // Beyond this distance a fix can't be explained by network lag (the use range is ~1 and
    // a lagging position is at most a couple of units behind): it was done remotely.
    private const float RemoteFixDistance = 6f;

    /// <summary>
    /// GreaterAmongUs: cancel AND report something a normal client can never send
    /// (alert + saved to cheat data). Upstream only dropped the RPC silently.
    /// </summary>
    private static bool Flag(PlayerControl? sender, string what)
    {
        bool report = BAUConfigs.AntiCheat.Value && !BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_Anticheat);
        if (report && sender != null && sender.Data != null)
        {
            BetterNotificationManager.NotifyCheat(sender, $"Invalid sabotage/fix: {what}");
        }
        else
        {
            Block(sender, what);
        }

        return false;
    }

    /// <summary>
    /// GreaterAmongUs: cancel something that can happen by accident (two players pressing at
    /// the same moment, lag). Logged with the player's name, but not reported as cheating.
    /// </summary>
    private static bool Block(PlayerControl? sender, string what)
    {
        string who = sender != null ? (sender.ExtendedData()?.RealName ?? sender.Data?.PlayerName ?? "?") : "?";
        BAUPlugin.Logger.LogCheat($"{who} - sabotage/fix canceled ({what})");
        return false;
    }

    /// <summary>
    /// Console check: too far for lag = cheat; a little too far = just canceled.
    /// </summary>
    private static bool CheckFixDistance<T>(PlayerControl? sender, string systemName, float distance = 2f) where T : PlayerTask, new()
    {
        float dist = GetConsoleDistance<T>(sender);
        if (dist < distance)
            return true;

        if (dist != float.MaxValue && dist > RemoteFixDistance)
            return Flag(sender, $"{systemName}: fixed from far away ({dist:0.0} units)");

        return Block(sender, $"{systemName}: not at the console ({dist:0.0} units)");
    }

    internal override bool HandleAntiCheatCancel(PlayerControl? sender, MessageReader reader)
    {
        if (sender.IsHost()) return true;

        byte count = reader.ReadByte();

        if (ShipStatus.Instance.Systems.TryGetValue(CatchedSystemType, out ISystemType system))
        {
            uint systemKey = (uint)CatchedSystemType;

            if (systemHandlers.TryGetValue(systemKey, out var handler))
            {
                return handler.Invoke(sender, system, reader, count);
            }
        }

        return true;
    }

    private static bool HandleSabotageSystem(PlayerControl? sender, SabotageSystemType sabotageSystem)
    {
        if (!sender.IsImpostorTeam())
        {
            return Flag(sender, "non-impostor triggered a sabotage");
        }

        if (sabotageSystem.Timer > 0f)
        {
            return Block(sender, "sabotage still on cooldown");
        }

        return true;
    }

    private static bool HandleVentilationSystem(PlayerControl? sender, VentilationSystem ventilationSystem, MessageReader reader)
    {
        _ = reader.ReadUInt16();
        var operation = (VentilationSystem.Operation)reader.ReadByte();
        if (operation == VentilationSystem.Operation.BootImpostors)
        {
            return Flag(sender, "Vents: kick-out-of-vents (host only)");
        }

        return true;
    }

    private static bool HandleSwitchSystem(PlayerControl? sender, SwitchSystem switchSystem, byte count)
    {
        if (count == 128) // Direct sabotage call from client, which is not possible, only the host should have this count when HandleSabotageSystem it's called
        {
            return Flag(sender, "Lights: direct sabotage from a client");
        }

        if (!switchSystem.IsActive)
        {
            return Block(sender, "Lights: switch flipped while lights not sabotaged");
        }

        if (!CheckFixDistance<ElectricTask>(sender, "Lights"))
        {
            return false;
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
        if ((count & 128) > 0) // Direct sabotage call from client, which is not possible, only the host should have this count when HandleSabotageSystem it's called
        {
            return Flag(sender, "Comms: direct sabotage from a client");
        }

        if (!hqHudSystem.IsActive)
        {
            return Block(sender, "Comms: fix while comms not sabotaged");
        }

        if (!CheckFixDistance<HqHudOverrideTask>(sender, "Comms"))
        {
            return false;
        }

        return true;
    }

    private static bool HandleHudOverrideSystem(PlayerControl? sender, HudOverrideSystemType hudOverrideSystem, byte count)
    {
        if (count == 128) // Direct sabotage call from client, which is not possible, only the host should have this count when HandleSabotageSystem it's called
        {
            return Flag(sender, "Comms: direct sabotage from a client");
        }

        if (!hudOverrideSystem.IsActive)
        {
            return Block(sender, "Comms: fix while comms not sabotaged");
        }

        if (!CheckFixDistance<HudOverrideTask>(sender, "Comms"))
        {
            return false;
        }

        return true;
    }

    private static bool HandleMushroomMixupSabotageSystem(PlayerControl? sender, MushroomMixupSabotageSystem mushroomMixupSabotage, byte count)
    {
        if (count == 1) // Direct sabotage call from client, which is not possible, only the host should have this count when HandleSabotageSystem it's called
        {
            return Flag(sender, "Mushroom Mixup: direct sabotage from a client");
        }

        if (mushroomMixupSabotage.IsActive)
        {
            return Block(sender, "Mushroom Mixup: already active");
        }

        return true;
    }

    private static bool HandleDoorsSystem(PlayerControl? sender, DoorsSystemType doorsSystem, byte count)
    {
        if (count == 128) // Direct sabotage call from client, which is not possible, only the host should have this count when HandleSabotageSystem it's called
        {
            return Flag(sender, "Doors: direct sabotage from a client");
        }

        return true;
    }

    private static bool HandleReactorSystem(PlayerControl? sender, ReactorSystemType reactorSystem, byte count)
    {
        if (count == 128 || count == 16) // Direct sabotage call from client, which is not possible, only the host should have this count when HandleSabotageSystem it's called
        {
            return Flag(sender, "Reactor: direct sabotage from a client");
        }

        if (!reactorSystem.IsActive)
        {
            return Block(sender, "Reactor: fix while reactor not sabotaged");
        }

        if (count.HasAnyBit(64))
        {
            foreach (var tuple in reactorSystem.UserConsolePairs)
            {
                if (tuple.Item1 == sender.PlayerId)
                {
                    return Block(sender, "Reactor: already holding a panel");
                }
            }
        }

        /*
          if (!CheckConsoleDistance<ReactorTask>(sender))
          {
              return false;
          }
         */

        return true;
    }

    private static bool HandleHeliSabotageSystem(PlayerControl? sender, HeliSabotageSystem heliSabotageSystem, byte count)
    {
        if (count == 128) // Direct sabotage call from client, which is not possible, only the host should have this count when HandleSabotageSystem it's called
        {
            return Flag(sender, "Crash course: direct sabotage from a client");
        }

        if (!heliSabotageSystem.IsActive)
        {
            return Block(sender, "Crash course: fix while not sabotaged");
        }

        if (!CheckFixDistance<HeliCharlesTask>(sender, "Crash course"))
        {
            return false;
        }

        return true;
    }

    private static bool HandleLifeSuppSystem(PlayerControl? sender, LifeSuppSystemType lifeSuppSystem, byte count)
    {
        if (count == 128) // Direct sabotage call from client, which is not possible, only the host should have this count when HandleSabotageSystem it's called
        {
            return Flag(sender, "O2: direct sabotage from a client");
        }

        if (!lifeSuppSystem.IsActive)
        {
            return Block(sender, "O2: fix while O2 not sabotaged");
        }

        /*
        if (!CheckConsoleDistance<NoOxyTask>(sender))
        {
            return false;
        }
        */

        return true;
    }
}