# GreaterAmongUs

> ## ⚠ Steam version of Among Us ONLY
>
> This build is compiled against the **Steam** version of Among Us. It will
> **not work** on Epic Games, Microsoft Store, itch.io, or any other
> distribution because the per-platform game libraries (`GameAssembly.dll`
> and the IL2CPP interop assemblies) differ.
>
> If you're not on Steam, **do not download** — the DLL will fail to load and
> the mod will do nothing. A separate Epic / MS Store build would require
> rebuilding against those platforms' game libraries; this fork does not
> ship one.

A client-sided anti-cheat / quality-of-life mod for Among Us, currently
targeting **AU 2026.6.5** on **Steam** (also compatible back to 2025.11.18).

This project is a **fork of [BetterAmongUs](https://github.com/D1GQ/BetterAmongUs)
by D1GQ**, distributed under the **GNU General Public License v3.0** in
accordance with the upstream license. See `LICENSE` for the full text.

## What's different from upstream BetterAmongUs

- **AU 2026.6.5 compatibility** — version array, build references, and
  Harmony bindings updated for current Among Us.
- **Vanilla-server safety** — the BAU custom-RPC handshake and per-target
  role-desync are gated off on official Innersloth servers. Both
  patterns trigger Innersloth's 2026.x anti-abuse system and result in
  the host getting banned from their own lobby; this fork avoids that.
- **No auto-kick / no auto-ban** — `AmongUsClient.KickPlayer` calls are
  commented out in the BAU `Kick` helper. Cheaters are still detected
  and logged; the host can manually kick via AU's native BanMenu.
- **Lobby warning popup** — when a player whose data is already in the
  local cheat list rejoins, a sticky popup shows their name, friend
  code, hashed PUID, and the prior detection reason. Dismiss with
  **CTRL+Y**.
- **Live-detection stack** — every fresh cheat detection pushes a new
  sticky popup onto a stack; previous popups are preserved underneath
  and revealed on dismiss. Dismiss with **CTRL+P**.
- **No bubble background on cheat popups** — the chat-bubble sprite is
  stripped from the alert clone so gameplay stays visible behind the text.
- **Additional cheat-client signatures** — adds detection for `GoatNet
  Client` (RPC 154), `ModMenuCrew` (RPC 201 and 205), plus a `HostGuard`
  presence notice (RPC 176, info-only).
- **Improved sabotage detection logging** — every cancelled sabotage RPC
  fires a popup naming the player and the specific anomaly (direct
  sabotage byte, remote fix, hold-while-not-sabotaged, etc.).
- **No audio alarm / no in-chat alerts** — removed in favor of the
  more visible always-on top-center popup.
- **Level-spoof threshold removed** — the old `DetectedLevelAbove`
  check false-flagged legit XP-glitch players. Only the reliable
  "client sent SetLevel twice in one session" check remains.

## Installation

### Option 1 — plugin only (if you already run BepInEx 6 IL2CPP)

1. Install BepInEx 6 IL2CPP for Among Us
2. Launch AU once with BepInEx to generate interop assemblies
3. Download `GreaterAmongUs-v1.4.1.dll` from the
   [latest release](https://github.com/BillyLoomis-dev/GreaterAmongUs/releases/latest)
   and drop it into `Among Us\BepInEx\plugins\`
4. Launch Among Us — version banner should read
   `GreaterAmongUs v1.4.1 …` in the main menu

### Option 2 — easy drag-and-drop bundle (no separate BepInEx install)

Best for most players — this all-in-one zip already includes BepInEx, so
you don't have to set anything up.

1. **Close Among Us** if it's running.
2. Download **`GreaterAmongUs-v1.4.1-Steam.AmongUs-Folder.zip`** from the
   [latest release](https://github.com/BillyLoomis-dev/GreaterAmongUs/releases/latest).
3. Open your Among Us folder — in Steam: right-click **Among Us → Manage →
   Browse local files** (usually
   `C:\Program Files (x86)\Steam\steamapps\common\Among Us`).
4. Extract **everything** from the zip into that folder. When Windows asks,
   choose **Replace the files in the destination** / **Merge folders** —
   you're only adding files, not deleting any.
5. Launch Among Us. The main menu should read `GreaterAmongUs v1.4.1`
   (a black console window may appear — that's normal, leave it open).

> **If the game opens as plain vanilla** — usually only right after an Among Us
> update — close it and open it once more. BepInEx regenerates its support
> files in the background and only needs to do that once.

The bundle only adds the `BepInEx` and `dotnet` folders plus
`winhttp.dll` / `doorstop_config.ini` / `.doorstop_version`; nothing else in
your game folder is touched. To uninstall, delete those and the game is fully
vanilla again. **Steam version only.**

GreaterAmongUs and BetterAmongUs use different `PLUGIN_GUID`s so they
*can* coexist, but you probably want only one loaded at a time to
avoid duplicate detections.

## Building from source

Project targets **.NET 6.0**. The csproj references interop assemblies
directly from your local BepInEx folder. Update the `<InteropDir>`
property in `BetterAmongUs.csproj` (or override on the command line
with `-p:InteropDir=...`) to point at your install's
`BepInEx\interop\` directory.

```bash
dotnet build -c Release
```

Output: `bin\Release\net6.0\BetterAmongUs.dll` — rename to
`GreaterAmongUs.dll` when deploying if you want the filename to match
the displayed mod name.

## License

GPL v3.0 (inherited from upstream BetterAmongUs). See `LICENSE` for the
full text. You are free to fork, modify, and redistribute under the
same license. You must:

- Keep the `LICENSE` file intact
- Preserve original copyright notices
- Make source code available for any binary distribution
- License derivatives under GPL v3.0 (or later)

## Credits

- **D1GQ** — original author of BetterAmongUs
  ([upstream repo](https://github.com/D1GQ/BetterAmongUs))
- This fork — bug fixes and feature changes listed above

## Disclaimer

**GreaterAmongUs** (and its parent project BetterAmongUs) is an
unofficial, fan-made mod for **Among Us**. It is not affiliated with,
endorsed by, or associated with **InnerSloth LLC** or the official
**Among Us** game. All trademarks and copyrights related to **Among
Us** are the property of **InnerSloth LLC**. This mod is created for
entertainment purposes only. Use at your own risk.
