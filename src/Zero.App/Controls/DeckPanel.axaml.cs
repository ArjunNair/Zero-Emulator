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
        public static readonly Avalonia.StyledProperty<DeckFinish> FinishProperty =
            Avalonia.AvaloniaProperty.Register<DeckPanel, DeckFinish>(nameof(Finish));

        /// <summary>The faceplates' finish: black, or silver with dark print.</summary>
        public DeckFinish Finish { get => GetValue(FinishProperty); set => SetValue(FinishProperty, value); }

        protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == FinishProperty) Classes.Set("silver", Finish == DeckFinish.Silver);
        }

        public DeckPanel()
        {
            InitializeComponent();
            VersionText.Text = VersionLabel;
            // The selector keys are radio buttons: pressing the lit one leaves it lit.
            DeckAKey.Click += (_, __) => { SelectDeck(SelectedDeck); DeckRequested?.Invoke(0); };
            DeckBKey.Click += (_, __) => { SelectDeck(SelectedDeck); DeckRequested?.Invoke(1); };
            MachineSwitch.ValueChangedByUser += (_, v) => { _requestedMachine = MachineAt(v); _requestedAt = DateTime.UtcNow; };
            // The faders' tooltips give their setting, and keep up as they are dragged.
            CurveFader.PropertyChanged += (_, e) => { if (e.Property == DeckFader.ValueProperty) ShowFaderTips(); };
            HousingFader.PropertyChanged += (_, e) => { if (e.Property == DeckFader.ValueProperty) ShowFaderTips(); };
            ShowFaderTips();
            SelectDeck(0);
        }

        /// <summary>The housing fader's full travel: the most of the window the cabinet can take, as in Options.</summary>
        public const double MaxHousing = 0.15;

        private void ShowFaderTips()
        {
            ToolTip.SetTip(CurveFader, $"Screen curvature: {CurveFader.Value:P0}");
            ToolTip.SetTip(HousingFader, $"Television housing: {HousingFader.Value * MaxHousing:P0} of the window");
        }

        // The machine the switch was last let go on. The session switches on its own thread, so until
        // it reports that machine the switch stays put rather than snapping back to the old one --
        // or until a moment has passed, in case the switch failed.
        private MachineModel? _requestedMachine;
        private DateTime _requestedAt;
        private static readonly TimeSpan MachineSwitchGrace = TimeSpan.FromSeconds(2);

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
            PauseKey.IsChecked = session.IsPaused;
            RecLamp.IsLit = recordArmed || saving;
            RecLamp.Blink = saving;

            // The machine row: the current model's legend lit; the 128KE lights 128K and its E.
            MachineModel model = session.Model;
            SetLit(Vfd48k, model == MachineModel._48k);
            SetLit(Vfd128k, model is MachineModel._128k or MachineModel._128ke);
            SetLit(Vfd128kE, model == MachineModel._128ke);
            SetLit(VfdPlus3, model == MachineModel._plus3);
            SetLit(VfdPentagon, model == MachineModel._pentagon);
            VfdStereo.Text = settings.Audio.StereoSoundMode switch { 0 => "MONO", 2 => "ABC", _ => "ACB" };
            SetLit(VfdTurbo, settings.Emulation.EmulationSpeed > 1);
            SetLit(VfdMute, settings.Audio.Mute);
            // The drives, on the display and on the receiver's drive fronts: the +3's two, or the
            // Pentagon's four (a machine without drives shows the ones it had last, unlit).
            bool drives = session.HasDiskDrives;
            ShowDriveCount(session.DiskDriveCount);
            ShowDrive(0, session.DiskInDrive(0), drives, VfdDiskA, DriveADisk, DriveAName);
            ShowDrive(1, session.DiskInDrive(1), drives, VfdDiskB, DriveBDisk, DriveBName);
            ShowDrive(2, session.DiskInDrive(2), drives, VfdDiskC, DriveCDisk, DriveCName);
            ShowDrive(3, session.DiskInDrive(3), drives, VfdDiskD, DriveDDisk, DriveDName);
            for (int unit = 0; unit < 4; unit++)
                ShowLight(unit, drives ? session.DiskLightOf(unit) : DiskLight.Off);

            if (!tape.IsInserted)
            {
                VfdCounter.Text = "000";
                VfdBlock.Text = "--/--";
                VfdTitle.Text = saving || recordArmed ? "REC  " + RecordTarget(tape) : "NO TAPE";
            }
            else
            {
                int count = tape.Blocks.Count;
                int block = Math.Min(tape.CurrentBlock + 1, Math.Max(1, count));
                VfdCounter.Text = tape.Counter.ToString("000");
                VfdBlock.Text = BlockReadout(block, count);
                VfdTitle.Text = string.IsNullOrEmpty(tape.Title) ? "UNTITLED" : tape.Title.ToUpperInvariant();
            }

            if (_message != null && DateTime.UtcNow < _messageUntil) VfdTitle.Text = _message;

            // The transport legends. PLAY and REC serve the tape and RZX recordings alike; RZX lights
            // beside them when it is a recording that is playing back or being made.
            bool rzxPlaying = session.State == EmulatorState.PlayingRzx;
            bool rzxRecording = session.State == EmulatorState.RecordingRzx;
            SetLit(VfdPlay, tape.IsPlaying || rzxPlaying);
            SetLit(VfdStop, tape.IsInserted && !tape.IsPlaying && !rzxPlaying);
            SetLit(VfdRec, saving || rzxRecording);
            SetLit(VfdRzx, rzxPlaying || rzxRecording);

            EjectKey.IsEnabled = true; // inserts a tape when the deck is empty
            RewindKey.IsEnabled = PrevKey.IsEnabled = NextKey.IsEnabled = tape.IsInserted;
            PlayKey.IsEnabled = tape.IsInserted && !tape.IsPlaying;
            StopKey.IsEnabled = tape.IsInserted && tape.IsPlaying;

            CrtSettings crt = settings.Render.Crt;
            CrtKey.IsChecked = crt.Enabled;
            ScanKey.IsChecked = crt.Scanlines;
            CurveFader.IsEnabled = HousingFader.IsEnabled = crt.Enabled;   // scanlines work with CRT off too
            CurveFader.Opacity = HousingFader.Opacity = crt.Enabled ? 1 : 0.45;
            CurveFader.Value = crt.Curvature;
            HousingFader.Value = crt.Bezel / MaxHousing;

            if (_requestedMachine == session.Model || DateTime.UtcNow - _requestedAt > MachineSwitchGrace)
                _requestedMachine = null;
            if (!MachineSwitch.IsInteracting && _requestedMachine == null)
                MachineSwitch.Value = Array.IndexOf(Machines, session.Model);
            int source = JoystickSourceAt(settings.Input);
            JoystickCursorKey.IsChecked = source == JoystickSourceCursor;
            JoystickGamepadKey.IsChecked = source == JoystickSourceGamepad;
            JoystickNoneKey.IsChecked = source == JoystickSourceNone;
            int joystick = source == JoystickSourceGamepad ? settings.Input.Gamepad1Emulates : settings.Input.KeyboardJoystickType;
            if (!JoystickSwitch.IsInteracting) JoystickSwitch.Value = Math.Clamp(joystick, 1, 4) - 1;
            JoystickSwitch.Opacity = source == JoystickSourceNone ? 0.45 : 1;   // nothing plays it
            StereoKnob.Value = settings.Audio.StereoSoundMode;
            CpuKnob.Value = settings.Emulation.CpuMultiplier switch { >= 4 => 2, 2 => 1, _ => 0 };
            VolumeKnob.Value = settings.Audio.Volume;
            MuteKey.IsChecked = settings.Audio.Mute;
            TurboKey.IsChecked = settings.Emulation.EmulationSpeed > 1;
        }

        /// <summary>Width of the drive bays: the +3's two full width, or the Pentagon's four two by two.</summary>
        private const double TwoBaysWidth = 150, FourBaysWidth = 222;

        /// <summary>
        /// Two drives (A over B) or four (A and B, then C and D beside them) on the display and the
        /// receiver. Neither deck grows taller: C and D take a second column.
        /// </summary>
        private void ShowDriveCount(int count)
        {
            bool four = count > 2;
            VfdDrivesCD.IsVisible = four;
            Grid.SetColumnSpan(VfdDrivesAB, four ? 1 : 2);
            DriveBays.Width = four ? FourBaysWidth : TwoBaysWidth;
            DriveCBay.IsVisible = DriveDBay.IsVisible = four;
            Grid.SetColumnSpan(DriveABay, four ? 1 : 3);
            Grid.SetColumnSpan(DriveBBay, four ? 1 : 3);
        }

        private static void ShowDrive(int unit, string disk, bool drives,
            VfdText vfdLine, Border slotDisk, TextBlock driveName)
        {
            char letter = (char)('A' + unit);
            vfdLine.Text = $"{letter}: {DiskLabel(disk)}";
            SetLit(vfdLine, drives && disk != null);  // an empty drive's line stays unlit
            slotDisk.IsVisible = disk != null;          // the slot looks occupied
            driveName.Text = disk == null ? "NO DISK" : DiskLabel(disk);
            driveName.Classes.Set("empty", disk == null);
        }

        /// <summary>
        /// A drive's busy light, on the receiver's drive front and as the segment on the display:
        /// bright while the drive is in use, dim for a +3 drive at rest, otherwise off. See
        /// <see cref="EmulatorSession.DiskLightOf"/>.
        /// </summary>
        private void ShowLight(int unit, DiskLight light)
        {
            (Border vfdLed, DeckLed driveLed) = unit switch
            {
                0 => (VfdLedA, DriveALed),
                1 => (VfdLedB, DriveBLed),
                2 => (VfdLedC, DriveCLed),
                _ => (VfdLedD, DriveDLed)
            };
            vfdLed.Classes.Set("lit", light == DiskLight.Bright);
            vfdLed.Classes.Set("dim", light == DiskLight.Dim);
            driveLed.IsLit = light != DiskLight.Off;
            driveLed.Dim = light == DiskLight.Dim;
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
        public void ShowFps(string text)
        {
            VfdFps.Text = text;
            VfdFpsLabel.IsVisible = int.TryParse(text, out _); // a rate, not PAUSE or nothing
        }

        /// <summary>The machine at each stop of the machine switch, in the order of its legends.</summary>
        private static readonly MachineModel[] Machines =
            { MachineModel._48k, MachineModel._128k, MachineModel._128ke, MachineModel._plus3, MachineModel._pentagon };

        public static MachineModel MachineAt(int stop) => Machines[Math.Clamp(stop, 0, Machines.Length - 1)];

        /// <summary>What can play the joystick, one key each under the joystick switch.</summary>
        public const int JoystickSourceCursor = 0, JoystickSourceGamepad = 1, JoystickSourceNone = 2;

        /// <summary>
        /// What plays the joystick: the cursor keys if they are on, else gamepad 1 if it emulates one.
        /// (The menus can turn both on; the deck shows the keys then.)
        /// </summary>
        public static int JoystickSourceAt(InputSettings input) =>
            input.EnableKeyboardJoystick && input.KeyboardJoystickType != 0 ? JoystickSourceCursor
            : input.Gamepad1Emulates != 0 ? JoystickSourceGamepad
            : JoystickSourceNone;

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

        /// <summary>
        /// The block and the tape's block count, as "03/12": two digits, or as many as the count
        /// needs up to four ("0042/1234"). Both sides take the same width, and stop at 9999.
        /// </summary>
        internal static string BlockReadout(int block, int count)
        {
            block = Math.Min(block, 9999);
            count = Math.Min(count, 9999);
            int digits = Math.Max(2, count.ToString().Length);
            return block.ToString().PadLeft(digits, '0') + "/" + count.ToString().PadLeft(digits, '0');
        }

        private static string RecordTarget(TapeDeck tape) =>
            System.IO.Path.GetFileNameWithoutExtension(tape.TapSavePath ?? "").ToUpperInvariant();

        private static void SetLit(Avalonia.StyledElement legend, bool lit) => legend.Classes.Set("off", !lit);
    }
}
