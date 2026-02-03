using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.TextMate;
using ImagerAvalonia.Services;
using ImagerAvalonia.Views;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Threading.Tasks;
using TextMateSharp.Grammars;


namespace ImagerAvalonia.ViewModels;



public class PythonEditorWindowViewModel : ReactiveObject
{
    private readonly IPythonCom _pythonCom;
    private readonly IPythonLinting _pythonLinting;

    public event EventHandler<List<DiagnosticFeedback>>? DiagnosticsReceived;
    public event EventHandler<List<HoverFeedback>>? HoverResponseReceived;

    private LineInfo _caretPosition;
    public LineInfo CaretPosition
    {
        get => _caretPosition;
        set 
        {
            Task.Run(() => GetDiagnostics());
            this.RaiseAndSetIfChanged(ref _caretPosition, value);   
        }
    }

    private LineInfo _hoverPosition;
    public LineInfo HoverPosition
    {
        get => _hoverPosition;
        set
        {
            Task.Run(() => GetHover(value.Line, value.Column));
            this.RaiseAndSetIfChanged(ref _caretPosition, value);
        }
    }

    public string Code { get; set; }

    public PythonEditorWindowViewModel(IPythonCom pythonCom, IPythonLinting pythonLinting)
    {
        _pythonCom = pythonCom;
        _pythonLinting = pythonLinting;
    }

    private async Task GetDiagnostics()
    {
        try
        {
            var response = await _pythonLinting.GetDiagnostics(Code, SavePath);
            if (string.IsNullOrEmpty(response)) return;
            var diagnosticfeedback = JArray.Parse(response).ToObject<List<DiagnosticFeedback>>();
            if (diagnosticfeedback is not null)
                DiagnosticsReceived?.Invoke(this, diagnosticfeedback);
        }
        catch { }
    }

    private async Task GetHover(int line, int column)
    {
        try
        {
            var response = await _pythonLinting.GetHover(Code,line, column, SavePath);
            if (string.IsNullOrEmpty(response)) return;
            var hoverfeedback = JArray.Parse(response).ToObject<List<HoverFeedback>>();
            if (hoverfeedback is not null)
                HoverResponseReceived?.Invoke(this, hoverfeedback);
        }
        catch { }
    }

    public async Task<List<CompletionItem>> GetCompletion(string code, int line,int col,string  _savePath)
    {
        try
        {
            var response = await _pythonLinting.GetCompletions(code, line, col, _savePath);
            if (string.IsNullOrEmpty(response)) return null;
            var completionfeedback = JArray.Parse(response).ToObject<List<CompletionItem>>();
            if (completionfeedback is not null)
                return completionfeedback;
            else
                return new List<CompletionItem>();
                
        }
        catch {
            return new List<CompletionItem>();
        }
    }

    public ObservableCollection<ThemeViewModel> AllThemes { get; set; } = [];
    public string SavePath { get; internal set; }

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

public record LineInfo
{
    public LineInfo(int line, int column)
    {
        Line = line;
        Column = column;
    }

    public int Line { get; set; }
    public int Column { get; set; }
}

public record DiagnosticFeedback
{
    public int line { get; set; }
    public int column { get; set; }
    public string? message { get; set; }
}

public record HoverFeedback(
    string? FullName,
    string? Name,
    string? Type,
    string? Docstring,
    string? Description
);


public record CompletionItem(
    string? Name,
    string? Description,
    string? Type,
    string? Docstring
);
