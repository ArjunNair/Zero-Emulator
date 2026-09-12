using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Zero.Emulation;
using Zero.Emulation.Tape;

namespace Zero.App.Windows
{
    /// <summary>
    /// The tape deck: block list, transport buttons, loading options. A view over
    /// <see cref="TapeDeck"/>; every command is posted to the emulation thread.
    /// </summary>
    public partial class TapeDeckWindow : Window
    {
        private readonly EmulatorSession _session;
        private readonly Func<System.Threading.Tasks.Task> _insert;
        private List<TapeBlockInfo> _blocks = new List<TapeBlockInfo>();
        private bool _updatingOptions;

        public TapeDeckWindow() : this(null, null) { }

        public TapeDeckWindow(EmulatorSession session, Func<System.Threading.Tasks.Task> insertHandler)
        {
            InitializeComponent();
            _session = session;
            _insert = insertHandler;
            if (_session != null)
            {
                _session.Tape.Changed += OnTapeChanged;
                Closed += (_, __) => _session.Tape.Changed -= OnTapeChanged;
                Refresh();
            }
        }

        private void OnTapeChanged() => Dispatcher.UIThread.Post(Refresh);

        private void Refresh()
        {
            TapeDeck tape = _session.Tape;
            bool inserted = tape.IsInserted;

            TitleText.Text = inserted ? tape.Title : "No tape inserted";
            EjectButton.IsEnabled = RewindButton.IsEnabled = PrevButton.IsEnabled = NextButton.IsEnabled = inserted;
            PlayButton.IsEnabled = inserted && !tape.IsPlaying;
            StopButton.IsEnabled = inserted && tape.IsPlaying;

            IReadOnlyList<TapeBlockInfo> blocks = tape.Blocks;
            if (blocks.Count != _blocks.Count || !blocks.Select(b => b.Block + b.Info).SequenceEqual(_blocks.Select(b => b.Block + b.Info)))
            {
                _blocks = blocks.ToList();
                BlockList.ItemsSource = _blocks;
            }
            int current = Math.Clamp(tape.CurrentBlock, 0, Math.Max(0, _blocks.Count - 1));
            if (_blocks.Count > 0 && BlockList.SelectedIndex != current)
            {
                BlockList.SelectedIndex = current;
                BlockList.ScrollIntoView(_blocks[current]);
            }

            _updatingOptions = true;
            AutoLoadBox.IsChecked = tape.AutoLoad;
            AutoPlayBox.IsChecked = tape.AutoPlay;
            EdgeLoadBox.IsChecked = tape.EdgeLoad;
            FastLoadBox.IsChecked = tape.FastLoad;
            _updatingOptions = false;

            StatusText.Text = !inserted ? "No tape in tape deck."
                : tape.IsPlaying ? $"Playing block {tape.CurrentBlock + 1} of {blocks.Count}"
                : $"Stopped at block {Math.Min(tape.CurrentBlock + 1, blocks.Count)} of {blocks.Count}";
        }

        private void OnInsert(object sender, RoutedEventArgs e) { if (_insert != null) _ = _insert(); }
        private void OnEject(object sender, RoutedEventArgs e) => _session.Post(_session.Tape.Eject);
        private void OnRewind(object sender, RoutedEventArgs e) => _session.Post(_session.Tape.Rewind);
        private void OnPrev(object sender, RoutedEventArgs e) => _session.Post(_session.Tape.PreviousBlock);
        private void OnNext(object sender, RoutedEventArgs e) => _session.Post(_session.Tape.NextBlock);
        private void OnPlay(object sender, RoutedEventArgs e) => _session.Post(_session.Tape.Play);
        private void OnStop(object sender, RoutedEventArgs e) => _session.Post(_session.Tape.Stop);

        private void OnBlockDoubleTapped(object sender, RoutedEventArgs e)
        {
            int index = BlockList.SelectedIndex;
            if (index < 0) return;
            _session.Post(() =>
            {
                _session.Tape.Stop();
                _session.Tape.Rewind();
                for (int i = 0; i < index; i++) _session.Tape.NextBlock();
                _session.Tape.Play();
            });
        }

        private void OnOptionChanged(object sender, RoutedEventArgs e)
        {
            if (_updatingOptions) return;
            var s = _session.Settings.Tape;
            s.AutoLoad = AutoLoadBox.IsChecked == true;
            s.AutoPlay = AutoPlayBox.IsChecked == true;
            s.EdgeLoad = EdgeLoadBox.IsChecked == true;
            s.FastLoad = FastLoadBox.IsChecked == true;
            _session.ApplySettings();
        }
    }
}
