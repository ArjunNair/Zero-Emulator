using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Platform;
using Zero.App.Controls;
using Zero.Emulation;
using Zero.Emulation.Settings;

namespace Zero.App
{
    /// <summary>
    /// The hi-fi deck under the screen. Its keys and knobs run the same commands as the menus, and
    /// RefreshMenuState keeps its lights in step with the check marks.
    /// </summary>
    public partial class MainWindow
    {
        private void WireDeck()
        {
            DeckPanel d = Deck;

            d.DeckRequested += SelectDeck;
            d.DriveAEject.Click += (_, __) => EjectOrInsertDisk(0);
            d.DriveBEject.Click += (_, __) => EjectOrInsertDisk(1);
            d.DriveCEject.Click += (_, __) => EjectOrInsertDisk(2);
            d.DriveDEject.Click += (_, __) => EjectOrInsertDisk(3);

            d.PauseKey.Click += (_, __) => TogglePause();
            d.PowerKey.Click += (_, __) => Close(); // asks first when Confirm on Exit is set
            WireResetKey(d.ResetKey);
            d.NmiKey.Click += (_, __) => { _session.Nmi(); SetStatus("NMI"); };
            d.MultifaceKey.Click += (_, __) => ToggleMultiface();

            // Like a VCR, the eject key loads a tape when the deck is empty.
            d.EjectKey.Click += (_, __) =>
            {
                if (_session.Tape.IsInserted) _session.Post(_session.Tape.Eject);
                else _ = InsertTapeAsync();
            };
            d.RecKey.Click += (_, __) => { if (_recordArmed) DisarmRecord(); else _ = SetTapSaveTargetAsync(); };
            d.RewindKey.Click += (_, __) => _session.Post(_session.Tape.Rewind);
            d.PrevKey.Click += (_, __) => _session.Post(_session.Tape.PreviousBlock);
            d.PlayKey.Click += (_, __) => _session.Post(_session.Tape.Play);
            d.NextKey.Click += (_, __) => _session.Post(_session.Tape.NextBlock);
            d.StopKey.Click += (_, __) => _session.Post(_session.Tape.Stop);

            d.CrtKey.Click += (_, __) => ToggleCrt();
            d.ScanKey.Click += (_, __) =>
            {
                _settings.Render.Crt.Scanlines = !_settings.Render.Crt.Scanlines;
                ApplyCrtSettings();
                RefreshDeck();
            };
            d.CurveFader.ValueChangedByUser += (_, v) => { _settings.Render.Crt.Curvature = v; ApplyCrtSettings(); };
            d.VignetteKey.Click += (_, __) =>
            {
                _settings.Render.Crt.Vignette = !_settings.Render.Crt.Vignette;
                ApplyCrtSettings();
                RefreshDeck();
            };
            d.TubeSwitch.ValueChangedByUser += (_, v) => { _settings.Render.Crt.Tube = v == 1 ? "Trinitron" : "Spherical"; ApplyCrtSettings(); };
            d.EdgeFader.ValueChangedByUser += (_, v) => { _settings.Render.Crt.EdgeLight = v; ApplyCrtSettings(); };
            d.HousingFader.ValueChangedByUser += (_, v) => { _settings.Render.Crt.Bezel = v * DeckPanel.MaxHousing; ApplyCrtSettings(); };
            d.FilterKnob.ValueChangedByUser += (_, v) => SelectFilter(smooth: v >= 1, sharp: v >= 2);
            d.BorderKnob.ValueChangedByUser += (_, v) => SelectBorder(DeckPanel.BorderCropAt((int)v));
            d.FinishSwitch.ValueChangedByUser += (_, v) =>
            {
                _settings.Render.DeckFinish = v == 1 ? "Silver" : "Black";
                ApplyDeckFinish();
                RefreshDeck();
            };

            // Switching machine resets it, so the switch only reports once it is let go.
            d.MachineKnob.ValueChangedByUser += (_, v) => SelectMachine(DeckPanel.MachineAt((int)v));
            d.JoystickSwitch.ValueChangedByUser += (_, v) => SetDeckJoystick(DeckPanel.JoystickSourceAt(_settings.Input), v + 1);
            // The source keys are radio buttons: pressing the lit one leaves it lit (RefreshDeck relights them).
            d.JoystickCursorKey.Click += (_, __) => SetDeckJoystick(DeckPanel.JoystickSourceCursor, d.JoystickSwitch.Value + 1);
            d.JoystickGamepadKey.Click += (_, __) => SetDeckJoystick(DeckPanel.JoystickSourceGamepad, d.JoystickSwitch.Value + 1);
            d.JoystickNoneKey.Click += (_, __) => SetDeckJoystick(DeckPanel.JoystickSourceNone, d.JoystickSwitch.Value + 1);
            d.StereoKnob.ValueChangedByUser += (_, v) => SelectStereo((int)v);
            d.Ay48kKey.Click += (_, __) => ToggleAy48k();
            d.VolumeKnob.ValueChangedByUser += (_, v) => _session.SetVolume((int)Math.Round(v));
            d.MuteKey.Click += (_, __) => SetMute(!_settings.Audio.Mute);
        }

