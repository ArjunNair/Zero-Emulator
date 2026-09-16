using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Peripherals;
using Speccy;
using SpeccyCommon;
using Zero.Emulation.Host;
using Zero.Emulation.Input;
using Zero.Emulation.Machines;
using Zero.Emulation.Settings;
using Zero.Emulation.Tape;

namespace Zero.Emulation
{
    /// <summary>
    /// Owns one emulated machine and runs it on a dedicated thread.
    ///
    /// Threading contract:
    ///  * Everything that touches the machine runs on the emulation thread. UI code uses
    ///    <see cref="Post"/> / <see cref="InvokeAsync{T}"/> or the typed helpers below, which do that for you.
    ///  * Events are raised on the emulation thread. Marshal to your UI thread before touching widgets.
    ///  * Frames are handed over through <see cref="Frames"/> (triple buffer, never blocks either side).
    ///  * Pacing comes from the audio sink: the core waits for the sink to have room for the next
    ///    20 ms of samples, so the audio clock drives the frame rate.
    /// </summary>
    public sealed partial class EmulatorSession : IDisposable
    {
        private readonly ConcurrentQueue<Action> _commands = new ConcurrentQueue<Action>();
        private readonly ManualResetEventSlim _resumeGate = new ManualResetEventSlim(true);
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private Thread _thread;
        private volatile bool _stopRequested;
        private volatile bool _paused;
        private zx_spectrum _zx;
        private IAudioOutput _audio;
        private KempstonJoystick _kempston;
        private KempstonMouse _mouse;
        private readonly GamepadInput[] _lastGamepad = new GamepadInput[2];
        private readonly List<keyCode> _gamepadKeys = new List<keyCode>();
        private EmulatorState _state = EmulatorState.Stopped;
        private MachineModel _modelBeforeRzx;
        private bool _autoLoadPending;
        private int _autoLoadIndex;

        public EmulatorSettings Settings { get; }
        public KeyboardState Keyboard { get; } = new KeyboardState();
        public TapeDeck Tape { get; } = new TapeDeck();
        public MouseState Mouse { get; } = new MouseState();

        /// <summary>The Kempston mouse interface while enabled in settings, else null. Emulation thread state.</summary>
        public KempstonMouse KempstonMouseDevice => _mouse;

        /// <summary>Raw state of gamepad 0 or 1 as sampled on the last frame (for remapping UIs).</summary>
        public GamepadInput LastGamepadInput(int index) => index >= 0 && index < _lastGamepad.Length ? _lastGamepad[index] : default;
        public FrameQueue Frames { get; } = new FrameQueue();

        /// <summary>Creates the audio sink for each new machine. Default paces silently from the wall clock.</summary>
        public Func<IAudioOutput> AudioFactory { get; set; } = () => new TimerPacedAudioOutput();

        /// <summary>Optional gamepad provider (SDL3 in the desktop app).</summary>
        public IGamepadSource Gamepads { get; set; }

        /// <summary>Directory holding the machine ROMs.</summary>
        public string RomDirectory { get; set; }

        /// <summary>The live machine. Only touch it from the emulation thread (see Post/InvokeAsync).</summary>
        public zx_spectrum Machine => _zx;

        public MachineModel Model { get; private set; }
        public EmulatorState State => _state;
        public bool IsPaused => _paused;
        public bool IsRunning => _thread != null && _thread.IsAlive;
        /// <summary>Frames handed to the display: one per turn of the loop.</summary>
        public long FrameCount => Frames.FramesProduced;

        /// <summary>
        /// Frames the machine actually ran. Above 1x the machine runs that many frames for every one
        /// it hands over -- only the last of them is painted -- so the two part company exactly by
        /// the speed, and it is this one that says how fast the Spectrum is going.
        /// </summary>
        public long EmulatedFrameCount => Interlocked.Read(ref _emulatedFrames);

        private long _emulatedFrames;

