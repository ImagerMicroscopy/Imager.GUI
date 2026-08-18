using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Skia;
using ImagerAvalonia.Services.MeasurementControl;
using ImagerAvalonia.ViewModels;
using ScottPlot;
using ScottPlot.Plottables;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ImagerAvalonia.Views.ImageViews
{



    public class CircleImageElement : IImageElement
    {
        public double X { get; }
        public double Y { get; }
        public double CanvasX { get; }
        public double CanvasY { get; }
        public Avalonia.Media.Color Color { get; }
        public string RegionParameterName { get; }

        private double Radius { get; }

        public CircleImageElement(double x, double y, double radius, Avalonia.Media.Color color, string name, double canvasX, double canvasY)
        {
            X = x;
            Y = y;
            Radius = radius;
            Color = color;
            RegionParameterName = name;
            CanvasX = canvasX;
            CanvasY = canvasY;
        }



        public async Task<(uint val1, uint val2, double val3)> ComputeValue(
               byte[] imageData, uint width, uint height)
        {

            return await Task.Run(() =>
            {
                unsafe
                {
                    int min_val = Math.Min((int)width, (int)height);
                    int max_val = Math.Max((int)width, (int)height);

                    int xp = 0;
                    int yp = 0;

                    if(height>width)
                    {
                        xp = Convert.ToInt32(X * max_val -(max_val/2 - min_val/2));
                        yp = Convert.ToInt32(Y * max_val);
                    }
                    if(width>height)
                    {

                        xp = Convert.ToInt32(X * max_val );
                        yp = Convert.ToInt32(Y * max_val - (max_val / 2 - min_val / 2));
                    }
                    if(width==height)
                    {
                        xp = Convert.ToInt32(X * max_val);
                        yp = Convert.ToInt32(Y * max_val);
                    }

                    //int xp = Convert.ToInt32(X * width);
                    //int yp = Convert.ToInt32(Y * height);


                    if (imageData.Length != width * height * 2)
                        throw new ArgumentException("Invalid image data size.");

                    fixed (byte* ptr = imageData)
                    {
                        ushort* image = (ushort*)ptr;

                        int x0 = Convert.ToInt32(Math.Max(0, xp - Radius));
                        int x1 = Convert.ToInt32(Math.Min(width - 1, xp + Radius));
                        int y0 = Convert.ToInt32(Math.Max(0, yp - Radius));
                        int y1 = Convert.ToInt32(Math.Min(height - 1, yp + Radius));

                        int x = Convert.ToInt32(xp);
                        int y = Convert.ToInt32(yp);

                        ulong sum = 0;
                        int count = 0;
                        ushort min = ushort.MaxValue;
                        ushort max = ushort.MinValue;

                        for (int j = y0; j <= y1; j++)
                        {
                            for (int i = x0; i <= x1; i++)
                            {
                                int dx = i - x;
                                int dy = j - y;
                                if (dx * dx + dy * dy <= Radius * Radius)
                                {
                                    ushort val = image[j * width + i];
                                    sum += val;
                                    count++;

                                    if (val < min) min = val;
                                    if (val > max) max = val;
                                }
                            }
                        }

                        double mean = count > 0 ? (double)sum / count : 0;
                        //this.min = min;
                        //this.max = max;
                        //this.mean = mean;
                        return (min, max, mean);
                    }
                }
            });


        }

        public Control GenerateRegionControl()
        {
            var ellipse = new Avalonia.Controls.Shapes.Ellipse
            {
                Width = Radius,
                Height = Radius,
                Stroke = new SolidColorBrush(Color),
                StrokeThickness = 2,
                ZIndex = 2,
                Tag = RegionParameterName

            };
            Canvas.SetLeft(ellipse, CanvasX);
            Canvas.SetTop(ellipse, CanvasY);
            return ellipse;
        }
        public List<IPlottable> RetrievePlotControls()
        {
            var MinPlot = new DataLogger();
            MinPlot.Color = ScottPlot.Color.FromSKColor(Color.ToSKColor());

            var MaxPlot = new DataLogger();
            MaxPlot.Color = ScottPlot.Color.FromSKColor(Color.ToSKColor());

            var MeanPlot = new DataLogger();
            MeanPlot.Color = ScottPlot.Color.FromSKColor(Color.ToSKColor());
            return new List<IPlottable>() { MinPlot, MaxPlot, MeanPlot };
        }



        public ElementPlotViewModel GenerateRegionPlotControl(List<IImageElement> image_elements, string acq, string det, string reg, 
            ObservableCollection<XYStagePosition> stagePositions)
        {
                var circle_vm = new CircleElementViewModel(image_elements, acq, det, reg, stagePositions);
                return circle_vm;
        }
    }

    public partial class CircleElementControl : ImageElementControlBase
    {
        private const double DotIncrement = 4;
        private const double MaxDotSize = 256;
        private const double MinDotSize = 4;
        private const double NormalizationFactor = 800;

        private readonly Random _random = new();

        public override List<Control> AddedRegions { get; protected set; } = new();
        public override Control AddedVisualType { get; protected set; }
        public override string RegionName { get; protected set; } = string.Empty;

        public SolidColorBrush CurrentDotColor { get; }

        public CircleElementControl()
        {
            AddedVisualType = CreateVisualDot();
            CurrentDotColor = GenerateRandomBrush();
        }

        private static Avalonia.Controls.Shapes.Ellipse CreateVisualDot()
        {
            var ellipse = new Avalonia.Controls.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(Avalonia.Media.Colors.Red),
                Name = "SelectionVisual",
                ZIndex = 2
            };

            return ellipse;
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

            var position = e.GetPosition(canvas);
            Canvas.SetLeft(AddedVisualType, position.X - AddedVisualType.Width / 2);
            Canvas.SetTop(AddedVisualType, position.Y - AddedVisualType.Height / 2);
            canvas.Focus();
        }

        public override void ElementKeyDown(object? sender, KeyEventArgs e)
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;

            switch (e.Key)
            {
                case Key.OemPlus:
                    ResizeDot(DotIncrement);
                    break;
                case Key.OemMinus:
                    ResizeDot(-DotIncrement);
                    break;
            }
        }

        private void ResizeDot(double delta)
        {
            double newSize = AddedVisualType.Width + delta;
            if (newSize is >= MinDotSize and <= MaxDotSize)
            {
                AddedVisualType.Width = newSize;
                AddedVisualType.Height = newSize;
            }
        }

        public override void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Canvas canvas) return;

            var position = e.GetPosition(canvas);

            var newDot = new Avalonia.Controls.Shapes.Ellipse
            {
                Width = AddedVisualType.Width,
                Height = AddedVisualType.Height,
                Stroke = GenerateRandomBrush(),
                StrokeThickness = 2
            };

            Canvas.SetLeft(newDot, position.X - newDot.Width / 2);
            Canvas.SetTop(newDot, position.Y - newDot.Height / 2);
            ZIndex = 2;

            canvas.Children.Add(newDot);
            AddedRegions.Add(newDot);
        }

        public override List<IImageElement> RetrieveRegionParameters()
        {
            return AddedRegions.Select(region =>
            {

                var el_reg = region as Avalonia.Controls.Shapes.Ellipse;
           
                var region_color = el_reg.Stroke as SolidColorBrush;
                double canvasX = Canvas.GetLeft(region);
                double canvasY = Canvas.GetTop(region);

                if (double.IsNaN(canvasX)) canvasX = 0;
                if (double.IsNaN(canvasY)) canvasY = 0;

                return (IImageElement)new CircleImageElement(
                    x: canvasX / NormalizationFactor,
                    y: canvasY / NormalizationFactor,
                    radius: region.Width,
                    color: region_color.Color,
                    name: RegionName,
                    canvasX: canvasX,
                    canvasY: canvasY
                );
            }).ToList();
        }

        internal override void SetRegionName(string name) => RegionName = name;
    }
}