        /// <summary>How long RESET has to be held for a hard reset instead of a soft one.</summary>
        internal static readonly TimeSpan HardResetHold = TimeSpan.FromMilliseconds(800);

        /// <summary>
        /// RESET: a press is Machine > Reset (F9); held down, it is a hard reset (Shift+F9), done the
        /// moment the hold is long enough so the key needs no letting go to confirm it.
        /// </summary>
        private void WireResetKey(Button key)
        {
            bool heldToHardReset = false;
            var hold = new DispatcherTimer { Interval = HardResetHold };
            hold.Tick += (_, __) =>
            {
                hold.Stop();
                heldToHardReset = true;
                _session.Reset(true);
                SetStatus("Hard reset");
            };
            // Tunnel, and handled events too: the button marks its own presses handled.
            key.AddHandler(PointerPressedEvent, (_, e) =>
            {
                if (!e.GetCurrentPoint(key).Properties.IsLeftButtonPressed) return;
                heldToHardReset = false;
                hold.Start();
            }, RoutingStrategies.Tunnel, handledEventsToo: true);
            key.AddHandler(PointerReleasedEvent, (_, __) => hold.Stop(), RoutingStrategies.Tunnel, handledEventsToo: true);
            key.AddHandler(PointerCaptureLostEvent, (_, __) => hold.Stop(), RoutingStrategies.Tunnel, handledEventsToo: true);
            key.Click += (_, __) =>
            {
                if (heldToHardReset) { heldToHardReset = false; return; } // the hold already did it
                _session.Reset(false);
            };
        }

        /// <summary>
        /// The deck's joystick controls: one source plays one joystick. The cursor keys or gamepad 1
        /// emulate it and the other is turned off; with no source both are. The type is kept as the
        /// keys' in every case, so the switch remembers it while nothing plays it.
        /// </summary>
        private void SetDeckJoystick(int source, int type)
        {
            InputSettings input = _settings.Input;
            input.KeyboardJoystickType = type;
            input.EnableKeyboardJoystick = source == DeckPanel.JoystickSourceCursor;
            input.Gamepad1Emulates = source == DeckPanel.JoystickSourceGamepad ? type : 0;
            _session.ApplySettings();
            RefreshMenuState();
        }

        /// <summary>Dress the decks in the finish chosen in Options; anything unknown is black.</summary>
        internal void ApplyDeckFinish() =>
            Deck.Finish = Enum.TryParse(_settings.Render.DeckFinish, true, out DeckFinish finish) ? finish : DeckFinish.Black;

        /// <summary>Mirror the session onto the deck. A hidden deck is left alone.</summary>
        private void RefreshDeck()
        {
            if (DeckShelf.IsVisible) Deck.Show(_session, _settings, _recordArmed);
        }

        /// <summary>True once the user has picked the tape file SAVE writes to (Tape menu or REC).</summary>
        private bool _recordArmed;

        private string DefaultTapSavePath => Path.Combine(AppPaths.Resolve(_settings.Paths.Saves, "saves"), "zero_saved.tap");

