using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Skia;
using Avalonia.Threading;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;
using ScottPlot;
using ScottPlot.DataSources;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace ImagerAvalonia.Views.ImageViews
{



    public class LineImageElement : IImageElement
    {
        private double _lineWidth = 0;
        private Avalonia.Point _startPoint = new Avalonia.Point();
        private Avalonia.Point _endPoint = new Avalonia.Point();




        public Avalonia.Media.Color Color { get; }
        public string RegionParameterName { get; }

        private double Radius { get; }

        public LineImageElement(Avalonia.Point startPoint, Avalonia.Point endPoint, double width, Avalonia.Media.Color color, string name)
        {
            _startPoint = startPoint;
            _endPoint = endPoint;
            _lineWidth = width;

            Color = color;
            RegionParameterName = name;
        }



        public async Task<List<double>> ComputeValue(
               byte[] imageData, uint width, uint height)
        {

            return await Task.Run(() =>
            {
                var profile = new List<double>();

                var endPointX = _endPoint.X /800 * width;
                var endPointY = _endPoint.Y /800 * height;
                var startPointX = _startPoint.X / 800 * width;
                var startPointY = _startPoint.Y / 800 * height;


                double dx = endPointX - startPointX;
                double dy = endPointY - startPointY;
                double length = Math.Sqrt(dx * dx + dy * dy);

                int nSamples = (int)Math.Ceiling(length);
                double stepX = dx / nSamples;
                double stepY = dy / nSamples;

                double nx = -dy / length;
                double ny = dx / length;

                for (int i = 0; i <= nSamples; i++)
                {
                    double cx = startPointX + stepX * i;
                    double cy = startPointY + stepY * i;

                    double sum = 0;
                    int count = 0;

                    int halfWidth = (int)Math.Floor((_lineWidth - 1) / 2.0);

                    for (int w = -halfWidth; w <= halfWidth; w++)
                    {
                        double px = cx + nx * w;
                        double py = cy + ny * w;

                        double val = SampleBilinear(imageData, width, height, px, py);
                        sum += val;
                        count++;
                    }

                    profile.Add(sum / count);
                }

                return profile;
            }
            );


        }



        private double SampleBilinear(byte[] imageData, uint width, uint height, double x, double y)
        {
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = x0 + 1;
            int y1 = y0 + 1;

            if (x0 < 0 || y0 < 0 || x1 >= width || y1 >= height)
                return 0; // outside image

            double dx = x - x0;
            double dy = y - y0;

            double v00 = GetPixel16(imageData, width, x0, y0);
            double v10 = GetPixel16(imageData, width, x1, y0);
            double v01 = GetPixel16(imageData, width, x0, y1);
            double v11 = GetPixel16(imageData, width, x1, y1);

            double v0 = v00 * (1 - dx) + v10 * dx;
            double v1 = v01 * (1 - dx) + v11 * dx;

            return v0 * (1 - dy) + v1 * dy;
        }

        private ushort GetPixel16(byte[] imageData, uint width, int x, int y)
        {
            int idx = (y * (int)width + x) * 2;
            return (ushort)(imageData[idx] | (imageData[idx + 1] << 8)); // little-endian
        }

        public Control GenerateRegionControl()
        {
            var ellipse = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = _startPoint,
                EndPoint = _endPoint,
                Stroke = new SolidColorBrush(Color),
                StrokeThickness = _lineWidth,
                ZIndex = 2,
                Tag = RegionParameterName

            };

            return ellipse;
        }
        public List<IPlottable> RetrievePlotControls()
        {
            double dx = _endPoint.X - _startPoint.X;
            double dy = _endPoint.Y - _startPoint.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            var profile = new List<double>();

            int nSamples = (int)Math.Ceiling(length);
            for (int i = 0; i <= nSamples; i++)
            {
                profile.Add(0);
            }

            var LinePlot = new Signal(new SignalSourceDouble(profile,1)); 
            LinePlot.Color = ScottPlot.Color.FromSKColor(Color.ToSKColor());



            return new List<IPlottable>() { LinePlot };
        }



        public ElementPlotViewModel GenerateRegionPlotControl(List<IImageElement> image_elements, string acq, string det, string reg, 
            ObservableCollection<XYStagePosition> stagePositions)
        {
                var circle_vm = new LineElementViewModel(image_elements, acq, det, reg, stagePositions);
                return circle_vm;
        }
    }

    public partial class LineElementControl : ImageElementControlBase
    {
        private const double DotIncrement = 1;
        private const double MaxDotSize = 10;
        private const double MinDotSize = 1;
        private const double NormalizationFactor = 800;



        private bool IsAdditionInProgress = false;
        private Avalonia.Controls.Shapes.Line AddedLine;

        private readonly Random _random = new();

        public override List<Control> AddedRegions { get; protected set; } = new();
        public override Control AddedVisualType { get; protected set; }
        public override string RegionName { get; protected set; } = string.Empty;

        public SolidColorBrush CurrentDotColor { get; }

        public LineElementControl()
        {
            AddedVisualType = CreateVisualDot();
            CurrentDotColor = GenerateRandomBrush();
        }

        private  Avalonia.Controls.Shapes.Line CreateVisualDot()
        {
            var line = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Avalonia.Point(0, 0),
                EndPoint = new Avalonia.Point(0, 0),
                Width = 2,
                Fill = new SolidColorBrush(Avalonia.Media.Colors.Red),
                Name = "SelectionVisual",
                ZIndex = 2
            };

            return line;
        }

        private SolidColorBrush GenerateRandomBrush() =>
            new(Avalonia.Media.Color.FromRgb(
                (byte)_random.Next(256),
                (byte)_random.Next(256),
                (byte)_random.Next(256))
            );

        public override void OnDotPointerMoved(object? sender, PointerEventArgs e)
        {
            if (sender is not Canvas canvas) return;

            if(IsAdditionInProgress)
            {
                var position = e.GetPosition(canvas);
                AddedLine.EndPoint = position;
            }
            canvas.Focus();
        }

        public override void ElementKeyDown(object? sender, KeyEventArgs e)
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;

            switch (e.Key)
            {
                case Key.OemPlus:
                    ResizeLine(DotIncrement);
                    break;
                case Key.OemMinus:
                    ResizeLine(-DotIncrement);
                    break;
            }
        }

        private void ResizeLine(double delta)
        {
            if (AddedLine is not null)
            {
                double newSize = AddedLine.StrokeThickness + delta;
                if (newSize is >= MinDotSize and <= MaxDotSize)
                {
                    AddedLine.StrokeThickness = newSize;
                }
            }
            
        }

        public override void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Canvas canvas) return;
            IsAdditionInProgress = !IsAdditionInProgress;

            if (IsAdditionInProgress)
            {
                var position = e.GetPosition(canvas);

                AddedLine = new Avalonia.Controls.Shapes.Line
                {
                    Width = AddedVisualType.Width,
                    StartPoint = position,
                    EndPoint = position,
                    Stroke = GenerateRandomBrush(),
                    StrokeThickness = 1
                };
                ZIndex = 2;

                canvas.Children.Add(AddedLine);
                AddedRegions.Add(AddedLine);
            }
        }

        public override List<IImageElement> RetrieveRegionParameters()
        {


            return AddedRegions.Select(region =>
            {
                var line_region = (Avalonia.Controls.Shapes.Line)region;

                if (line_region.Stroke is SolidColorBrush region_color)
                {
                    return (IImageElement)new LineImageElement(
                        line_region.StartPoint, line_region.EndPoint, line_region.StrokeThickness, region_color.Color, RegionName);
                }
                else
                {
                    throw new Exception("Could not convert stroke to SolidColorBrush");
                }
            }).ToList();
        }

        internal override void SetRegionName(string name) => RegionName = name;
    }
}
