using Avalonia;
using Avalonia.Controls;
using Avalonia.Diagnostics;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using AvaloniaEdit.CodeCompletion;
using ImagerAvalonia.PythonEditor.Resources;
using ImagerAvalonia.ViewModels;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Snippets;
using AvaloniaEdit.TextMate;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TextMateSharp.Grammars;
using Snippet = AvaloniaEdit.Snippets.Snippet;
using AvaloniaEdit;
using System.IO;
using Autofac;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ImagerAvalonia.Services;
using System.Threading.Tasks;
using Avalonia.Threading;
using System.Text;
using System.ComponentModel.Design;
namespace ImagerAvalonia.Views
{
    using Pair = KeyValuePair<int, Control>;

    public partial class EditorView : UserControl
    {
        private readonly AvaloniaEdit.TextEditor _textEditor;
        private FoldingManager _foldingManager;
        private readonly TextMate.Installation _textMateInstallation;
        private CompletionWindow _completionWindow;
        private OverloadInsightWindow _insightWindow;
        private Button _addControlButton;
        private Button _clearControlButton;
        private Button _insertSnippetButton;
        private ComboBox _syntaxModeCombo;
        private TextBlock _statusTextBlock;
        private ElementGenerator _generator = new ElementGenerator();
        private RegistryOptions _registryOptions;
        private int _currentTheme = (int)ThemeName.DarkPlus;
        private string? _savePath;

        public event EventHandler<string> OnReloadRequested;

        public EditorView()
        {
            InitializeComponent();


            _textEditor = this.FindControl<TextEditor>("Editor");
            _textEditor.HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Visible;
            _textEditor.Background = Brushes.Transparent;
            _textEditor.ShowLineNumbers = true;
            _textEditor.TextArea.Background = this.Background;
            _textEditor.TextArea.Options.ConvertTabsToSpaces = true;
            _textEditor.TextArea.TextEntered += textEditor_TextArea_TextEntered;
            _textEditor.TextArea.TextEntering += textEditor_TextArea_TextEntering;
            _textEditor.Options.AllowToggleOverstrikeMode = true;
            _textEditor.Options.EnableTextDragDrop = true;
            _textEditor.Options.ConvertTabsToSpaces = true;
            _textEditor.Options.ShowBoxForControlCharacters = true;
            _textEditor.Options.ColumnRulerPositions = new List<int>() { 80, 100 };
            _textEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
            _textEditor.TextArea.RightClickMovesCaret = true;
            _textEditor.Options.HighlightCurrentLine = true;
            _textEditor.Options.CompletionAcceptAction = CompletionAcceptAction.DoubleTapped;

            _addControlButton = this.FindControl<Button>("addControlBtn");

            _clearControlButton = this.FindControl<Button>("clearControlBtn");

            _insertSnippetButton = this.FindControl<Button>("insertSnippetBtn");

            _textEditor.TextArea.TextView.ElementGenerators.Add(_generator);

            _registryOptions = new RegistryOptions(
                (ThemeName)_currentTheme);

            _textMateInstallation = _textEditor.InstallTextMate(_registryOptions);

            _textMateInstallation.AppliedTheme += TextMateInstallationOnAppliedTheme;

            Language pythonLanguage = _registryOptions.GetLanguageByExtension(".py");

            _syntaxModeCombo = this.FindControl<ComboBox>("syntaxModeCombo");
            _syntaxModeCombo.ItemsSource = _registryOptions.GetAvailableLanguages();
            _syntaxModeCombo.SelectedItem = pythonLanguage;
            _syntaxModeCombo.SelectionChanged += SyntaxModeCombo_SelectionChanged;

            string scopeName = _registryOptions.GetScopeByLanguageId(pythonLanguage.Id);

            _textMateInstallation.SetGrammar(_registryOptions.GetScopeByLanguageId(pythonLanguage.Id));
            _textEditor.TextArea.TextView.LineTransformers.Add(new UnderlineAndStrikeThroughTransformer());
            _statusTextBlock = this.Find<TextBlock>("StatusText");

            this.AddHandler(PointerWheelChangedEvent, (o, i) =>
            {
                if (i.KeyModifiers != KeyModifiers.Control) return;
                if (i.Delta.Y > 0) _textEditor.FontSize++;
                else _textEditor.FontSize = _textEditor.FontSize > 1 ? _textEditor.FontSize - 1 : 1;
            }, RoutingStrategies.Bubble, true);

            var PythonEditorVM = new PythonEditorWindowViewModel(_textMateInstallation, _registryOptions);
            foreach (ThemeName themeName in Enum.GetValues<ThemeName>())
            {
                var themeViewModel = new ThemeViewModel(themeName);
                PythonEditorVM.AllThemes.Add(themeViewModel);
                if (themeName == ThemeName.DarkPlus)
                {
                    PythonEditorVM.SelectedTheme = themeViewModel;
                }
            }
            DataContext = PythonEditorVM;
            
            InitializeSmartFeatures();
        }

