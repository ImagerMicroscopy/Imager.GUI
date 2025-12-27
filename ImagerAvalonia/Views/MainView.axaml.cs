using Autofac;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ImagerAvalonia.Utils;
using ImagerAvalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;


namespace ImagerAvalonia.Views;

public partial class MainView : UserControl
{
    private Button start_button;

    public Action? InitializeDataContextEquipment;
    public Action? InitializeImageControlPanel;

    public MainView()
    {
        InitializeComponent();



        MainViewModel mainVM = App.Container.Resolve<MainViewModel>();
        DataContext = mainVM;
        InitializeDataContextEquipment = mainVM.InitializeEquipment;
        InitializeImageControlPanel = mainVM.InitializeImageControlPanel;
        mainVM.OnProgramStorageRequested += SaveImagerProgram;
        mainVM.OnProgramLoadRequested += LoadImagerProgram;
    }

    private void OpenExperimentPanel(object sender, RoutedEventArgs e)
    {

        ListBox ClickedItem = (ListBox)sender;
        ExperimentalPanelViewModel experiment = (ExperimentalPanelViewModel)ClickedItem.SelectedItem;


    }


    private void IncreaseValue(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.TemplatedParent is NumericUpDown numericUpDown)
        {
            if (numericUpDown.Value == null)
            {
                numericUpDown.Value = numericUpDown.Minimum;
            }
            else
            {
                numericUpDown.Value = Math.Min((decimal)numericUpDown.Value + (decimal)numericUpDown.Increment, (decimal)numericUpDown.Maximum);
            }
        }
    }


    private void DecreaseValue(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.TemplatedParent is NumericUpDown numericUpDown)
        {
            if (numericUpDown.Value == null)
            {
                numericUpDown.Value = numericUpDown.Minimum;
            }
            else
            {
                numericUpDown.Value = Math.Max((decimal)numericUpDown.Value - (decimal)numericUpDown.Increment, (decimal)numericUpDown.Minimum);
            }
        }
    }

    private void OnLiveViewEnabledDisabled(object? sender, RoutedEventArgs e)
    {
        if (sender is Button s)
        {
            var content_value = s.Content as string;
            if (content_value == null) return;

            if (content_value == "Stop")
            {
                s.Content = "Live";
                s.Background = Brushes.LightGreen;
                start_button.IsEnabled = true;

                //record_button.IsEnabled = true;
                //snap_button.IsEnabled = true;
            }
            else
            {
                s.Background = Brushes.Red;
                s.Content = "Stop";
                start_button.IsEnabled = false;
                //snap_button.IsEnabled = false;

            }
        }
    }
    private async void SaveImagerProgram(object sender, RoutedEventArgs args)
    {
        if (sender is string program)
        {


            var topLevel = TopLevel.GetTopLevel(this);
            var customImagerFileType = new FilePickerFileType("Only Imager programs")
            {
                Patterns = new[] { "*.imag" },
               
            };
            // Start async operation to open the dialog.
            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = new List<FilePickerFileType>() { customImagerFileType },
                Title = "Save Imager Program"
            });
            if (file != null)
            {
                var localPath = file.TryGetLocalPath();
                if (localPath!=null)
                {
                    if (!localPath.EndsWith(".imag", StringComparison.OrdinalIgnoreCase))
                        localPath += ".imag";

                    using var fs = File.Create(localPath);
                    byte[] programVals = Encoding.UTF8.GetBytes(program);
                    fs.Write(programVals, 0, programVals.Length);
                }
                else
                {
                    await using var stream = await file.OpenWriteAsync();
                    byte[] programVals = Encoding.UTF8.GetBytes(program);
                    await stream.WriteAsync(programVals, 0, programVals.Length);
                }
            }
        }
    }

    private async void LoadImagerProgram(object sender)
    {


        var topLevel = TopLevel.GetTopLevel(this);
        var customImagerFileType = new FilePickerFileType("Only Imager programs")
        {
            Patterns = new[] { "*.imag" },

        };
        // Start async operation to open the dialog.
        var file = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            FileTypeFilter = new List<FilePickerFileType>() { customImagerFileType },
            Title = "Load Imager program"
        });
        if (file.Count == 1)
        {
            // Open reading stream from the first file.
            await using var stream = await file[0].OpenReadAsync();
            using var streamReader = new StreamReader(stream);
            // Reads all the content of file as a text.
            var fileContent = await streamReader.ReadToEndAsync();
            if(DataContext is MainViewModel mainVM)
            {
                mainVM.ParseLoadedExperiment(fileContent);
            }
        }

    }
}