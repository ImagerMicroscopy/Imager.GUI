using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.TextMate;
using ImagerAvalonia.Services;
using ImagerAvalonia.Views;
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

            DiagnosticsReceived?.Invoke(this, diagnosticfeedback);

            //Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            //{

            //    _textMarkerService.RemoveAll(m => true);

            //    var errors = JArray.Parse(json);
            //    foreach (var err in errors)
            //    {
            //        var l = err["line"]?.ToObject<int>();
            //        var c = err["column"]?.ToObject<int>();
            //        var msg = err["message"]?.ToString();

            //        if (l.HasValue && c.HasValue)
            //        {

            //            //var lineObj = _textEditor.Document.GetLineByNumber(l.Value);
            //            //int offset = lineObj.Offset + c.Value;
            //            //int length = Math.Max(1, lineObj.Length - c.Value);


            //            //if (offset < 0) offset = 0;
            //            //if (offset + length > _textEditor.Document.TextLength) length = _textEditor.Document.TextLength - offset;

            //            //if (length > 0)
            //            //{
            //            //    var m = _textMarkerService.Create(offset, length);
            //            //    m.MarkerType = TextMarkerType.SquigglyUnderline;
            //            //    m.MarkerColor = Colors.Red;
            //            //    m.ToolTip = msg;
            //            //}
            //        }
            //    }

            //});
        }
        catch { }

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
