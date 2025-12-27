
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using Avalonia.Controls;

using System.Linq;
using ImagerAvalonia.Exceptions;


namespace ImagerAvalonia.ViewModels;

public partial class RootPanelViewModel : MeasurementViewModel
{
    [ObservableProperty]
    private string _OutputFolder;

    [ObservableProperty]
    private string _FileName;

    [ObservableProperty]
    private bool _isStorageEnabled = true;


    public string GetOutputFolder()
    { 
        if( string.IsNullOrEmpty(OutputFolder) || !System.IO.Directory.Exists(OutputFolder))
        {
            throw new Exception("Output folder does not exist");

        }
        return OutputFolder;
    }

    public string GetFileName()
    {
        if (string.IsNullOrEmpty(FileName))
        {
            throw new Exception("File name can not be empty");

        }


        int num = 0;
        string formattedNumber = num.ToString($"D{6}");

        while (System.IO.File.Exists(System.IO.Path.Combine(OutputFolder,$"{FileName}{formattedNumber}.tif")))
        {
            num++;
            formattedNumber = num.ToString($"D{6}");
            if (num > 999999)
            {
                throw new Exception("Your folder contains more than 999999 images with the same prefix. This is a bit too much for the software to handle.");
            }

        }

        return $"{FileName}{formattedNumber}";
    }

    public string GetUniqueFileName()
    {
        // Gets unique filename that is not encountered in the list of previously provided names. 

        List<string> prev_file_names = new List<string>(System.IO.Directory.GetFiles(this.OutputFolder));


        if (string.IsNullOrEmpty(FileName))
        {
            throw new Exception("File name can not be empty");

        }
        List<string> prefixes = prev_file_names.Select(p => System.IO.Path.GetFileNameWithoutExtension(p)).ToList();

        int num = 0;
        string formattedNumber = num.ToString($"D{6}");


        while (System.IO.File.Exists(System.IO.Path.Combine(OutputFolder, $"{FileName}{formattedNumber}.tif")) || 
            prefixes.Contains($"{FileName}{formattedNumber}"))
        {
            num++;
            formattedNumber = num.ToString($"D{6}");
            if (num > 999999)
            {
                throw new Exception("Your folder contains more than 999999 images with the same prefix. This is a bit too much for the software to handle.");
            }

        }

        return $"{FileName}{formattedNumber}";



    }

    public async void SelectOutputFolder()
    {
        //var topLevel = TopLevel.GetTopLevel(this);
        var topLevel = new Window();

        try
        {
            var files = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Folder",
                AllowMultiple = false

            });
            if (files.Count != 0)
            {
                OutputFolder = files[0].Path.ToString();
                OutputFolder = OutputFolder.Replace("file:///", "");
            }
        }
        catch (Exception ex)
        {
            await ExceptionWindowHandler.ShowExceptionAsync(ex);
            OutputFolder = string.Empty;
        }

    }

    public RootPanelViewModel()
    {

    }
}

