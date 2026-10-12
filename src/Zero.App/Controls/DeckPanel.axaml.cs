using System;
using System.Reflection;
using Avalonia.Controls;
using SpeccyCommon;
using Zero.Emulation;
using Zero.Emulation.Machines;
using Zero.Emulation.Settings;
using Zero.Emulation.Tape;

namespace Zero.App.Controls
{
    /// <summary>
    /// The hi-fi deck under the screen: VCR-style status lights, a VFD window, tape transport keys
    /// and receiver-style knobs and faders. A view only: <see cref="Show"/> mirrors the session onto
    /// it, and MainWindow.Deck.cs wires its keys and knobs to the same commands the menus use.
    /// </summary>
    public partial class DeckPanel : UserControl
    {
        public DeckPanel()
        {
            InitializeComponent();
            VersionText.Text = VersionLabel;
            // The selector keys are radio buttons: pressing the lit one leaves it lit.
            DeckAKey.Click += (_, __) => { SelectDeck(SelectedDeck); DeckRequested?.Invoke(0); };
            DeckBKey.Click += (_, __) => { SelectDeck(SelectedDeck); DeckRequested?.Invoke(1); };
            SelectDeck(0);
        }

        /// <summary>The build's version as printed under the badge, e.g. "v0.9.0".</summary>
        public static string VersionLabel
        {
            get
            {
                Assembly app = typeof(DeckPanel).Assembly;
                string v = app.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                           ?? app.GetName().Version?.ToString(3) ?? "";
                int plus = v.IndexOf('+'); // drop the commit hash the SDK appends
                return "v" + (plus >= 0 ? v.Substring(0, plus) : v);
            }
        }

        /// <summary>How long the REC lamp flashes after the machine SAVEs a block.</summary>
        public static readonly TimeSpan SaveFlash = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Mirror the session and settings onto the lights, display and controls. UI thread.
        /// <paramref name="recordArmed"/>: the user picked the tape file SAVE writes to.
        /// </summary>
        public void Show(EmulatorSession session, EmulatorSettings settings, bool recordArmed)
        {
            TapeDeck tape = session.Tape;
            bool saving = DateTime.UtcNow - tape.LastSaveUtc < SaveFlash;

            PowerLed.IsLit = session.IsRunning;
            PowerLed.Blink = session.IsPaused;
            RecLamp.IsLit = recordArmed || saving;
            RecLamp.Blink = saving;

            VfdMachine.Text = MachineFactory.DisplayName(session.Model).ToUpperInvariant() + session.State switch
            {
                EmulatorState.PlayingRzx => "  RZX",
                EmulatorState.RecordingRzx => "  REC",
                _ => ""
            };
            VfdStereo.Text = settings.Audio.StereoSoundMode switch { 0 => "MONO", 2 => "ABC", _ => "ACB" };
            SetLit(VfdTurbo, settings.Emulation.EmulationSpeed > 1);
            SetLit(VfdMute, settings.Audio.Mute);
            // The +3's drives, on the display and on the receiver's drive fronts. (The Pentagon's
            // TR-DOS drives are not emulated, so it reads as having none.)
            bool drives = session.HasDiskDrives;
            int busy = drives ? session.BusyDiskUnit : -1;
            ShowDrive(0, session.DiskInDrive(0), drives, busy == 0, VfdLedA, VfdDiskA, DriveADisk, DriveALed, DriveAName);
            ShowDrive(1, session.DiskInDrive(1), drives, busy == 1, VfdLedB, VfdDiskB, DriveBDisk, DriveBLed, DriveBName);

            if (!tape.IsInserted)
            {
                VfdMode.Text = saving ? "REC" : "----";
                VfdCounter.Text = "000";
                VfdBlock.Text = "BLK --/--";
                VfdTitle.Text = saving || recordArmed ? "REC  " + RecordTarget(tape) : "NO TAPE";
            }
            else
            {
                int count = tape.Blocks.Count;
                int block = Math.Min(tape.CurrentBlock + 1, Math.Max(1, count));
                VfdMode.Text = saving ? "REC" : tape.IsPlaying ? "PLAY" : "STOP";
                VfdCounter.Text = tape.Counter.ToString("000");
                VfdBlock.Text = $"BLK {Math.Min(block, 99):00}/{Math.Min(count, 99):00}";
                VfdTitle.Text = string.IsNullOrEmpty(tape.Title) ? "UNTITLED" : tape.Title.ToUpperInvariant();
            }

            if (_message != null && DateTime.UtcNow < _messageUntil) VfdTitle.Text = _message;

            EjectKey.IsEnabled = true; // inserts a tape when the deck is empty
            RewindKey.IsEnabled = PrevKey.IsEnabled = NextKey.IsEnabled = tape.IsInserted;
            PlayKey.IsEnabled = tape.IsInserted && !tape.IsPlaying;
            StopKey.IsEnabled = tape.IsInserted && tape.IsPlaying;

            CrtSettings crt = settings.Render.Crt;
            CrtKey.IsChecked = crt.Enabled;
            ScanKey.IsChecked = crt.Scanlines;
            CurveFader.IsEnabled = GlowFader.IsEnabled = crt.Enabled;   // scanlines work with CRT off too
            CurveFader.Opacity = GlowFader.Opacity = crt.Enabled ? 1 : 0.45;
            CurveFader.Value = crt.Curvature;
            GlowFader.Value = crt.Glow;

            if (!MachineKnob.IsInteracting) MachineKnob.Value = Array.IndexOf(Machines, session.Model);
            StereoKnob.Value = settings.Audio.StereoSoundMode;
            CpuKnob.Value = settings.Emulation.CpuMultiplier switch { >= 4 => 2, 2 => 1, _ => 0 };
            VolumeKnob.Value = settings.Audio.Volume;
            MuteKey.IsChecked = settings.Audio.Mute;
            TurboKey.IsChecked = settings.Emulation.EmulationSpeed > 1;
        }