        public void SetDocument(string documentPath)
        {
            _savePath = documentPath;
            _textEditor.Document = new TextDocument(
                ResourceLoader.LoadSampleFile(documentPath));
        }

        public void SaveDocument(object? sender, RoutedEventArgs e)
        {
            if (_savePath is not null)
            {
                using (FileStream fs = new FileStream(_savePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    _textEditor.Save(fs);
                }
                OnReloadRequested?.Invoke(this, _savePath);
            }
        }

        private void TextMateInstallationOnAppliedTheme(object sender, TextMate.Installation e)
        {
            ApplyThemeColorsToEditor(e);
            ApplyThemeColorsToWindow(e);
        }

        void ApplyThemeColorsToEditor(TextMate.Installation e)
        {
            ApplyBrushAction(e, "editor.background", brush => _textEditor.Background = brush);
            ApplyBrushAction(e, "editor.foreground", brush => _textEditor.Foreground = brush);

            if (!ApplyBrushAction(e, "editor.selectionBackground",
                    brush => _textEditor.TextArea.SelectionBrush = brush))
            {
                if (Application.Current!.TryGetResource("TextAreaSelectionBrush", out var resourceObject))
                {
                    if (resourceObject is IBrush brush)
                    {
                        _textEditor.TextArea.SelectionBrush = brush;
                    }
                }
            }

            if (!ApplyBrushAction(e, "editor.lineHighlightBackground",
                    brush =>
                    {
                        _textEditor.TextArea.TextView.CurrentLineBackground = brush;
                        _textEditor.TextArea.TextView.CurrentLineBorder = new Pen(brush);
                    }))
            {
                _textEditor.TextArea.TextView.SetDefaultHighlightLineColors();
            }


            if (!ApplyBrushAction(e, "editorLineNumber.foreground",
                    brush => _textEditor.LineNumbersForeground = brush))
            {
                _textEditor.LineNumbersForeground = _textEditor.Foreground;
            }
        }

        private void ApplyThemeColorsToWindow(TextMate.Installation e)
        {
            var panel = this.Find<StackPanel>("StatusBar");
            if (panel == null)
            {
                return;
            }

            if (!ApplyBrushAction(e, "statusBar.background", brush => panel.Background = brush))
            {
                panel.Background = Brushes.Purple;
            }

            if (!ApplyBrushAction(e, "statusBar.foreground", brush => _statusTextBlock.Foreground = brush))
            {
                _statusTextBlock.Foreground = Brushes.White;
            }
            ApplyBrushAction(e, "editor.background", brush => Background = brush);
            ApplyBrushAction(e, "editor.foreground", brush => Foreground = brush);
        }

        bool ApplyBrushAction(TextMate.Installation e, string colorKeyNameFromJson, Action<IBrush> applyColorAction)
        {
            if (!e.TryGetThemeColor(colorKeyNameFromJson, out var colorString))
                return false;

            if (!Color.TryParse(colorString, out Color color))
                return false;

            var colorBrush = new SolidColorBrush(color);
            applyColorAction(colorBrush);
            return true;
        }

        private void Caret_PositionChanged(object sender, EventArgs e)
        {
            _statusTextBlock.Text = string.Format("Line {0} Column {1}",
                _textEditor.TextArea.Caret.Line,
                _textEditor.TextArea.Caret.Column);
        }



        private void SyntaxModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RemoveUnderlineAndStrikethroughTransformer();

            Language language = (Language)_syntaxModeCombo.SelectedItem;

            if (_foldingManager != null)
            {
                _foldingManager.Clear();
                FoldingManager.Uninstall(_foldingManager);
            }

            string scopeName = _registryOptions.GetScopeByLanguageId(language.Id);

            _textMateInstallation.SetGrammar(null);
            _textEditor.Document = new TextDocument(ResourceLoader.LoadSampleFile(scopeName));
            _textMateInstallation.SetGrammar(scopeName);

            if (language.Id == "xml")
            {
                _foldingManager = FoldingManager.Install(_textEditor.TextArea);

                var strategy = new XmlFoldingStrategy();
                strategy.UpdateFoldings(_foldingManager, _textEditor.Document);
                return;
            }
        }

