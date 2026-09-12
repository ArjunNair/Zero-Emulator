# Cross-platform port — status and decisions

Branch: `crossplatform`. Plan: [crossplatform-plan-v2.md](crossplatform-plan-v2.md). This file records
where the work stands and every place the implementation deliberately deviates from the plan.

## Layout

| Path | What | Target |
|---|---|---|
| `Ziggy/Speccy` | Emulation core (Z80, ULA, machines, tape, RZX) | net8.0, no UI deps |
| `Ziggy/Peripherals` | File formats (SZX/SNA/Z80/PZX/RZX/TAP), FDC stubs | net8.0 |
| `src/Zero.Emulation` | Host-neutral session: emulation thread, frame hand-off, input, tape deck, file loading, settings | net8.0 |
| `src/Zero.Sdl` | SDL3 audio output + gamepads (ppy.SDL3-CS ships the natives) | net8.0 |
| `src/Zero.App` | Avalonia desktop shell | net8.0 |
| `Ziggy/ZiggySound`, `ZiggyWin` | Legacy DirectSound/WinForms shell, compile-only reference | net8.0-windows |
| `tests/Zero.Core.Tests` | xunit v2: zexall, machine boot, TAP, session, RZX | |
| `tests/Zero.App.Tests` | xunit v3 + Avalonia.Headless: real window, real key events, PNG screenshots | |
| `lib/mdx` | Managed DirectX + Microsoft.VisualC reference assemblies (compile-time only) | |

## Phase status

- **Phase 0** done. SDK-style projects, native DLLs and dead projects removed, FDC stubs return 0xFF,
  zlib.net → `System.IO.Compression.ZLibStream`, managed `TapFile` (byte-identical to tap2pzx).
- **Phase 1** done. Core has no `System.Windows.Forms` reference; errors go through `OnError` events.
  ZEXALL: 67/67 pass (`dotnet test --filter Category=Zexall`, ~20 min). All five machines boot to
  their firmware screens headless, including +3 and Pentagon with the disk stubs.
- **Phase 2/3** done, merged. `EmulatorSession` runs the machine on its own thread, paced by the audio
  sink (`IAudioOutput.FinishedPlaying`); triple-buffered `VideoFrame`s; positional key state; SDL3 audio
  and gamepads. Two full RZX recordings replay to the end with zero desync.
- **Phase 4** in progress. The Avalonia shell boots, renders, takes keyboard input, loads files, has the
  v1 menu set (machine, tape deck controls, sound, view, input). Missing: a tape-deck window with block
  list, options dialog beyond menu toggles, joystick button remapping UI, keyboard-help picture,
  About box polish, macOS native menu, file associations.
- **Phase 5** not started.

## Deviations from the plan (and why)

1. **Speccy depended on DirectSound, not just WinForms.** `zx_spectrum` constructed
   `ZeroSound.SoundManager` directly. Fixed in phase 0 by introducing `Speccy.IAudioOutput`, passed into
   every machine constructor. Any host can now supply audio.
2. **Managed DirectX cannot run on .NET 8** (it is a .NET 1.1 mixed-mode assembly). So the plan's "WinForms
   app still runs" gate for phases 0–3 is unreachable on .NET 8; `ZeroWin` is kept *compiling* only, as a
   diff reference. The runnable baseline is the original `master` on .NET Framework. Consequently the
   host abstraction was not implemented over DirectX first; it went straight to SDL3 + Avalonia, verified
   through headless tests instead of the WinForms app.
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
BASIC importer (relied on a Windows `zmakebas.exe`), file associations.

## Verifying

```bash
dotnet build Zero.sln
dotnet test tests/Zero.Core.Tests --filter "Category!=Zexall&Category!=Slow"
dotnet test tests/Zero.App.Tests            # headless UI; set ZERO_SCREENSHOT_DIR to keep PNGs
dotnet run --project src/Zero.App           # the emulator
```