        /// <summary>REC pressed again: SAVE goes back to the default file.</summary>
        private void DisarmRecord()
        {
            _recordArmed = false;
            string path = DefaultTapSavePath;
            _session.Post(() => _session.Tape.TapSavePath = path);
            SetStatus("SAVE output goes to " + Path.GetFileName(path));
            RefreshDeck();
        }

        // The decks are left off at 1x, where they would be shrunk past the point of being usable.
        private bool DecksFit => _settings.Render.WindowScale > 1;
        private bool DeckWanted => DecksFit && _settings.Render.ShowDeck;

        /// <summary>
        /// Show the decks, or the status bar in their place. Which deck shows makes no difference to
        /// the size: the deck area is always the taller of the two.
        /// </summary>
        private void ApplyDeckVisibility(bool refit)
        {
            Deck.SelectDeck(_settings.Render.ActiveDeck);
            DeckShelf.IsVisible = DeckWanted;
            StatusBar.IsVisible = !DeckWanted;
            // Grow or shrink the window by the change, so the screen keeps its size. ApplyWindowScale
            // measures the chrome from the current layout, so let the change land first.
            if (refit && WindowState == WindowState.Normal) { UpdateLayout(); ApplyWindowScale(); }
            RefreshDeck();
        }

        private void ToggleDeck()
        {
            _settings.Render.ShowDeck = !_settings.Render.ShowDeck;
            ApplyDeckVisibility(refit: true);
            RefreshMenuState();
        }

        /// <summary>Show deck A (cassette), B (receiver) or C (video), from the DECK keys or the menu.</summary>
        private void SelectDeck(int deck)
        {
            _settings.Render.ActiveDeck = Math.Clamp(deck, 0, 2);
            Deck.SelectDeck(_settings.Render.ActiveDeck);
            RefreshMenuState();
        }

        /// <summary>
        /// The Multiface switch: fit the interface that suits the machine, or take it away. The
        /// session says if the ROM could not be loaded, and the switch goes back off.
        /// </summary>
        private void ToggleMultiface()
        {
            _settings.Emulation.EnableMultiface = !_settings.Emulation.EnableMultiface;
            _session.ApplyMultiface();
            RefreshDeck();
        }

        /// <summary>A drive's eject button: out with the disk, or, with none in, choose one to put in.</summary>
        private void EjectOrInsertDisk(int unit)
        {
            if (_session.DiskInDrive(unit) != null) _session.EjectDisk(unit);
            else _ = InsertDiskAsync(unit);
        }

        private Size NaturalDeckSize()
        {
            Control deck = DeckViewbox.Child;
            if (deck == null) return default;
            deck.Measure(Size.Infinity); // which decks are showing may have just changed
            return deck.DesiredSize;
        }

        private double ShelfPadding(bool horizontal)
        {
            Thickness pad = DeckShelf.Padding, edge = DeckShelf.BorderThickness;
            return horizontal ? pad.Left + pad.Right + edge.Left + edge.Right : pad.Top + pad.Bottom + edge.Top + edge.Bottom;
        }

        // Width over height of the picture as shown, which the border setting changes.
        private double PictureAspect()
        {
            Rect source = Display.SourceRect;
            return source.Height > 0 ? source.Width / source.Height : 352.0 / 296;
        }

        // How tall the deck shelf is in a window this wide, with the decks filling its width. Window
        // sizing needs the height up front: measuring after the resize would be one step behind.
        internal double DeckHeightFor(double windowWidth)
        {
            if (!DeckWanted) return 0;
            Size natural = NaturalDeckSize();
            if (natural.Width <= 0) return 0;
            double scale = Math.Max(0, windowWidth - ShelfPadding(horizontal: true)) / natural.Width;
            return natural.Height * scale + ShelfPadding(horizontal: false);
        }

        // Tell the shelf the picture's shape, which it sizes the decks by (see DeckShelfBorder): the
        // border setting changes it, and a picture stretched to fill the window has none.
        private void UpdateDeckAspect() =>
            DeckShelf.PictureAspect = _settings.Render.MaintainAspectRatio ? PictureAspect() : 0;
    }
}
