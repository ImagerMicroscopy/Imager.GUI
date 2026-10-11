using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ImagerAvalonia.Services.Workspace;

/// <summary>
/// Handles file I/O for experiment output.
/// Manages output folder, filename generation, and uniqueness checking.
/// Raises change notifications so bound UI (e.g. the "Save path" display) stays
/// in sync when the output folder is picked.
/// </summary>
public partial class ExperimentStorageService : ObservableObject
{
    [ObservableProperty] private string _outputFolder = string.Empty;
    [ObservableProperty] private string _fileName = string.Empty;
    [ObservableProperty] private bool _isExperimentStorageEnabled = true;

    /// <summary>
    /// Gets the output folder after validating it exists.
    /// </summary>
    public string GetOutputFolder()
    {
        if (string.IsNullOrEmpty(OutputFolder) || !System.IO.Directory.Exists(OutputFolder))
        {
            throw new Exception("Output folder does not exist");
        }

        return OutputFolder;
    }

    /// <summary>
    /// Gets a filename with auto-incrementing suffix to ensure uniqueness.
    /// </summary>
    public string GetFileName()
    {
        if (string.IsNullOrEmpty(FileName))
        {
            throw new Exception("File name can not be empty");
        }

        int num = 0;
        string formattedNumber = num.ToString($"D{6}");

        while (System.IO.File.Exists(System.IO.Path.Combine(GetOutputFolder(), $"{FileName}{formattedNumber}.tif")))
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

    /// <summary>
    /// Gets a unique filename that does not conflict with previously existing files.
    /// </summary>
    public string GetUniqueFileName()
    {
        if (string.IsNullOrEmpty(FileName))
        {
            throw new Exception("File name can not be empty");
        }

        var prevFileNames = new List<string>(System.IO.Directory.GetFiles(GetOutputFolder()));
        var prefixes = prevFileNames.Select(p => System.IO.Path.GetFileNameWithoutExtension(p)).ToList();

        int num = 0;
        string formattedNumber = num.ToString($"D{6}");

        while (System.IO.File.Exists(System.IO.Path.Combine(GetOutputFolder(), $"{FileName}{formattedNumber}.tif"))
            || prefixes.Contains($"{FileName}{formattedNumber}"))
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

    /// <summary>
    /// Gets the full storage path for the current experiment output.
    /// </summary>
    public string GetStoragePath()
    {
        return System.IO.Path.Combine(GetOutputFolder(), $"{GetUniqueFileName()}.tif");
    }

    /// <summary>
    /// Allows user to select an output folder via dialog.
    /// </summary>
    public async void SelectOutputFolder()
    {
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
                // TryGetLocalPath returns the decoded local path; Path.ToString() keeps
                // percent-encoding, which would not match a folder with spaces on disk.
                OutputFolder = files[0].TryGetLocalPath() ?? string.Empty;
            }
        }
        catch (Exception)
        {
            // Keep the previously selected folder - a failed/cancelled picker must
            // not silently wipe it.
        }
    }
}