        /// <summary>A new frame is in <see cref="Frames"/>. Emulation thread.</summary>
        public event Action FrameReady;
        public event Action<string> Error;
        public event Action<MachineModel> MachineChanged;
        public event Action<EmulatorState> StateChanged;
        /// <summary>A file was loaded (snapshot, tape, RZX). Argument is the path or archive entry name.</summary>
        public event Action<string> FileLoaded;

        public EmulatorSession(EmulatorSettings settings = null)
        {
            Settings = settings ?? new EmulatorSettings();
            RomDirectory = AppPaths.Resolve(Settings.Paths.Roms, "roms");
            Tape.EdgeLoad = Settings.Tape.EdgeLoad;
            Tape.AutoPlay = Settings.Tape.AutoPlay;
            Tape.FastLoad = Settings.Tape.FastLoad;
            Tape.AutoLoad = Settings.Tape.AutoLoad;
            Tape.TapSavePath = Path.Combine(AppPaths.Resolve(Settings.Paths.Saves, "saves"), "zero_saved.tap");
            Tape.Error += msg => Error?.Invoke(msg);
        }

        // ------------------------------------------------------------------ lifecycle

        /// <summary>Create the configured machine and start the emulation thread.</summary>
        public void Start()
        {
            if (IsRunning) return;
            _stopRequested = false;
            _thread = new Thread(ThreadMain)
            {
                Name = "Zero emulation",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _thread.Start();
            Post(() => CreateMachine(Settings.Emulation.Model));
        }

        /// <summary>Stop the thread and release the machine and audio device.</summary>
        public void Stop()
        {
            if (_thread == null) return;
            Trace.Log("Session.Stop: requesting");
            _stopRequested = true;
            _paused = false;
            _resumeGate.Set();
            _wake.Set();
            if (!_thread.Join(2000))
            {
                Trace.Log("Session.Stop: join timed out, interrupting");
                _thread.Interrupt();
                _thread.Join(2000);
            }
            Trace.Log("Session.Stop: thread finished=" + !_thread.IsAlive);
            _thread = null;
            SetState(EmulatorState.Stopped);
        }

        public void Pause()
        {
            if (_paused) return;
            _paused = true;
            _resumeGate.Reset();
            Post(() => { _audio?.Stop(); });
            SetState(EmulatorState.Paused);
        }

        public void Resume()
        {
            if (!_paused) return;
            Post(() => { _audio?.Play(); });
            _paused = false;
            _resumeGate.Set();
            SetState(_zx != null && _zx.isPlayingRZX ? EmulatorState.PlayingRzx : EmulatorState.Running);
        }

        public void TogglePause() { if (_paused) Resume(); else Pause(); }

        public void Dispose()
        {
            Stop();
            Gamepads?.Dispose();
            _resumeGate.Dispose();
            _wake.Dispose();
        }

        // ------------------------------------------------------------------ marshalling

        /// <summary>Run <paramref name="action"/> on the emulation thread before the next frame. Works while paused.</summary>
        public void Post(Action action)
        {
            _commands.Enqueue(action);
            _wake.Set();
        }

        /// <summary>Run a function on the emulation thread and await its result.</summary>
        public Task<T> InvokeAsync<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Post(() =>
            {
                try { tcs.SetResult(func()); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }

        public Task InvokeAsync(Action action) => InvokeAsync(() => { action(); return true; });

        // ------------------------------------------------------------------ thread body

        private void ThreadMain()
        {
            try
            {
                while (!_stopRequested)
                {
                    DrainCommands();
                    if (_stopRequested) break;

                    if (_paused)
                    {
                        // Sleep until resumed or a command arrives (commands run while paused).
                        WaitHandle.WaitAny(new WaitHandle[] { _resumeGate.WaitHandle, _wake }, 250);
                        continue;
                    }

                    if (_zx == null)
                    {
                        _wake.WaitOne(50);
                        continue;
                    }

                    RunOneFrame();
                }
            }
            catch (ThreadInterruptedException) { }
            catch (Exception ex)
            {
                Error?.Invoke("Emulation thread crashed: " + ex);
            }
            finally
            {
                Trace.Log("ThreadMain: shutting down machine");
                Tape.Detach();
                try { _zx?.Shutdown(); } catch (Exception ex) { Trace.Log("machine shutdown threw: " + ex.Message); }
                _zx = null;
                _audio = null;
                Trace.Log("ThreadMain: exit");
            }
        }

        private void DrainCommands()
        {
            while (_commands.TryDequeue(out Action cmd))
            {
                try { cmd(); }
                catch (Exception ex) { Error?.Invoke(ex.Message); }
            }
        }

        private void RunOneFrame()
        {
            zx_spectrum zx = _zx;

            ApplyInput(zx);

            if (zx.doRun)
            {
                zx.Run();
                Interlocked.Add(ref _emulatedFrames, Math.Max(1, zx.emulationSpeed));
            }

            if (zx.isResetOver && _autoLoadPending)
                AutoLoadStep(zx);

            PublishFrame(zx);
            zx.needsPaint = false;
        }

        private void PublishFrame(zx_spectrum zx)
        {
            int[] src = zx.ScreenBuffer;
            if (src == null) return;
            int w = zx.GetTotalScreenWidth();
            int h = zx.GetTotalScreenHeight();
            VideoFrame frame = Frames.BeginWrite(w, h);
            Array.Copy(src, frame.Pixels, Math.Min(src.Length, frame.Pixels.Length));
            Frames.EndWrite();
            FrameReady?.Invoke();
        }

        // ------------------------------------------------------------------ input

        private void ApplyInput(zx_spectrum zx)
        {
            InputSettings input = Settings.Input;
            bool keyJoy = input.EnableKeyboardJoystick && input.KeyboardJoystickType != (int)zx_spectrum.JoysticksEmulated.NONE;

            Keyboard.ApplyTo(zx, keyJoy);

            if (keyJoy)
            {
                var s = new GamepadState
                {
                    Connected = true,
                    Left = Keyboard.IsDown(keyCode.LEFT),
                    Right = Keyboard.IsDown(keyCode.RIGHT),
                    Up = Keyboard.IsDown(keyCode.UP),
                    Down = Keyboard.IsDown(keyCode.DOWN),
                    Fire1 = Keyboard.IsDown(keyCode.ALT)
                };
                zx.keyBuffer[(int)keyCode.ALT] = false; // fire key is consumed by the joystick
                JoystickRouter.Apply(zx, _kempston, input.KeyboardJoystickType, s);
            }

            IGamepadSource pads = Gamepads;
            if (pads != null)
            {
                pads.Update();
                for (int i = 0; i < 2; i++)
                {
                    GamepadInput raw = pads.Count > i ? pads.Poll(i) : default;
                    _lastGamepad[i] = raw;
                    int type = i == 0 ? input.Gamepad1Emulates : input.Gamepad2Emulates;
                    if (!raw.Connected) continue;
                    _gamepadKeys.Clear();
                    GamepadState mapped = Settings.Input.BindingsFor(i).Resolve(raw, _gamepadKeys);
                    if (type != 0) JoystickRouter.Apply(zx, _kempston, type, mapped);
                    foreach (keyCode k in _gamepadKeys) zx.keyBuffer[(int)k] = true;
                }
            }

            if (_mouse != null)
            {
                Mouse.Consume(out int dx, out int dy);
                _mouse.MouseX += (byte)dx;
                _mouse.MouseY -= (byte)dy; // Kempston Y grows upwards
                int buttons = Mouse.Buttons;
                byte b = 0xFF;
                if ((buttons & MouseState.LeftButton) != 0) b &= unchecked((byte)~0x2);
                if ((buttons & MouseState.RightButton) != 0) b &= unchecked((byte)~0x1);
                _mouse.MouseButton = b;
            }
        }

        private void ConfigureJoysticks(zx_spectrum zx)
        {
            InputSettings input = Settings.Input;
            bool wantKempston =
                input.Gamepad1Emulates == (int)zx_spectrum.JoysticksEmulated.KEMPSTON ||
                input.Gamepad2Emulates == (int)zx_spectrum.JoysticksEmulated.KEMPSTON ||
                (input.EnableKeyboardJoystick && input.KeyboardJoystickType == (int)zx_spectrum.JoysticksEmulated.KEMPSTON);

            zx.RemoveDevice(SPECCY_DEVICE.KEMPSTON_JOYSTICK);
            _kempston = null;
            if (wantKempston)
            {
                _kempston = new KempstonJoystick { UsePort1F = input.KempstonUsesPort1F };
                zx.AddDevice(_kempston);
            }
            zx.UseKempstonPort1F = input.KempstonUsesPort1F;

            zx.RemoveDevice(SPECCY_DEVICE.KEMPSTON_MOUSE);
            _mouse = null;
            if (input.EnableKempstonMouse)
            {
                _mouse = new KempstonMouse();
                zx.AddDevice(_mouse);
            }
        }

        // ------------------------------------------------------------------ machine management

        /// <summary>Switch machine model (emulation thread). The current machine is discarded.</summary>
        private void CreateMachine(MachineModel model)
        {
            Tape.Detach();
            if (_zx != null)
            {
                _zx.Shutdown();
                _zx = null;
            }
            _autoLoadPending = false;

            _audio = AudioFactory();
            zx_spectrum zx = MachineFactory.Create(model, _audio, Settings.Emulation.LateTimings);
            zx.OnError += msg => Error?.Invoke(msg);

            string romFile = Settings.Roms.For(model);
            string romDir = RomDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()) ? RomDirectory : RomDirectory + Path.DirectorySeparatorChar;
            if (!zx.LoadROM(romDir, romFile))
                Error?.Invoke($"Could not load ROM '{romFile}' from {romDir}");

            ApplySettingsTo(zx);
            Tape.Attach(zx);

            _zx = zx;
            Model = model;
            Settings.Emulation.Model = model;
            zx.Start();
            _audio.Play();
            if (_paused) _audio.Stop();
            MachineChanged?.Invoke(model);
            SetState(_paused ? EmulatorState.Paused : EmulatorState.Running);
        }

        /// <summary>Push every relevant setting into the machine. Safe to call after editing Settings.</summary>
        private void ApplySettingsTo(zx_spectrum zx)
        {
            EmulatorSettings s = Settings;
            zx.SetSoundVolume(Math.Clamp(s.Audio.Volume, 0, 100) / 100.0f);
            zx.SetEmulationSpeed(Math.Clamp(s.Emulation.EmulationSpeed, 1, 10));
            zx.SetCPUSpeed(Math.Clamp(s.Emulation.CpuMultiplier, 1, 14));
            zx.SetStereoSound(s.Audio.StereoSoundMode);
            zx.EnableAY(s.Audio.EnableAYFor48K);
            zx.MuteSound(s.Audio.Mute);
            zx.tapeTrapsDisabled = !s.Tape.RomTraps;
            zx.Issue2Keyboard = s.Emulation.UseIssue2Keyboard;
            zx.LateTiming = s.Emulation.LateTimings ? 1 : 0;
            Tape.EdgeLoad = s.Tape.EdgeLoad;
            Tape.AutoPlay = s.Tape.AutoPlay;
            Tape.FastLoad = s.Tape.FastLoad;
            Tape.AutoLoad = s.Tape.AutoLoad;
            ConfigureJoysticks(zx);
            ApplyPalette(zx, s.Render.Palette);
        }

        private static void ApplyPalette(zx_spectrum zx, string palette)
        {
            switch (palette)
            {
                case "Grayscale":
                    zx.RemoveDevice(SPECCY_DEVICE.ULA_PLUS);
                    zx.SetPalette(Grayscale(zx.NormalColors));
                    break;
                case "ULA Plus":
                    zx.AddDevice(zx.ula_plus);
                    zx.SetPalette(zx.NormalColors);
                    break;
                default:
                    zx.RemoveDevice(SPECCY_DEVICE.ULA_PLUS);
                    zx.SetPalette(zx.NormalColors);
                    break;
            }
        }

        private static int[] Grayscale(int[] rgb)
        {
            var gray = new int[rgb.Length];
            for (int i = 0; i < rgb.Length; i++)
            {
                int r = (rgb[i] >> 16) & 0xFF, g = (rgb[i] >> 8) & 0xFF, b = rgb[i] & 0xFF;
                int y = (int)(0.299 * r + 0.587 * g + 0.114 * b);
                gray[i] = (y << 16) | (y << 8) | y;
            }
            return gray;
        }

        private void SetState(EmulatorState state)
        {
            if (_state == state) return;
            _state = state;
            StateChanged?.Invoke(state);
        }

        // ------------------------------------------------------------------ public commands (UI thread safe)

        public void SwitchMachine(MachineModel model) => Post(() => CreateMachine(model));

        /// <summary>Re-read Settings and apply what can change without a new machine.</summary>
        public void ApplySettings() => Post(() => { if (_zx != null) ApplySettingsTo(_zx); });

        public void Reset(bool hard) => Post(() => ResetCore(hard));

        private void ResetCore(bool hard)
        {
            if (_zx == null) return;
            StopRzxCore();
            _zx.Reset(hard);
            _zx.ResetKeyboard();
            Keyboard.ReleaseAll();
            _autoLoadPending = false;
        }

        // Settings change synchronously so callers (menus) can read them back at once;
        // only the machine update is deferred to the emulation thread.

        public void SetSpeed(int speed)
        {
            int clamped = Math.Clamp(speed, 1, 10);
            Settings.Emulation.EmulationSpeed = clamped;
            Post(() => _zx?.SetEmulationSpeed(clamped));
        }

        public void SetVolume(int percent)
        {
            int clamped = Math.Clamp(percent, 0, 100);
            Settings.Audio.Volume = clamped;
            Post(() => _zx?.SetSoundVolume(clamped / 100.0f));
        }

        public void SetMute(bool mute)
        {
            Settings.Audio.Mute = mute;
            Post(() => _zx?.MuteSound(mute));
        }

        public void SetPalette(string palette)
        {
            Settings.Render.Palette = palette;
            Post(() => { if (_zx != null) ApplyPalette(_zx, palette); });
        }

        // ------------------------------------------------------------------ tape auto-load

        private const int SV_LAST_K = 23560; // system variable: last key pressed
        private const int SV_FLAGS = 23611;  // bit 5 set = a key is waiting
        private static readonly byte[] AutoLoad48Keys = { 239, 34, 34, 13 };                 // LOAD "" ENTER
        private static readonly byte[] AutoLoadPlus3Keys =                                    // (down) ENTER  load "t:":load ""  ENTER
            { 10, 13, 108, 111, 97, 100, 32, 34, 116, 58, 34, 58, 108, 111, 97, 100, 32, 34, 34, 13 };

        /// <summary>Hard-reset and then type LOAD "" once the ROM is ready (emulation thread).</summary>
        private void BeginAutoLoad()
        {
            ResetCore(true);
            _autoLoadPending = true;
            _autoLoadIndex = 0;
        }

        private void AutoLoadStep(zx_spectrum zx)
        {
            if ((zx.PeekByteNoContend(SV_FLAGS) & 0x20) != 0)
                return; // previous key not consumed yet

            byte[] keys;
            if (zx.model == MachineModel._plus3) keys = AutoLoadPlus3Keys;
            else if (zx.model == MachineModel._48k) keys = AutoLoad48Keys;
            else keys = null; // 128K menus: ENTER selects "Tape Loader"

            if (keys == null)
            {
                zx.PokeByteNoContend(SV_LAST_K, 13);
                zx.PokeByteNoContend(SV_FLAGS, zx.PeekByteNoContend(SV_FLAGS) | 0x20);
                _autoLoadPending = false;
                return;
            }

            zx.PokeByteNoContend(SV_LAST_K, keys[_autoLoadIndex]);
            zx.PokeByteNoContend(SV_FLAGS, zx.PeekByteNoContend(SV_FLAGS) | 0x20);
            if (++_autoLoadIndex >= keys.Length)
                _autoLoadPending = false;
        }
    }
}
