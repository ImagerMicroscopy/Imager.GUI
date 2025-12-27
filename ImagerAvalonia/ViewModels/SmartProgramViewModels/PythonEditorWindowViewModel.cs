using System.Collections.ObjectModel;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.TextMate;
using ReactiveUI;
using TextMateSharp.Grammars;


namespace ImagerAvalonia.ViewModels;

public class PythonEditorWindowViewModel(TextMate.Installation _textMateInstallation, RegistryOptions _registryOptions) : ReactiveObject
{
    public ObservableCollection<ThemeViewModel> AllThemes { get; set; } = [];
    private ThemeViewModel _selectedTheme;

    public ThemeViewModel SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTheme, value);
            _textMateInstallation.SetTheme(_registryOptions.LoadTheme(value.ThemeName));
        }
    }

    public void CopyMouseCommand(TextArea textArea)
    {
        ApplicationCommands.Copy.Execute(null, textArea);
    }

    public void CutMouseCommand(TextArea textArea)
    {
        ApplicationCommands.Cut.Execute(null, textArea);
    }
    
    public void PasteMouseCommand(TextArea textArea)
    {
        ApplicationCommands.Paste.Execute(null, textArea);
    }

    public void SelectAllMouseCommand(TextArea textArea)
    {
        ApplicationCommands.SelectAll.Execute(null, textArea);
    }

    // Undo Status is not given back to disable it's item in ContextFlyout; therefore it's not being used yet.
    public void UndoMouseCommand(TextArea textArea)
    {
        ApplicationCommands.Undo.Execute(null, textArea);
    }
}