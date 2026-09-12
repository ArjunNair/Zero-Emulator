using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Zero.App.Dialogs
{
    /// <summary>Minimal modal message box (Avalonia has none built in).</summary>
    public static class MessageDialog
    {
        public static Task ShowAsync(Window owner, string title, string message)
        {
            var ok = new Button { Content = "OK", MinWidth = 90, HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true, IsCancel = true };
            var dialog = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                MaxWidth = 560,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 16,
                    Children =
                    {
                        new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 500 },
                        ok
                    }
                }
            };
            ok.Click += (_, __) => dialog.Close();
            return dialog.ShowDialog(owner);
        }

        public static async Task<bool> ConfirmAsync(Window owner, string title, string message, string yes = "Yes", string no = "No")
        {
            bool result = false;
            var yesButton = new Button { Content = yes, MinWidth = 90, IsDefault = true };
            var noButton = new Button { Content = no, MinWidth = 90, IsCancel = true };
            var dialog = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 16,
                    Children =
                    {
                        new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 500 },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Children = { noButton, yesButton }
                        }
                    }
                }
            };
            yesButton.Click += (_, __) => { result = true; dialog.Close(); };
            noButton.Click += (_, __) => dialog.Close();
            await dialog.ShowDialog(owner);
            return result;
        }
    }
}
