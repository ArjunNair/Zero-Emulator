# Zero Emulator — Cross-Platform Plan (v2, scope-locked)

Supersedes v1. Scope decisions now fixed: no native dependencies at all.
Target stack: **.NET 8 + Avalonia 11 + SDL3**. Windows / macOS (incl. arm64) / Linux.

---

## 1. v1 scope

**In**

- Machines: 48K, 128K, 128Ke, Pentagon 128K, +3 (CPU/ULA/memory only — no disk)
- Files in: `.sna`, `.szx`, `.z80`, `.pzx`, `.scr`, `.rzx`, **`.tap`** (via a new managed converter)
- Files out: `.szx`, `.sna`, `.scr`, RZX recording
- Display, audio, keyboard, joystick/gamepad, save states, tape deck, options

**Out, deliberately**

| Dropped | Cost | Because |
|---|---|---|
| +3 disk (`.dsk`) | `fdc765.dll` | native x86 |
| TR-DOS / Beta disk (`.trd`, `.scl`) | `wd1793.dll` | native x86 |
| `.tzx`, `.csw` tapes | `pzx_tools.dll` | native x86 |
| Commander / assembler | ScintillaNET | native Win32, reference already broken |
| File associations | `ZeroFileAssociater` | per-platform packaging concern |
| Debugger suite | — | deferred to v2 on effort grounds (see §5) |

**Every one of these is additive later.** Nothing in this plan forecloses them, and the
stubs described in §3 are the interfaces the real implementations will slot into.

Be clear-eyed about what v1 can't run: `.tzx` is roughly half the archived Spectrum
library, and anything multiload or with custom loaders is `.tzx`-only. `.tap` covers
standard-loader games, which is a large and very playable subset. This is a fine developer
milestone and a thin public release.

---

## 2. What the cuts buy

v1's top risk was the FDC libraries — unknown scope until you established whether usable
source existed. **That risk is now gone.** Everything remaining is predictable work with no
external unknowns and no native code to find, build, ship or sign.

Consequence worth taking: with no native dependencies, `PublishSingleFile` +
`PublishTrimmed` self-contained works on all six RIDs. Users get one file. Nothing to
install.

---

## 3. Delete vs stub

**Delete outright**

- `ZeroRenderer/`, `DirectXRenderer/`, `IODevices/` — abandoned refactor, not in the solution
- `ZiggyWin.csproj`, `ZiggySound.csproj`, `ZiggyFileAssociater.csproj` — legacy duplicates
- `Form1_BAK.cs` (3,899 lines)
- `ZiggyWin/ZiggyWin/zlib.NET_104/` — use `System.IO.Compression`
- **ScintillaNET and everything that uses it.** Complete removal list, verified against the
  tree — there is nothing else:
  - `Tools/Commander.cs`, `Tools/Commander.Designer.cs`, `Tools/Commander.resx`
  - `ZeroWin.csproj` lines 126–127: `<Reference Include="ScintillaNET">` and its `HintPath`
  - `ZeroWin.csproj` lines 172–176 and 372–373: the `Compile` and `EmbeddedResource` entries
  - `Form1.cs` line 122 (`private Tools.Commander commander;`) and line 4665 (the ctor call)
  - `run/ScintillaNET.dll`

  Nothing else in the repo touches Scintilla — no installer or manifest entries.
- `ZeroFileAssociater` project, and the `SHChangeNotify` P/Invokes that serve it
- `run/*.dll` native binaries, `Zero.application` / ClickOnce manifests, `Managed DirectX Setup/`

**Stub, do not delete**

- `Peripherals/WD1793.cs` — keep the class, replace 13 `extern` methods with managed no-ops.
  Reads (`ReadStatusReg`, `ReadDataReg`, `ReadSectorReg`, `ReadTrackReg`, `ReadSystemReg`)
  return `0xFF`; writes and `DiskInsert`/`DiskEject`/`DiskInitialise`/`DiskShutdown` no-op.