        private void RemoveUnderlineAndStrikethroughTransformer()
        {
            for (int i = _textEditor.TextArea.TextView.LineTransformers.Count - 1; i >= 0; i--)
            {
                if (_textEditor.TextArea.TextView.LineTransformers[i] is UnderlineAndStrikeThroughTransformer)
                {
                    _textEditor.TextArea.TextView.LineTransformers.RemoveAt(i);
                }
            }
        }

        
        private DispatcherTimer _hoverTimer;
        private Point _lastHoverPos;
        private bool _hoverOpen = false;
        private DispatcherTimer _diagnosticsTimer;
        private EditorTextMarkerService _textMarkerService;

        private void InitializeSmartFeatures()
        {
            _hoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _hoverTimer.Tick += HoverTimer_Tick;
            
            _textEditor.PointerMoved += TextEditor_PointerMoved;
            _textEditor.PointerExited += (s, e) => {
                 _hoverTimer.Stop();
                 ToolTip.SetIsOpen(_textEditor, false);
            };


            _textMarkerService = new EditorTextMarkerService(_textEditor.Document);
            _textEditor.TextArea.TextView.BackgroundRenderers.Add(_textMarkerService);
            _textEditor.TextArea.TextView.LineTransformers.Add(_textMarkerService);

            _diagnosticsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
            _diagnosticsTimer.Tick += DiagnosticsTimer_Tick;
            _textEditor.TextChanged += (s, e) => {
                _diagnosticsTimer.Stop();
                _diagnosticsTimer.Start();
            };
        }

