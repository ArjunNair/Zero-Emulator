using System;
using System.Windows.Input;

namespace Zero.App.Input
{
    /// <summary>Minimal ICommand over a delegate, for menu items.</summary>
    public sealed class ActionCommand : ICommand
    {
        private readonly Action _action;
        private bool _enabled = true;

        public ActionCommand(Action action) { _action = action; }

        public event EventHandler CanExecuteChanged;

        public bool IsEnabled
        {
            get => _enabled;
            set { if (_enabled != value) { _enabled = value; CanExecuteChanged?.Invoke(this, EventArgs.Empty); } }
        }

        public bool CanExecute(object parameter) => _enabled;
        public void Execute(object parameter) => _action();
    }
}