- `Peripherals/UDP765.cs` — same, 9 methods. `DiskStatusRead`/`DiskReadByte` → `0xFF`.

`0xFF` on status reads is what a real controller reports with no drive ready, so the ROMs
fail cleanly ("drive not ready") rather than hanging. Verify +3 and Pentagon still boot to
BASIC after stubbing — that's the acceptance test for this step.

**Write new**

- `Peripherals/TapFile.cs` — TAP → PZX in memory. TAP is
  `[uint16 length][flag byte][data][XOR checksum]` per block; emit standard ROM timings
  (2168T pilot, 667/735T sync, 855/1710T bit cells, 8063 pilot pulses for header / 3223 for
  data). Roughly 150 lines. `PZXFile.cs` already consumes the output, and `Form1.cs` line
  ~3665 is the single call site to redirect.

---

## 4. Phases

### Phase 0 — Build modernisation (1–2 days, zero behaviour change)

SDK-style `.csproj` everywhere. `Speccy` + `Peripherals` → `net8.0`, `AnyCPU`. Drop the
`x86` pin and the `HintPath`s into `C:\Windows\Microsoft.NET\DirectX for Managed Code\`.
`ZeroWin` stays `net8.0-windows` / `UseWindowsForms=true`.

Do the §3 deletions and stubs here. **Gate: the WinForms app still builds and runs on
Windows, with disk and TZX gone.** Don't proceed until that's true — it's the last point
where a regression is cheap to find.

### Phase 1 — Portable core (3–5 days)

1. Remove the 9 `MessageBox.Show` calls from `Speccy` (`Z80.cs` ×2, `zx_48k`, `zx_128k`,
   `zx_128ke`, `zx_plus3`, `Pentagon_128k`). Replace with `event Action<string> OnError`.
2. Remove the stray `using System.Windows.Forms;` from `Peripherals/RZXFile.cs`.
3. **Drop the `System.Windows.Forms` reference from both projects entirely** so this can't
   regress.
4. Write `TapFile.cs`; wire it into the `.tap` case.
5. Build the zexdoc/zexall harness. `Speccy` is now a headless `net8.0` library, so this is
   straightforward, and it's your regression net for every later phase. Do not skip it.

**Gate: `Speccy` + `Peripherals` compile and pass zexall on Linux.** The core is portable
from here on, whatever happens to the UI.

### Phase 2 — Host abstraction, still on Windows (3–5 days)

Add `Zero.Core.Host` with `IDisplay` / `IAudioOut` / `IGamepadSource` (definitions in v1 of
this plan, §5 Phase 2). Implement them over the *existing* DirectX code. `ZRenderer`'s
public properties (`EnableVsync`, `PixelSmoothing`, `ShowScanlines`, `EnableFullScreen`)
already map near-1:1 — you're finishing a refactor the author started.

Also here: `PrecisionTimer` → `Stopwatch`.

### Phase 3 — The loop rewrite + SDL3 backends (1.5–2.5 weeks)

The genuinely novel engineering. Two things, in this order:

1. **Move emulation off the UI thread.** `Program.cs` currently hooks `Application.Idle`
   and `Form1.AppStillIdle` polls Win32 `PeekMessage` — no Avalonia equivalent exists. Move
   to a dedicated thread with a triple-buffered framebuffer handoff, paced by the audio
   clock rather than `Thread.Sleep`. Replace `GetAsyncKeyState` with an event-fed key-state
   array (positional keys, not layout-dependent) and `GetForegroundWindow` with a focus flag.
2. **Implement the backends on SDL3** — audio, gamepad, hotplug. Video via Avalonia
   `WriteableBitmap`; a Spectrum frame with border is under 200k pixels, ~20 MB/s at 50Hz,
   comfortably within Skia's software path. No OpenGL in v1 — that sidesteps Apple's
   deprecated GL entirely. Add `OpenGlControlBase` later only if you want CRT shaders.

Test both **inside the existing WinForms app**, switchable at runtime. This is the highest-
leverage sequencing choice in the plan: it separates "did I break emulation timing" from
"did I break the UI port", which otherwise merge into one undebuggable problem.

**Gate: the WinForms app runs correctly with SDL3 audio, SDL3 gamepads, a WriteableBitmap
display, and emulation on its own thread.** At this point every hard problem is solved and
what remains is volume.

### Phase 4 — Avalonia shell (3–5 weeks)

Port order, and the v1 dialog set:

**Port (≈10,700 lines of WinForms to re-express)** — `Form1` (5,792), `Options` (2,443,
trim the disk pages), `TapeDeck` (1,008), `LoadBinary` (433), `AboutBox1` (273),
`JoystickRemap` (204), `JoystickButtonMapper` (184), `SpectrumKeyboard` (154),
`ArchiveHandler` (139), `TapeInfo` (84). `ZRenderer` is replaced, not ported.

**Defer to v2 — debugger (11,312 lines)**: `Monitor` (8,701), `Registers`, `Breakpoints`,
`WatchWindow`, `Profiler`, `MemoryProfiler`, `MemoryViewer`, `CallStackViewer`.

**Defer to v2 — library/metadata browser (2,737 lines)**: `CoverFlow`, `CoverFlowImage`,
`Library`, `ZLibrary`, `Infoseeker`, `Infoviewer`, `pane`, `TextOnImageControl`,
`PicturePreview`, `ScrollableLabel`. `CoverFlow` is custom GDI drawing and disproportionately
expensive to port.

Deferring the debugger is the single biggest scope lever you have: **a third of the UI
project for a feature most users never open.** Decide it now, explicitly, rather than
discovering it six weeks in.

Also in this phase: replace `System.Drawing` types with Avalonia equivalents (33 non-designer
files, but mostly `Point`/`Size`/`Color`/`Font` — mechanical), and move config from
`Microsoft.Win32.Registry` + WinForms settings to `System.Text.Json` at `%APPDATA%` /
`~/.config` / `~/Library/Application Support`.

Drop `WM_COPYDATA` single-instance IPC, or reimplement on a named pipe.

### Phase 5 — Packaging (1 week)

Six RIDs, self-contained single-file. macOS `.app` bundle + notarisation (start early —
Apple Developer enrolment has lead time). AppImage or Flatpak for Linux. Emulation must
marshal to the main thread for any UI touch on macOS.

---

## 5. Effort and risk

| Phase | Estimate | Risk |
|---|---|---|
| 0 — build modernisation | 1–2 days | Very low |
| 1 — portable core + TAP | 3–5 days | Low |
| 2 — host abstraction | 3–5 days | Low |
| 3 — loop rewrite + SDL3 | 1.5–2.5 weeks | **Medium-high** |
| 4 — Avalonia shell (v1 set) | 3–5 weeks | Medium, but volume not novelty |
| 5 — packaging | 1 week | Medium (notarisation) |

**≈8–12 weeks solo.** No remaining unknowns of the kind that change the plan's shape.

**Where it will actually hurt**

1. **Frame pacing and audio sync across three schedulers.** The classic emulator time-sink,
   reliably underestimated. `Thread.Sleep(1)` behaves quite differently on macOS and Linux.
   Drive from the audio clock; budget real time for this in phase 3.
2. **Scope creep back into the debugger.** The deferral only holds if it's a stated decision.
3. **Silent core regressions during refactoring.** Entirely mitigated by phase 1's zexall
   harness, which is why that step isn't optional.

**Still worth verifying before phase 0:** does the solution build at all today? The Managed
DirectX references need the 2006 SDK installed at a hardcoded
`C:\Windows\Microsoft.NET\DirectX for Managed Code\` path — so you may be starting from a
non-building tree. That's fine, and phase 0 removes the cause, but it's better known going
in than mistaken later for something you broke.
