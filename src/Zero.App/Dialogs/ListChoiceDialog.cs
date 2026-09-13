using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Zero.App.Dialogs
{
    /// <summary>Pick one entry from a list (used for ZIP archives holding several files).</summary>
    public static class ListChoiceDialog
    {
        public static async Task<string> ShowAsync(Window owner, string title, string prompt, IReadOnlyList<string> items)
        {
            string result = null;
            var list = new ListBox { ItemsSource = items, SelectedIndex = 0, MinWidth = 420, MaxHeight = 320 };
            var ok = new Button { Content = "Open", MinWidth = 90, IsDefault = true, Classes = { "accent" } };
            var cancel = new Button { Content = "Cancel", MinWidth = 90, IsCancel = true };
            var dialog = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = prompt },
                        list,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 8,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Children = { cancel, ok }
                        }
                    }
                }
            };
            ok.Click += (_, __) => { result = list.SelectedItem as string; dialog.Close(); };
            cancel.Click += (_, __) => dialog.Close();
            list.DoubleTapped += (_, __) => { result = list.SelectedItem as string; dialog.Close(); };
            await dialog.ShowDialog(owner);
            return result;
        }
    }
}