        private void DiagnosticsTimer_Tick(object sender, EventArgs e)
        {
             _diagnosticsTimer.Stop();
             var service = App.Container.Resolve<IPythonComService>();
             var code = _textEditor.Text;
             
             Task.Run(async () => {
                 var json = await service.GetDiagnostics(code, _savePath);
                 if (string.IsNullOrEmpty(json)) return;
                 
                  Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                      try {
                          _textMarkerService.RemoveAll(m => true);
                          
                          var errors = JArray.Parse(json);
                          foreach(var err in errors)
                          {
                              var l = err["line"]?.ToObject<int>();
                              var c = err["column"]?.ToObject<int>();
                              var msg = err["message"]?.ToString();
                              
                              if (l.HasValue && c.HasValue)
                              {
                                   
                                   var lineObj = _textEditor.Document.GetLineByNumber(l.Value);
                                   int offset = lineObj.Offset + c.Value;
                                   int length = Math.Max(1, lineObj.Length - c.Value);
                                   
                                   
                                   if (offset < 0) offset=0;
                                   if (offset + length > _textEditor.Document.TextLength) length = _textEditor.Document.TextLength - offset;

                                   if (length > 0)
                                   {
                                      var m = _textMarkerService.Create(offset, length);
                                      m.MarkerType = TextMarkerType.SquigglyUnderline;
                                      m.MarkerColor = Colors.Red;
                                      m.ToolTip = msg;
                                   }
                              }
                          }
                      } catch {}
                  });
             });
        }

        private void TextEditor_PointerMoved(object sender, PointerEventArgs e)
        {
             _lastHoverPos = e.GetPosition(_textEditor);
             _hoverTimer.Stop();
             _hoverTimer.Start();
        }

        private void HoverTimer_Tick(object sender, EventArgs e)
        {
            _hoverTimer.Stop();
            
            var pos = _textEditor.GetPositionFromPoint(_lastHoverPos);
            if (pos.HasValue)
            {
                var line = pos.Value.Line;
                var col = pos.Value.Column;
                var code = _textEditor.Text; 
                
                var service = App.Container.Resolve<IPythonComService>();
                Task.Run(async () => {
                    var json = await service.GetHover(code, line, col - 1, _savePath);
                    if (string.IsNullOrEmpty(json)) return;
                    
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                         try {
                             var stats = JArray.Parse(json);
                             if (stats.Count > 0)
                             {
                                 var item = stats[0];
                                 var name = item["full_name"]?.ToString() ?? item["name"]?.ToString();
                                 var type = item["type"]?.ToString();
                                 var doc = item["docstring"]?.ToString();
                                 var desc = item["description"]?.ToString();
                                 
                                 var sb = new StringBuilder();
                                 if (!string.IsNullOrEmpty(name)) sb.AppendLine(name);
                                 if (!string.IsNullOrEmpty(type)) sb.AppendLine($"Type: {type}");
                                 if (!string.IsNullOrEmpty(desc)) sb.AppendLine(desc);
                                 if (!string.IsNullOrEmpty(doc) && type != "keyword") {
                                     sb.AppendLine();
                                     sb.AppendLine(doc);
                                 }
                                 
                                 var txt = sb.ToString().Trim();
                                 if (!string.IsNullOrEmpty(txt))
                                 {
                                      ToolTip.SetTip(_textEditor, txt);
                                      ToolTip.SetIsOpen(_textEditor, true);
                                 }
                                 else
                                 {
                                     ToolTip.SetIsOpen(_textEditor, false);
                                 }
                             }
                             else
                             {
                                  ToolTip.SetIsOpen(_textEditor, false);
                             }
                         } catch 
                         {
                              ToolTip.SetIsOpen(_textEditor, false);
                         }
                    });
                });
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
             base.OnKeyDown(e);
             var keymap = Avalonia.Application.Current.PlatformSettings.HotkeyConfiguration;
             bool isCtrlSpace = (e.Key == Key.Space && (e.KeyModifiers & KeyModifiers.Control) != 0);
             
             if (isCtrlSpace)
             {
                 ShowCompletion(true);
                 e.Handled = true;
             }
             
             if (e.Key == Key.F12)
             {
                 GoToDefinition();
                 e.Handled = true;
             }
             
             if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
             {
                 FormatDocument();
                 e.Handled = true;
             }
        }

        private void GoToDefinition()
        {
            var service = App.Container.Resolve<IPythonComService>();
            var code = _textEditor.Text;
            var line = _textEditor.TextArea.Caret.Line;
            var col = _textEditor.TextArea.Caret.Column;
            
            Task.Run(async () =>
            {
               var json = await service.GetGoto(code, line, col - 1, _savePath);
               if (string.IsNullOrEmpty(json)) return;
               
               Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                   try {
                       var results = JArray.Parse(json);
                       if (results.Count > 0)
                       {
                           var match = results[0];
                           var path = match["module_path"]?.ToString();
                           var l = match["line"]?.ToObject<int>();
                           var c = match["column"]?.ToObject<int>();
                           
                           if (l.HasValue && c.HasValue) 
                           {
                               if (path != null && _savePath != null && path != _savePath)
                               {
                                   // Different file. TODO: Trigger open file event?
                               }
                               else
                               {
                                   _textEditor.TextArea.Caret.Line = l.Value;
                                   _textEditor.TextArea.Caret.Column = c.Value + 1;
                                   _textEditor.ScrollTo(l.Value, c.Value + 1);
                               }
                           }
                       }
                   } catch {}
               });
            });
        }

        private void FormatDocument()
        {
             var service = App.Container.Resolve<IPythonComService>();
             var code = _textEditor.Text;
             
             Task.Run(async () => {
                 var result = await service.FormatCode(code);
                 if (result != null)
                 {
                     Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                         try {
                             var obj = JObject.Parse(result);
                             if (obj["code"] != null)
                             {
                                 var newCode = obj["code"].ToString();
                                 if (newCode != _textEditor.Text)
                                 {
                                     _textEditor.Document.Text = newCode;
                                 }
                             }
                         } catch {}
                     });
                 }
             });
        }

        private void AddControlButton_Click(object sender, RoutedEventArgs e)
        {
            var button = new Button() { Content = "Click me", Cursor = Cursor.Default };

            button.VerticalAlignment = VerticalAlignment.Center;

            TextBlock.SetBaselineOffset(button, 22);

            _generator.controls.Add(new Pair(_textEditor.CaretOffset, button));
            _generator.controls.Sort(0, _generator.controls.Count, _generator);
            _textEditor.TextArea.TextView.Redraw();
        }

        private void ClearControlButton_Click(object sender, RoutedEventArgs e)
        {
            _generator.controls.Clear();
            _textEditor.TextArea.TextView.Redraw();
        }

        private void textEditor_TextArea_TextEntering(object sender, TextInputEventArgs e)
        {
            if (e.Text.Length > 0 && _completionWindow != null)
            {
                if (!char.IsLetterOrDigit(e.Text[0]))
                {
                    _completionWindow.CompletionList.RequestInsertion(e);
                }
            }


        }

        private void textEditor_TextArea_TextEntered(object sender, TextInputEventArgs e)
        {
            if (e.Text.Length > 0)
            {
                char c = e.Text[0];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '.')
                {
                    
                    
                    ShowCompletion(false);
                }
                else if (c == '(')
                {
                    ShowSignatures();
                }
            }
        }



        private void ShowCompletion(bool force)
        {
            
            try
            {
                var service = App.Container.Resolve<IPythonComService>();
                var code = _textEditor.Text;
                var line = _textEditor.TextArea.Caret.Line;
                var col = _textEditor.TextArea.Caret.Column; 
                
                Task.Run(async () =>
                {
                    var json = await service.GetCompletions(code, line, col - 1, _savePath);
                    if (string.IsNullOrEmpty(json)) return;

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        try 
                        {
                            var updates = JArray.Parse(json);
                            if (updates.Count > 0)
                            {
                                if (_completionWindow == null)
                                {
                                    _completionWindow = new CompletionWindow(_textEditor.TextArea);
                                    _completionWindow.Closed += (o, args) => _completionWindow = null;
                                }

                                int offset = _textEditor.CaretOffset;
                                int start = offset;
                                while (start > 0)
                                {
                                    char c = _textEditor.Document.GetCharAt(start - 1);
                                    if (!char.IsLetterOrDigit(c) && c != '_') break;
                                    start--;
                                }
                                
                    
                                _completionWindow.StartOffset = start;
                                _completionWindow.EndOffset = offset;


                                var data = _completionWindow.CompletionList.CompletionData;
                                data.Clear();
                                
                                var existing = new HashSet<string>();
                                foreach(var item in updates)
                                {
                                    var name = item["name"]?.ToString();
                                    var desc = item["description"]?.ToString() ?? "";
                                    var type = item["type"]?.ToString();
                                    var doc = item["docstring"]?.ToString() ?? "";
                                    
                                    if(!string.IsNullOrEmpty(doc) && type != "keyword") desc += "\n\n" + doc;

                                    if (name != null && !existing.Contains(name))
                                    {
                                        data.Add(new MyCompletionData(name, desc));
                                        existing.Add(name);
                                    }
                                }
                                
                                if (data.Count > 0)
                                {
                                    _completionWindow.Show();
                                }
                            }
                            else if (force)
                            {
                            }
                        }
                        catch { }
                    });
                });
            }
            catch { }
        }

        private void ShowSignatures()
        {
            
            try
            {
                var service = App.Container.Resolve<IPythonComService>();
                var code = _textEditor.Text;
                var line = _textEditor.TextArea.Caret.Line;
                var col = _textEditor.TextArea.Caret.Column; 
                
                Task.Run(async () =>
                {
                    var json = await service.GetSignatures(code, line, col - 1, _savePath);
                    if (string.IsNullOrEmpty(json)) return;

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        try 
                        {
                            var updates = JArray.Parse(json);
                            if (updates.Count > 0)
                            {
                                _insightWindow = new OverloadInsightWindow(_textEditor.TextArea);
                                _insightWindow.Closed += (o, args) => _insightWindow = null;

                                var items = new List<(string, string)>();
                                int selectedIndex = 0;

                                foreach(var item in updates)
                                {
                                    var name = item["name"]?.ToString() ?? "Unknown";
                                    var doc = item["docstring"]?.ToString() ?? "";
                                    var paramsList = item["params"] as JArray;
                                    var idx = item["index"]?.ToObject<int>() ?? 0;
                                    
                                    var pStrs = new List<string>();
                                    if(paramsList != null) {
                                        foreach(var p in paramsList) {
                                            pStrs.Add(p["name"]?.ToString() ?? "?");
                                        }
                                    }
                                    var header = $"{name}({string.Join(", ", pStrs)})";
                                    items.Add((header, doc));
                                }

                                _insightWindow.Provider = new MyOverloadProvider(items);
                                _insightWindow.Show();
                            }
                        }
                         catch { }
                    });
                });
            }
            catch { }
        }

        class UnderlineAndStrikeThroughTransformer : DocumentColorizingTransformer
        {
            protected override void ColorizeLine(DocumentLine line)
            {
                if (line.LineNumber == 2)
                {
                    string lineText = this.CurrentContext.Document.GetText(line);

                    int indexOfUnderline = lineText.IndexOf("underline");
                    int indexOfStrikeThrough = lineText.IndexOf("strikethrough");

                    if (indexOfUnderline != -1)
                    {
                        ChangeLinePart(
                            line.Offset + indexOfUnderline,
                            line.Offset + indexOfUnderline + "underline".Length,
                            visualLine =>
                            {
                                if (visualLine.TextRunProperties.TextDecorations != null)
                                {
                                    var textDecorations = new TextDecorationCollection(visualLine.TextRunProperties.TextDecorations) { TextDecorations.Underline[0] };

                                    visualLine.TextRunProperties.SetTextDecorations(textDecorations);
                                }
                                else
                                {
                                    visualLine.TextRunProperties.SetTextDecorations(TextDecorations.Underline);
                                }
                            }
                        );
                    }

                    if (indexOfStrikeThrough != -1)
                    {
                        ChangeLinePart(
                            line.Offset + indexOfStrikeThrough,
                            line.Offset + indexOfStrikeThrough + "strikethrough".Length,
                            visualLine =>
                            {
                                if (visualLine.TextRunProperties.TextDecorations != null)
                                {
                                    var textDecorations = new TextDecorationCollection(visualLine.TextRunProperties.TextDecorations) { TextDecorations.Strikethrough[0] };

                                    visualLine.TextRunProperties.SetTextDecorations(textDecorations);
                                }
                                else
                                {
                                    visualLine.TextRunProperties.SetTextDecorations(TextDecorations.Strikethrough);
                                }
                            }
                        );
                    }
                }
            }
        }

        private class MyOverloadProvider : IOverloadProvider
        {
            private readonly IList<(string header, string content)> _items;
            private int _selectedIndex;

            public MyOverloadProvider(IList<(string header, string content)> items)
            {
                _items = items;
                SelectedIndex = 0;
            }

            public int SelectedIndex
            {
                get => _selectedIndex;
                set
                {
                    _selectedIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CurrentHeader));
                    OnPropertyChanged(nameof(CurrentContent));

                }
            }

            public int Count => _items.Count;
            public string CurrentIndexText => $"{SelectedIndex + 1} of {Count}";
            public object CurrentHeader => _items[SelectedIndex].header;
            public object CurrentContent => _items[SelectedIndex].content;

            public event PropertyChangedEventHandler PropertyChanged;

            private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public class MyCompletionData : ICompletionData
        {
            private string _description;
            public MyCompletionData(string text, string description = "")
            {
                Text = text;
                _description = description;
            }

            public IImage Image => null;

            public string Text { get; }

            public object Content => _contentControl ??= BuildContentControl();

            public object Description => _description;

            public double Priority { get; } = 0;

            public void Complete(TextArea textArea, ISegment completionSegment,
                EventArgs insertionRequestEventArgs)
            {
                textArea.Document.Replace(completionSegment, Text);
            }

            Control BuildContentControl()
            {
                TextBlock textBlock = new TextBlock();
                textBlock.Text = Text;
                textBlock.Margin = new Thickness(5);

                return textBlock;
            }

            Control _contentControl;
        }

        class ElementGenerator : VisualLineElementGenerator, IComparer<Pair>
        {
            public List<Pair> controls = new List<Pair>();

            public override int GetFirstInterestedOffset(int startOffset)
            {
                int pos = controls.BinarySearch(new Pair(startOffset, null), this);
                if (pos < 0)
                    pos = ~pos;
                if (pos < controls.Count)
                    return controls[pos].Key;
                else
                    return -1;
            }

            public override VisualLineElement ConstructElement(int offset)
            {
                int pos = controls.BinarySearch(new Pair(offset, null), this);
                if (pos >= 0)
                    return new InlineObjectElement(0, controls[pos].Value);
                else
                    return null;
            }

            int IComparer<Pair>.Compare(Pair x, Pair y)
            {
                return x.Key.CompareTo(y.Key);
            }
        }

        private void InsertSnippetButton_Click(object sender, RoutedEventArgs e)
        {
            var className = new SnippetReplaceableTextElement { Text = "Name" };
            var snippet = new Snippet
            {
                Elements =
                {
                    new SnippetTextElement { Text = "public class " },
                    className,
                    new SnippetTextElement
                    {
                        Text = "\n{\n    public "
                    },
                    new SnippetBoundElement { TargetElement = className },
                    new SnippetTextElement { Text = "()\n    {\n        " },
                    new SnippetCaretElement(),
                    new SnippetTextElement { Text = "\n    }\n}" }
                }
            };

            snippet.Insert(_textEditor.TextArea);
            _textEditor.Focus();
        }

       
    }
    public interface ITextMarkerService : IBackgroundRenderer, IVisualLineTransformer
    {
        ITextMarker Create(int offset, int length);
        void Remove(ITextMarker marker);
        void RemoveAll(Predicate<ITextMarker> predicate);
        IEnumerable<ITextMarker> TextMarkers { get; }
    }

    public interface ITextMarker
    {
        int StartOffset { get; }
        int EndOffset { get; }
        int Length { get; }
        Color? BackgroundColor { get; set; }
        Color? MarkerColor { get; set; }
        TextMarkerType MarkerType { get; set; }
        object ToolTip { get; set; }
    }

    public enum TextMarkerType
    {
        None,
        SquigglyUnderline,
        NormalUnderline,
        DottedUnderline,
        Block
    }

    public class EditorTextMarkerService : DocumentColorizingTransformer, ITextMarkerService
    {
        private readonly TextDocument _document;
        private readonly TextSegmentCollection<TextMarker> _markers;

        public EditorTextMarkerService(TextDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _markers = new TextSegmentCollection<TextMarker>(document);
            document.Changed += OnDocumentChanged; 
        }

        private void OnDocumentChanged(object sender, DocumentChangeEventArgs e)
        {
        }

        public ITextMarker Create(int offset, int length)
        {
            var m = new TextMarker(this, offset, length);
            _markers.Add(m);
            return m;
        }

        public void Remove(ITextMarker marker)
        {
            if (marker is TextMarker m && _markers.Remove(m))
            {
                Redraw(m);
            }
        }

        public void RemoveAll(Predicate<ITextMarker> predicate)
        {
            var toRemove = _markers.Where(m => predicate(m)).ToList();
            foreach (var m in toRemove)
            {
                Remove(m);
            }
        }

        public IEnumerable<ITextMarker> TextMarkers => _markers;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (textView == null) throw new ArgumentNullException(nameof(textView));
            if (drawingContext == null) throw new ArgumentNullException(nameof(drawingContext));

            if (_markers == null || !textView.VisualLinesValid) return;

            var visualLines = textView.VisualLines;
            if (visualLines.Count == 0) return;

            int viewStart = visualLines.First().FirstDocumentLine.Offset;
            int viewEnd = visualLines.Last().LastDocumentLine.EndOffset;

            foreach (var marker in _markers.FindOverlappingSegments(viewStart, viewEnd - viewStart))
            {
                if (marker.MarkerColor != null) 
                {
                    if (marker.MarkerType == TextMarkerType.Block)
                    {
                    }
                    
                    if (marker.MarkerType != TextMarkerType.None && marker.MarkerType != TextMarkerType.Block)  
                    {
                         foreach (var r in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker))
                         {
                             var startPoint = r.BottomLeft;
                             var endPoint = r.BottomRight;

                             var pen = new Pen(new SolidColorBrush(marker.MarkerColor.Value));
                             
                             if (marker.MarkerType == TextMarkerType.SquigglyUnderline)
                             {
                                 double offset = 2.5;
                                 int count = (int)(r.Width / offset) / 2;
                                 var g = new StreamGeometry();
                                 using (var ctx = g.Open())
                                 {
                                     ctx.BeginFigure(new Point(startPoint.X, startPoint.Y - 2), false);
                                     for (int i = 0; i < count; i++)
                                     {
                                         double x = startPoint.X + (i * 2 * offset);
                                         ctx.LineTo(new Point(x + offset, startPoint.Y + 2));
                                         ctx.LineTo(new Point(x + 2 * offset, startPoint.Y - 2));
                                     }
                                 }
                                 drawingContext.DrawGeometry(null, pen, g);
                             }
                             else 
                             {
                                 drawingContext.DrawLine(pen, new Point(startPoint.X, startPoint.Y), new Point(endPoint.X, endPoint.Y));
                             }
                         }
                    }
                }
            }
        }

        public KnownLayer Layer => KnownLayer.Selection; 

        protected override void ColorizeLine(DocumentLine line)
        {
        }

        private void Redraw(ISegment segment)
        {
        }
    }

    public class TextMarker : TextSegment, ITextMarker
    {
        private readonly EditorTextMarkerService _service;
        public TextMarker(EditorTextMarkerService service, int offset, int length)
        {
            _service = service;
            StartOffset = offset;
            Length = length;
        }
        
        public Color? BackgroundColor { get; set; }
        public Color? MarkerColor { get; set; }
        public TextMarkerType MarkerType { get; set; }
        public object ToolTip { get; set; }
    }

    public partial class EditorView
    {
        private async void GoToDefinition_Click(object sender, RoutedEventArgs e)
        {
             try
             {
                 var service = App.Container.Resolve<IPythonComService>();
                 var code = _textEditor.Text;
                 var line = _textEditor.TextArea.Caret.Line;
                 var col = _textEditor.TextArea.Caret.Column;

                 var json = await service.GetGoto(code, line, col - 1, _savePath);
                 if (string.IsNullOrEmpty(json)) return;

                 var defs = JArray.Parse(json);
                 if (defs.Count > 0)
                 {
                     var def = defs[0];
                     var dLine = def["line"]?.ToObject<int>() ?? 0;
                     var dCol = def["column"]?.ToObject<int>() ?? 0;
                     
                     if (dLine > 0)
                     {
                         _textEditor.TextArea.Caret.Line = dLine;
                         _textEditor.TextArea.Caret.Column = dCol + 1;
                         _textEditor.ScrollTo(dLine, dCol);
                         _textEditor.Focus();
                     }
                 }
             }
             catch { }
        }

        private async void FormatDocument_Click(object sender, RoutedEventArgs e)
        {
             try
             {
                 var service = App.Container.Resolve<IPythonComService>();
                 var code = _textEditor.Text;

                 var jsonStr = await service.FormatCode(code);
                 if (string.IsNullOrEmpty(jsonStr)) return;

                 var json = JObject.Parse(jsonStr);
                 var formatted = json["code"]?.ToString();
                 
                 if (!string.IsNullOrEmpty(formatted) && formatted != code)
                 {
                     _textEditor.Document.Text = formatted;
                 }
             }
             catch { }
        }



    }




}
