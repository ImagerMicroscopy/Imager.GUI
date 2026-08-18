using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using System;
using System.Threading.Tasks;

namespace ImagerAvalonia.Exceptions
{
    public static class ExceptionWindowHandler
    {
        public static async Task ShowExceptionAsync(Exception ex)
        {
            Window? mainWindow = null;

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                mainWindow = desktop.MainWindow;
            }

            await ExceptionWindowHandler.ShowDialogAsync(
                "Error", ex.Message, ex.StackTrace, mainWindow);
        }

        public static async Task ShowDialogAsync(string title, string message, string? callStack, Window? mainWindow)
        {
            // Create the dialog window
            var dialog = new Window
            {
                Background = new SolidColorBrush(Color.Parse("#454545")),
                Width = 400,
                Height = 250, // Increase height to accommodate call stack
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            FontWeight = Avalonia.Media.FontWeight.Bold,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            Margin = new Avalonia.Thickness(10)
                        },
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            Margin = new Avalonia.Thickness(40)
                        },
                        new ScrollViewer
                        {
                            Content = new TextBlock
                            {
                                Text = callStack,
                                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                                Margin = new Avalonia.Thickness(10),
                                FontFamily = new Avalonia.Media.FontFamily("Courier New"), // Monospaced font for better readability
                                FontSize = 12
                            },
                            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                            Margin = new Avalonia.Thickness(10)
                        },
                        new Button
                        {
                            Content = "OK",
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            Margin = new Avalonia.Thickness(10)
                        }
                    }
                }
            };

            var okButton = ((StackPanel)dialog.Content).Children[3] as Button;
            if (okButton is not null)
            {
                okButton.Click += (s, e) =>
                {
                    dialog.Close();
                };
            }


            var tcs = new TaskCompletionSource();
            dialog.Closed += (s, e) => tcs.TrySetResult();

            if (mainWindow is not null)
            {
                await dialog.ShowDialog(mainWindow);
            }
            else
            {
                dialog.Show();
                await tcs.Task;
            }
        }
    }
}