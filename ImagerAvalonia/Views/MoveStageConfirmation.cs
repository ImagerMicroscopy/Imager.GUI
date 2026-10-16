using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Threading.Tasks;

namespace ImagerAvalonia.Views
{
    /// <summary>
    /// Asks the user to confirm a stage move that is farther than the field
    /// viewer's far-move threshold. Modal over the main window when one
    /// exists, top-level otherwise (same pattern as ExceptionWindowHandler).
    /// </summary>
    public static class MoveStageConfirmation
    {
        public static async Task<bool> ShowAsync(double distanceUm)
        {
            var accepted = new TaskCompletionSource<bool>();

            var dialog = new Window
            {
                Background = new SolidColorBrush(Color.Parse("#454545")),
                Width = 400,
                Height = 170,
                Title = "Move stage",
                Content = new StackPanel
                {
                    Margin = new Thickness(10),
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Move the stage there?",
                            FontWeight = FontWeight.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(10),
                        },
                        new TextBlock
                        {
                            Text = $"The stage will move {distanceUm / 1000.0:F2} mm from its current position.",
                            TextWrapping = TextWrapping.Wrap,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(40, 0, 40, 10),
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Spacing = 20,
                            Children =
                            {
                                new Button { Content = "Move", Width = 80 },
                                new Button { Content = "Cancel", Width = 80 },
                            },
                        },
                    },
                },
            };

            var buttons = (StackPanel)((StackPanel)dialog.Content!).Children[^1];
            var moveButton = (Button)buttons.Children[0];
            var cancelButton = (Button)buttons.Children[1];

            moveButton.Click += (_, _) =>
            {
                accepted.TrySetResult(true);
                dialog.Close();
            };
            cancelButton.Click += (_, _) =>
            {
                accepted.TrySetResult(false);
                dialog.Close();
            };
            // The button handlers resolve the task before closing, so this
            // only fires on any other close path (title bar X): a cancellation.
            dialog.Closing += (_, _) => accepted.TrySetResult(false);

            Window? mainWindow = null;
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                mainWindow = desktop.MainWindow;

            if (mainWindow is not null)
            {
                await dialog.ShowDialog(mainWindow);
            }
            else
            {
                dialog.Show();
                await accepted.Task;
                dialog.Close();
            }

            return accepted.Task.Result;
        }
    }
}
