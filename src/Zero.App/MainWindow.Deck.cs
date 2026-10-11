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

            d.PowerKey.Click += (_, __) => Close(); // asks first when Confirm on Exit is set
            WireResetKey(d.ResetKey);

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
            d.PauseKey.Click += (_, __) => TogglePause();

            d.CrtKey.Click += (_, __) => ToggleCrt();
            d.ScanKey.Click += (_, __) =>
            {
                _settings.Render.Crt.Scanlines = !_settings.Render.Crt.Scanlines;
                ApplyCrtSettings();
                RefreshDeck();
            };
            d.CurveFader.ValueChangedByUser += (_, v) => { _settings.Render.Crt.Curvature = v; ApplyCrtSettings(); };
            d.GlowFader.ValueChangedByUser += (_, v) => { _settings.Render.Crt.Glow = v; ApplyCrtSettings(); };

            // Switching machine resets it, so this knob only acts once it is let go (see CommitOnRelease).
            d.MachineKnob.ValueChangedByUser += (_, v) => SelectMachine(DeckPanel.MachineAt(v));
            d.StereoKnob.ValueChangedByUser += (_, v) => SelectStereo((int)v);
            d.CpuKnob.ValueChangedByUser += (_, v) => SelectCpuSpeed(DeckPanel.CpuMultiplierAt(v));
            d.VolumeKnob.ValueChangedByUser += (_, v) => _session.SetVolume((int)Math.Round(v));
            d.MuteKey.Click += (_, __) => SetMute(!_settings.Audio.Mute);
            d.TurboKey.Click += (_, __) => ToggleFullSpeed();
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
        private bool CassetteWanted => DecksFit && _settings.Render.ShowDeck;
        private bool ReceiverWanted => DecksFit && _settings.Render.ShowReceiver;
        private bool DeckWanted => CassetteWanted || ReceiverWanted;

        /// <summary>
        /// Show the decks that are switched on. The status bar stands in whenever the cassette deck
        /// is off, since its display is where the status otherwise goes.
        /// </summary>
        private void ApplyDeckVisibility(bool refit)
        {
            Deck.CassetteDeck.IsVisible = CassetteWanted;
            Deck.ReceiverDeck.IsVisible = ReceiverWanted;
            DeckShelf.IsVisible = DeckWanted;
            StatusBar.IsVisible = !CassetteWanted;
            // Grow or shrink the window by the change, so the screen keeps its size. ApplyWindowScale
            // measures the chrome from the current layout, so let the change land first.
            if (refit && WindowState == WindowState.Normal) { UpdateLayout(); ApplyWindowScale(); }
            RefreshDeck();
        }

        private void ToggleDeck(bool cassette)
        {
            if (cassette) _settings.Render.ShowDeck = !_settings.Render.ShowDeck;
            else _settings.Render.ShowReceiver = !_settings.Render.ShowReceiver;
            ApplyDeckVisibility(refit: true);
            RefreshMenuState();
        }

        /// <summary>
        /// The most the decks are enlarged. They are drawn small, so at their natural size the legends
        /// are hard to read; scaled to fill a 2x window they come out at about this.
        /// </summary>
        internal const double MaxDeckScale = 1.5;

        /// <summary>Below this the decks are not shrunk to keep the window on screen.</summary>
        private const double MinDeckScaleForScreen = 0.75;

        // Room left for the window's title bar, which the working area includes and ClientSize does not.
        private const double TitleBarAllowance = 32;

        private double _deckScaleLimit = MaxDeckScale;

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

        /// <summary>
        /// Cap the decks' scale so the window still fits on its screen, given the height the screen
        /// and the rest of the chrome already take. Scaling only trades away the decks' size; the
        /// emulated screen keeps the size the user picked.
        /// </summary>
        private void LimitDeckScaleToScreen(double heightWithoutDecks)
        {
            _deckScaleLimit = MaxDeckScale;
            Screen screen = Screens?.ScreenFromWindow(this) ?? Screens?.Primary;
            Size natural = NaturalDeckSize();
            if (screen == null || natural.Height <= 0 || !DeckWanted) return;
            double usable = screen.WorkingArea.Height / screen.Scaling - TitleBarAllowance;
            double room = usable - heightWithoutDecks - ShelfPadding(horizontal: false);
            _deckScaleLimit = Math.Clamp(room / natural.Height, MinDeckScaleForScreen, MaxDeckScale);
        }

        /// <summary>
        /// How tall the deck shelf will be in a window this wide. The decks sit in a Viewbox that
        /// scales them to the window's width, up to <see cref="_deckScaleLimit"/>; the Viewbox's
        /// MaxWidth enforces that limit. Window sizing needs the height up front: measuring after
        /// the resize would be one step behind.
        /// </summary>
        internal double DeckHeightFor(double windowWidth)
        {
            if (!DeckWanted) return 0;
            Size natural = NaturalDeckSize();
            if (natural.Width <= 0) return 0;

            DeckViewbox.MaxWidth = natural.Width * _deckScaleLimit;
            double room = windowWidth - ShelfPadding(horizontal: true);
            double scale = Math.Clamp(room / natural.Width, 0, _deckScaleLimit);
            return natural.Height * scale + ShelfPadding(horizontal: false);
        }
    }
}