        private static void ShowDrive(int unit, string disk, bool drives, bool busy,
            Border vfdLed, TextBlock vfdLine, Border slotDisk, DeckLed driveLed, TextBlock driveName)
        {
            char letter = (char)('A' + unit);
            vfdLine.Text = $"{letter}: {DiskLabel(disk)}";
            SetLit(vfdLine, drives && disk != null);  // an empty drive's line stays unlit
            vfdLed.Classes.Set("lit", busy);
            slotDisk.IsVisible = disk != null;          // the slot looks occupied
            driveLed.IsLit = busy;
            driveName.Text = disk == null ? "NO DISK" : DiskLabel(disk);
            driveName.Classes.Set("empty", disk == null);
        }

        /// <summary>The deck showing: 0 the cassette deck (A), 1 the receiver (B).</summary>
        public int SelectedDeck { get; private set; }

        /// <summary>Raised when a DECK selector key is pressed, with the deck asked for.</summary>
        public event Action<int> DeckRequested;

        /// <summary>
        /// Show one deck. Both stay laid out, one over the other, so the deck area keeps its size;
        /// the other is made transparent and stops taking the pointer.
        /// </summary>
        public void SelectDeck(int deck)
        {
            SelectedDeck = deck == 1 ? 1 : 0;
            bool cassette = SelectedDeck == 0;
            CassetteDeck.Opacity = cassette ? 1 : 0;
            CassetteDeck.IsHitTestVisible = cassette;
            ReceiverDeck.Opacity = cassette ? 0 : 1;
            ReceiverDeck.IsHitTestVisible = !cassette;
            DeckAKey.IsChecked = cassette;
            DeckBKey.IsChecked = !cassette;
        }

        /// <summary>Frames per second, or "PAUSE", in the corner of the display.</summary>
        public void ShowFps(string text) => VfdFps.Text = text;

        /// <summary>The machine at each stop of the machine knob, in the order of its legends.</summary>
        private static readonly MachineModel[] Machines =
            { MachineModel._48k, MachineModel._128k, MachineModel._128ke, MachineModel._plus3, MachineModel._pentagon };

        public static MachineModel MachineAt(double stop) => Machines[Math.Clamp((int)stop, 0, Machines.Length - 1)];

        // ------------------------------------------------------------------ messages

        private string _message;
        private DateTime _messageUntil;

        /// <summary>How long a status message stays on the display before the tape title returns.</summary>
        public static readonly TimeSpan MessageTime = TimeSpan.FromSeconds(4);

        /// <summary>Show a status message on the display's info line for a few seconds.</summary>
        public void ShowMessage(string text)
        {
            _message = string.IsNullOrEmpty(text) ? null : text.ToUpperInvariant();
            _messageUntil = DateTime.UtcNow + MessageTime;
        }

        /// <summary>The CPU multiplier for each stop on the CPU knob.</summary>
        public static int CpuMultiplierAt(double stop) => stop >= 2 ? 4 : stop >= 1 ? 2 : 1;

        /// <summary>
        /// A disk's name as the deck shows it: its image's file name, upper case, without the
        /// extension. +3 disks carry no volume name of their own. Long names are trimmed to fit.
        /// </summary>
        internal static string DiskLabel(string path) =>
            string.IsNullOrEmpty(path) ? "--" : System.IO.Path.GetFileNameWithoutExtension(path).ToUpperInvariant();

        private static string RecordTarget(TapeDeck tape) =>
            System.IO.Path.GetFileNameWithoutExtension(tape.TapSavePath ?? "").ToUpperInvariant();

        private static void SetLit(TextBlock legend, bool lit) => legend.Classes.Set("off", !lit);
    }
}
