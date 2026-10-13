# GreaterAmongUs

A client-sided anti-cheat and quality-of-life mod for **Among Us** (64-bit, **2026.9.29**), made for the
**Steam** version.

GreaterAmongUs is a **fork of [BetterAmongUs](https://github.com/D1GQ/BetterAmongUs) by D1GQ**, now based on
BetterAmongUs **1.3.4**, distributed under the **GNU General Public License v3.0** like the original.
See `LICENSE` for the full text.

## What GreaterAmongUs adds to BetterAmongUs

Everything BetterAmongUs 1.3.4 has, plus:

### Judge and Influencer
- **Dead players see who judged who.** During a meeting a dead player sees `(judging Bob)` on the Judge and
  `(being judged by Alice)` on the target. After the meeting it reads `(judged Bob)` / `(judged by Alice)`
  for the rest of the game, above heads and in meetings. The Judge always sees their own choice.
  Living players never see it, and neither does the Influencer (their pictures reach living players).
  The end-of-game summary shows it to everyone.
  BetterAmongUs only recorded this on the clients where the overrule screen ran and never displayed it;
  GreaterAmongUs reads the overrule list the host sends to every player, so it works for everyone.
- **Fake Judge overrule** blocked and reported: a vote overrule from a player who isn't a living Judge.
  Checked against the role the host assigned, so a cheat that only changes its own role can't pass.
- **Forged meeting result** blocked (host): a non-host sending `VotingComplete` (force skip / force eject) or
  `CloseMeeting` (force-close the meeting).
- **Forged Judge result** reported (non-host): a meeting result that claims a Judge overrule when no living
  Judge is in the game.
- **Fake Influencer message** blocked: picture messages that arrive while nobody in the game is an Influencer.
  These four meeting/map checks never ran in BetterAmongUs (those messages bypassed its anti-cheat).
- Dead players and Guardian Angels see a dead player's real role (a dead Judge shows `Judge`, a Judge who
  became a Guardian Angel shows `Judge > Guardian Angel`), also in the end-of-game summary.

### Host safety
- **No automatic kicks or bans.** Innersloth's anti-abuse system has banned the *host* after automatic kicks,
  so detections alert and are saved instead. Your own `/kick` command and the game's kick/ban buttons still work.
  When an automatic kick is blocked for a non-cheat reason (ban chat list, minimum level), you get a warning.
- **No handshake on official servers.** The BetterAmongUs handshake is never sent on Innersloth servers
  (they disconnected players for it). `Send Greater RPC` is off by default.
- Players who take over 25 seconds to load are no longer left invisible; you get a warning instead.

### Alerts
- **Sticky cheat alerts.** Each detection stays on screen (up to 60 minutes) and stacks, newest on top.
  **CTRL+P** dismisses a cheat alert. The same player caught again for the same thing shows `(x2)`, `(x3)`...
  instead of burying other alerts.
- **Known cheater joined.** When a player saved in your cheat data joins, you see their name, friend code,
  hashed PUID and the old reason. **CTRL+Y** dismisses it. Works as host *and* as a normal player.
- **Ban lists warn instead of kick.** A player on your ban player list / ban name list gets a CTRL+Y warning.
- Alerts you didn't dismiss come back in the next lobby. No alert sounds.
- "Unregistered RPC" is reported once per player and RPC, not on every message.

### More detections
- **GoatNet** client (RPC 154), **ModMenuCrew** role signature (RPC 205; BetterAmongUs already catches 201/202),
  **HostGuard** presence notice (RPC 176, info only, never flagged).
- **Sabotage cheats are named.** A sabotage/fix that a normal client can never send (a crewmate sabotaging,
  a "direct sabotage" byte, kicking everyone out of vents, fixing from far away) is reported with the player's
  name and what they did. Things that can happen by accident (two players pressing at the same moment, lag)
  are still blocked but only logged, so nobody gets flagged for a timing race.
- The level check stays opt-in (off by default): real players can be above level 1000 from old XP glitches.

### Smaller things
- Lazily-built role list fixed: roles added in a game update (like Judge) no longer show as missing.
- New roles automatically use the game's own role color.
- Logger no longer freezes the game after the plugin rename (it used to re-log its own output forever).
- Chat during gameplay is off by default.
- Own data folder `Greater_Data`, own plugin id, in-game updater and news read this repository.

## Installation

### Option 1 - plugin only (you already run BetterAmongUs or BepInEx 6 IL2CPP)

1. Close Among Us.
2. Download `GreaterAmongUs.dll` from the
   [latest release](https://github.com/BillyLoomis-dev/GreaterAmongUs/releases/latest).
3. Put it in `Among Us\BepInEx\plugins\`. Remove `BetterAmongUs.dll` and any older GreaterAmongUs DLL
   from that folder so only one is loaded.
4. Start Among Us. The main menu shows `♻GAU♻ v1.5.0`.

### Option 2 - everything in one zip (no BepInEx yet)

1. Close Among Us.
2. Download the BetterAmongUs 1.3.4 zip for your platform from the
   [BetterAmongUs releases](https://github.com/D1GQ/BetterAmongUs/releases) and extract **everything** into your
   Among Us folder (Steam: right-click Among Us > Manage > Browse local files).
3. In `Among Us\BepInEx\plugins\`, replace `BetterAmongUs.dll` with `GreaterAmongUs.dll` (Option 1, step 2).
4. Start Among Us. **The first start can open plain Among Us while BepInEx sets itself up - close it and start
   it again.** You only do this once.

To uninstall, delete `BepInEx`, `dotnet`, `winhttp.dll` and `doorstop_config.ini` from the game folder.

## Building from source

Requirements: .NET SDK 9 or newer (the project targets .NET 6), internet for NuGet.

```bash
cd src
dotnet build -c Release
```

The DLL is `src\bin\Release\net6.0\GreaterAmongUs.dll`.

By default the game libraries come from the `AmongUs.GameLibs.Steam` 2026.9.29 NuGet package. If a game update
is out before that package, build against your own game instead (start Among Us once with BepInEx first):

```bash
dotnet build -c Release -p:InteropDir="C:\Program Files (x86)\Steam\steamapps\common\Among Us\BepInEx\interop"
```

To have the DLL copied into your game after each build, put your `BepInEx\plugins` folder path in
`src\build\mod_folder_path`.

## License

GPL v3.0, inherited from BetterAmongUs. See `LICENSE`. You may fork, modify and redistribute under the same
license: keep `LICENSE`, keep copyright notices, publish the source of any binary you distribute, and license
derivatives under GPL v3.0 (or later).

## Credits

- **D1GQ** and the BetterAmongUs contributors - the original mod
  ([upstream repository](https://github.com/D1GQ/BetterAmongUs))
- **BillyLoomis-dev** - GreaterAmongUs

## Disclaimer

GreaterAmongUs (and BetterAmongUs) is an unofficial, fan-made mod for **Among Us**. It is not affiliated with,
endorsed by, or associated with **Innersloth LLC**. All Among Us trademarks and copyrights belong to
Innersloth LLC. Use at your own risk.
