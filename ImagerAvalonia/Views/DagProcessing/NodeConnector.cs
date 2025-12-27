using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;

using Avalonia.Threading;

using Avalonia.Controls.Shapes;
using Avalonia.VisualTree;

namespace ImagerAvalonia.Views
{
    public class NodeConnector : Path, IDisposable
    {

        public Point PointStartC;
        public Point Point1C;
        public Point Point2C;
        public Point Point3C;
        private bool _disposed = false;


        public void UpdateStart(Point point)
        {
            PointStartC = point;
            Point1C = new Point(point.X + 100, point.Y);
            Dispatcher.UIThread.Invoke(UpdateBezierPath);
        }

        public void UpdateConnectorOnMove(object? sender, PointerEventArgs e)
        {
            Point endpoint = e.GetPosition(this.GetVisualParent());
            UpdateEnd(endpoint);
        }


        public void UpdateEnd(Point point)
        {
            Point2C = new Point(point.X - 100, point.Y);
            Point3C = new Point(point.X, point.Y);
            Dispatcher.UIThread.Invoke(UpdateBezierPath);
        }

        public Visual startVisual;
        public Visual endVisual { get; private set; }
        public Control startControl { get; private set; }
        public UserControl endControl { get; private set; }

        public void SetEndPoint(Visual end_visual)
        {
            endVisual = end_visual;
        }

        public void SetEndControl(UserControl end_control)
        {
            endControl = end_control;
        }

        public NodeConnector()
        {
            //InitializeComponent();
            Point1C = new Point(50, 0);
            Point2C = new Point(50, 100);
            Point3C = new Point(100, 100);
        }
        public NodeConnector(Point start, Visual start_visual, Control start_control) : base()
        {
            //InitializeComponent();
            startVisual = start_visual;
            startControl = start_control;
            PointStartC = start;
            Point1C = new Point(start.X + 100, start.Y);
            Point2C = new Point(start.X, start.Y);
            Point3C = new Point(start.X, start.Y);
            ZIndex = 0;
            //Canvas.SetLeft(this,120);
            //Canvas.SetTop(this, 250);

            Stroke = Avalonia.Media.Brushes.Black;
            StrokeThickness = 4;


            //ConnectorPath = this.Find<Path>("PathFig");
            //ConnectorPath.ZIndex = 4;


            //this.PointerPressed += OnPointerPressed;
        }


        private void UpdateBezierPath()
        {
            var geometry = new PathGeometry
            {
                Figures = new PathFigures
                {
                    new PathFigure
                    {
                        IsClosed = false,
                        StartPoint = PointStartC,
                        Segments = new PathSegments
                        {
                            new BezierSegment
                            {
                                Point1 = Point1C,
                                Point2 = Point2C,
                                Point3 = Point3C
                            }
                        }
                    }
                }
            };

            this.Data = geometry;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (endControl is DagNodeImageInputView endNode && startControl is DagNodeImageOutputView startNode)
                {
                    startNode.RemoveInput(endNode);
                    endNode.RemoveOutput(startNode);
                }

            }



            _disposed = true;
        }




    }

}
