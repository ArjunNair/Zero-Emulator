# Cross-platform port — status and decisions

Branch: `crossplatform`. Plan: [crossplatform-plan-v2.md](crossplatform-plan-v2.md). This file records
where the work stands and every place the implementation deliberately deviates from the plan.

## Layout

| Path | What | Target |
|---|---|---|
| `src/Speccy` | Emulation core (Z80, ULA, machines, tape, RZX) | net10.0, no UI deps |
| `src/Peripherals` | File formats (SZX/SNA/Z80/PZX/RZX/TAP), FDC stubs | net10.0 |
| `src/Zero.Emulation` | Host-neutral session: emulation thread, frame hand-off, input, tape deck, file loading, settings | net10.0 |
| `src/Zero.Sdl` | SDL3 audio output + gamepads (ppy.SDL3-CS ships the natives) | net10.0 |
| `src/Zero.App` | Avalonia desktop shell | net10.0 |
| `tests/Zero.Core.Tests` | xunit v2: zexall, machine boot, TAP, session, RZX | |
| `tests/Zero.App.Tests` | xunit v3 + Avalonia.Headless: real window, real key events, PNG screenshots | |

## Phase status

- **Phase 0** done. SDK-style projects, native DLLs and dead projects removed, FDC stubs return 0xFF,
  zlib.net → `System.IO.Compression.ZLibStream`, managed `TapFile` (byte-identical to tap2pzx).
- **Phase 1** done. Core has no `System.Windows.Forms` reference; errors go through `OnError` events.
  ZEXALL: 67/67 pass (`dotnet test --filter Category=Zexall`, ~20 min). All five machines boot to
  their firmware screens headless, including +3 and Pentagon with the disk stubs.
- **Phase 2/3** done, merged. `EmulatorSession` runs the machine on its own thread, paced by the audio
  sink (`IAudioOutput.FinishedPlaying`); triple-buffered `VideoFrame`s; positional key state; SDL3 audio
  and gamepads. Two full RZX recordings replay to the end with zero desync.
- **Phase 4** mostly done. The Avalonia shell boots, renders, takes keyboard input, loads files, has the
  v1 menu set (machine, tape, sound, view, input), a tape-deck window (block list, transport, options,
  PZX header metadata), an Options window (folders, ROM images, CPU multiplier, session and gamepad
  options), confirm-on-exit, restore-last-session, archive chooser, drag & drop, keyboard help and About.
  Verified by headless UI tests with screenshots. Missing: gamepad button remapping UI, Kempston mouse,
  LoadBinary, keyboard-layout picture, macOS native menu bar, file associations.
- **Phase 5** started. `dotnet publish` self-contained single-file works for all six RIDs
  (`packaging/README.md`); GitHub Actions workflow tests on three OSes and publishes artifacts;
  `packaging/macos/make-app.sh` builds `Zero.app`. Trimmed builds are 44 MB vs 109 MB and warning-free,
  but are not enabled in CI until someone runs one on a real display. Code signing / notarisation
  needs the Apple Developer identity and is documented, not automated.

## Deviations from the plan (and why)

1. **Speccy depended on DirectSound, not just WinForms.** `zx_spectrum` constructed
   `ZeroSound.SoundManager` directly. Fixed in phase 0 by introducing `Speccy.IAudioOutput`, passed into
   every machine constructor. Any host can now supply audio.
2. **Managed DirectX cannot run on modern .NET** (it is a .NET 1.1 mixed-mode assembly). So the plan's
   "WinForms app still runs" gate for phases 0–3 was unreachable; `ZeroWin` was kept compiling as a diff
   reference until the Avalonia shell worked, then deleted along with `ZiggySound` and `lib/mdx`
   (2026-09-13). The WinForms sources remain on `master` for reference when porting the remaining
   dialogs. The host abstraction went straight to SDL3 + Avalonia, verified through headless tests.
3. **Avalonia 12, not 11.** Avalonia 12.1 is current (Sept 2026); 11.x is no longer the supported line.
   The only 12-specific API touched so far is drag-and-drop (`DataTransfer`).
4. **`RZXFile` used `Application.LocalUserAppDataPath`** (WinForms) for temp files; now `Path.GetTempPath()`.
5. **ROM filenames were mixed-case on disk** (`48k.ROM`) while config defaulted to lowercase — invisible
   on Windows, fatal on macOS/Linux. Files renamed.
6. **Config stays JSON** but moves from Newtonsoft (`ZeroConfig`) to `System.Text.Json`
   (`EmulatorSettings`), stored under `%APPDATA%\Zero`, `~/Library/Application Support/Zero` or
   `$XDG_CONFIG_HOME/Zero`.

## Known gaps / v1 cut list (unchanged from the plan)

TZX/CSW tapes (converter needed), +3 and TR-DOS disks (managed FDCs needed), debugger, library browser,
BASIC importer (the old one shelled out to a Windows-only `zmakebas.exe`, now removed; a C# port of
zmakebas next to `TapFile` is the natural v1.1 replacement), file associations.

## Verifying

```bash
dotnet build Zero.sln
dotnet test tests/Zero.Core.Tests --filter "Category!=Zexall&Category!=Slow"
dotnet test tests/Zero.App.Tests            # headless UI; set ZERO_SCREENSHOT_DIR to keep PNGs
dotnet run --project src/Zero.App           # the emulator
```
