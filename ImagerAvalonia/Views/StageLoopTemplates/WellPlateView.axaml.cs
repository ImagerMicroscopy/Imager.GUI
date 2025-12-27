using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Autofac;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;


namespace ImagerAvalonia.Views;

public partial class WellPlateView: UserControl
{

    private List<string> _selectedWellNames = new();
    private List<int> _xWellPosition = new();
    private List<int> _yWellPosition = new();

    private bool _isWellSelectionOngoing = false;
    private readonly IStageControl _stageControl;
    private string firstwell_letter = String.Empty;
    private string firstwell_number = String.Empty;
    private string lastwell_letter = String.Empty;
    private string lastwell_number = String.Empty;

    private List<string> _wellLetters = new() {"A","B","C","D","E","F","G","H" };
    private List<string> _wellNumbers = Enumerable.Range(1, 12).Select(x => x.ToString()).ToList();

    private Canvas _well_canvas = new();
    public WellPlateView()
    {
        _stageControl = App.Container.Resolve<IStageControl>();
        InitializeComponent();
        _well_canvas = this.FindControl<Canvas>("WellPlate")!;
        int offset_y = 35;
        int offset_x = 42;
        foreach (var wellLetter in _wellLetters)
        {
            offset_y += 22;
            foreach(var wellnumber in _wellNumbers)
            {
                var well = new Ellipse() { Name=$"{wellLetter}{wellnumber}", Width = 22, Height = 22, StrokeThickness=1, IsHitTestVisible=true, Fill=Brushes.Gray, Stroke= Brushes.AliceBlue };
                well.PointerPressed += Well_PointerPressed;
                well.PointerEntered += Well_PointerEntered;
                _well_canvas.Children.Add(well);
                Canvas.SetTop(well, offset_y);
                Canvas.SetLeft(well, offset_x);
                offset_x += 24;
            }
            offset_x = 42;
        }
    }

  

    private void Well_PointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (_isWellSelectionOngoing)
        {
            if (sender is Ellipse well && well.Name!=null)
            {
                lastwell_letter = well.Name.First().ToString();
                lastwell_number = Regex.Replace(well.Name.ToString(), @"\D", "");


                int firstwell_letter_index = _wellLetters.IndexOf(firstwell_letter.ToString());
                int firstwell_number_index = _wellNumbers.IndexOf(firstwell_number.ToString());

                int lastwell_letter_index = _wellLetters.IndexOf(lastwell_letter);
                int lastwell_number_index = _wellNumbers.IndexOf(lastwell_number);

                int min_well_letter = Math.Min(firstwell_letter_index, lastwell_letter_index);
                int min_well_number = Math.Min(firstwell_number_index, lastwell_number_index);

                int max_well_letter = Math.Max(firstwell_letter_index, lastwell_letter_index);
                int max_well_number = Math.Max(firstwell_number_index, lastwell_number_index);

                for (int i = 0; i < 8 ; i++)
                {
                    for (int j = 0; j < 12; j++)
                    {

                        if ((i >= min_well_letter && i <= max_well_letter) && (j >= min_well_number && j <= max_well_number))
                        {
                            var el = _well_canvas.Children.First(x => x.Name == $"{_wellLetters[i]}{_wellNumbers[j]}");
                            if (el is Ellipse w)
                            {
                                w.Fill = Brushes.Red;
                            } 
                        }
                        else 
                        {
                            var el = _well_canvas.Children.First(x => x.Name == $"{_wellLetters[i]}{_wellNumbers[j]}");
                            if (el is Ellipse w)
                            {
                                w.Fill = Brushes.Gray;
                            }
                        }
                    }
                }
            }
        }
    }

    private async void OpenDialogButton_Click(object? sender, RoutedEventArgs e)
    {
        var a1dialog = new GoToWellPositionDialog("Go to position A1 on the well plate");
        var b3dialog = new GoToWellPositionDialog("Go to position B3 on the well plate");
        var parentWindow = this.VisualRoot as Window;

        if (parentWindow != null)
        {
            await a1dialog.ShowDialog(parentWindow);

            var A1Position = _stageControl.ReadStagePosition();

            await b3dialog.ShowDialog(parentWindow);

            var B3Position = _stageControl.ReadStagePosition();

            if (A1Position != null && B3Position != null)
            {

                double dirx = Math.Sign(B3Position.XPos - A1Position.XPos);
                double diry = Math.Sign(B3Position.YPos - A1Position.YPos);

                if (DataContext is WellPlateViewModel wellplatevm)
                {

                    var vm = wellplatevm.StageLoopViewModel;
                    if (Math.Abs(B3Position.XPos - A1Position.XPos) < Math.Abs(B3Position.YPos - A1Position.YPos))
                    {
                        for (int i = 0; i < _selectedWellNames.Count; i++)
                        {
                            vm.AppendStagePosition(A1Position.XPos + _xWellPosition[i] * 9000, A1Position.YPos + _yWellPosition[i] * 9000, A1Position.ZPos, A1Position.IsPFSEnabled, A1Position.PFSOffset, _selectedWellNames[i]);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < _selectedWellNames.Count; i++)
                        {
                            vm.AppendStagePosition(A1Position.XPos + _yWellPosition[i] * 9000, A1Position.YPos + _xWellPosition[i] * 9000, A1Position.ZPos, A1Position.IsPFSEnabled, A1Position.PFSOffset, _selectedWellNames[i]);
                        }
                    }
                }
            }
        }

    }


    private void Well_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        _isWellSelectionOngoing = !_isWellSelectionOngoing;
        if (_isWellSelectionOngoing)
        {
            _xWellPosition.Clear();
            _yWellPosition.Clear();
            _selectedWellNames.Clear();
            foreach (var wellletter in _wellLetters)
            {
                foreach (var wellnumber in _wellNumbers)
                {
                    var el = _well_canvas.Children.First(x => x.Name == $"{wellletter}{wellnumber}");
                    if (el is Ellipse w)
                    {
                        w.Fill = Brushes.Gray;
                    }
                }
            }
            if (sender is Ellipse well && well.Name!=null)
            {
                firstwell_letter = well.Name.First().ToString();
                firstwell_number = Regex.Replace(well.Name.ToString(), @"\D", "");

                return;
                
            }
        }


      


        int firstwell_letter_index = _wellLetters.IndexOf(firstwell_letter.ToString());
        int firstwell_number_index = _wellNumbers.IndexOf(firstwell_number.ToString());

        int lastwell_letter_index = _wellLetters.IndexOf(lastwell_letter.ToString());
        int lastwell_number_index = _wellNumbers.IndexOf(lastwell_number.ToString());

        int min_well_letter = Math.Min(firstwell_letter_index, lastwell_letter_index);
        int min_well_number = Math.Min(firstwell_number_index, lastwell_number_index);

        int max_well_letter = Math.Max(firstwell_letter_index, lastwell_letter_index);
        int max_well_number = Math.Max(firstwell_number_index, lastwell_number_index);

        for (int i = min_well_letter; i <= max_well_letter; i++)
        {
            for (int j = min_well_number; j <= max_well_number; j++)
            {
                _xWellPosition.Add(i);
                _yWellPosition.Add(j);
                _selectedWellNames.Add($"{_wellLetters[i]}{_wellNumbers[j]}");

                var el = _well_canvas.Children.First(x => x.Name == $"{_wellLetters[i]}{_wellNumbers[j]}");
                if (el is Ellipse w)
                {
                    w.Fill = Brushes.Red;
                }

            }
        }


    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

    }



}

public class GoToWellPositionDialog : Window
{
    public GoToWellPositionDialog(string wheretogo)
    {
        Title = wheretogo;
        Width = 300;
        Height = 200;
        Background = new SolidColorBrush(Color.Parse("#454545")); 

        var submitButton = new Button
        {
            Content = "Submit",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 20, 0, 0)
        };

        submitButton.Click += (_, _) =>
        {
            Close(); // You can also pass a result: Close(true);
        };

        Content = new StackPanel
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = wheretogo,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                },
                submitButton
            }
        };
    }
}
