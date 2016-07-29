using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Warp.Tools;

namespace Warp.Controls
{
    /// <summary>
    /// Interaction logic for MicrographDisplay.xaml
    /// </summary>
    public partial class MicrographDisplay : UserControl
    {
        public Movie Movie
        {
            get { return (Movie)GetValue(MovieProperty); }
            set { SetValue(MovieProperty, value); }
        }
        public static readonly DependencyProperty MovieProperty = DependencyProperty.Register("Movie", typeof(Movie), typeof(MicrographDisplay), new PropertyMetadata(null, (s, e) =>
        {
            if (e.OldValue != null)
                ((Movie)e.OldValue).PropertyChanged -= ((MicrographDisplay)s).Movie_PropertyChanged;
            if (e.NewValue != null)
                ((Movie)e.NewValue).PropertyChanged += ((MicrographDisplay)s).Movie_PropertyChanged;

            ((MicrographDisplay)s).UpdateDisplay();
        }));

        private int Downsample = 0;
        private double ScaleFactor => 1.0 / Math.Pow(2.0, Downsample);

        public MicrographDisplay()
        {
            InitializeComponent();

            ImageDisplay.PreviewMouseWheel += ImageDisplay_MouseWheel;
            ImageDisplay.MouseLeave += MicrographDisplay_MouseLeave;
        }

        private void MicrographDisplay_MouseLeave(object sender, MouseEventArgs e)
        {
            CanvasTrack.Children.Clear();
        }

        private void ImageDisplay_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            Downsample = Math.Max(0, Math.Min(4, Downsample + Math.Sign(-e.Delta)));
            UpdateScale();
        }

        private void Movie_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "AverageImage")
                Dispatcher.Invoke(() => UpdateDisplay());
        }

        public void UpdateDisplay()
        {
            if (Movie?.AverageImage != null)
            {
                ImageDisplay.Source = Movie.AverageImage;
                
                UpdateScale();
            }
            else
            {
                ImageDisplay.Source = null;
            }
        }

        public void UpdateScale()
        {
            if (ImageDisplay.Source != null)
            {
                ImageDisplay.Width = ImageDisplay.Source.Width * ScaleFactor;
                ImageDisplay.Height = ImageDisplay.Source.Height * ScaleFactor;

                CanvasTrack.Width = ImageDisplay.Source.Width * ScaleFactor;
                CanvasTrack.Height = ImageDisplay.Source.Height * ScaleFactor;
            }
        }

        private void ImageDisplay_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            CanvasTrack.Children.Clear();
            if (Movie == null)
                return;

            double Scale = ScaleFactor * 10;

            // Get motion track at mouse position.
            {
                Point MousePos = e.GetPosition(CanvasTrack);
                float2 NormalizedPosition = new float2((float)(MousePos.X / CanvasTrack.Width),
                                                       (float)(MousePos.Y / CanvasTrack.Height));
                float2[] TrackData = Movie.GetMotionTrack(NormalizedPosition, 1);
                if (TrackData == null)
                    return;
                Point[] TrackPoints = TrackData.Select(v => new Point((-v.X + TrackData[0].X) * Scale, (-v.Y + TrackData[0].Y) * Scale)).ToArray();

                // Construct path.
                Path TrackPath = new Path()
                {
                    Stroke = new SolidColorBrush(Colors.DeepSkyBlue),
                    StrokeThickness = 2.5
                };
                PolyLineSegment PlotSegment = new PolyLineSegment(TrackPoints, true);
                PathFigure PlotFigure = new PathFigure
                {
                    Segments = new PathSegmentCollection { PlotSegment },
                    StartPoint = TrackPoints[0]
                };
                TrackPath.Data = new PathGeometry { Figures = new PathFigureCollection { PlotFigure } };

                var TrackShadow = new DropShadowEffect
                {
                    Opacity = 2,
                    Color = Colors.Black,
                    BlurRadius = 6,
                    ShadowDepth = 0,
                    RenderingBias = RenderingBias.Quality
                };
                TrackPath.Effect = TrackShadow;

                TrackPath.PreviewMouseWheel += ImageDisplay_MouseWheel;
                TrackPath.PreviewMouseMove += ImageDisplay_OnPreviewMouseMove;

                CanvasTrack.Children.Add(TrackPath);
                Canvas.SetLeft(TrackPath, MousePos.X);
                Canvas.SetTop(TrackPath, MousePos.Y);

                for (int z = 0; z < TrackPoints.Length / 1 + 1; z++)
                {
                    Point DotPosition = TrackPoints[Math.Min(TrackPoints.Length - 1, z * 1)];
                    Ellipse Dot = new Ellipse()
                    {
                        Width = 4,
                        Height = 4,
                        Fill = new SolidColorBrush(Colors.DeepSkyBlue),
                        StrokeThickness = 0,
                        Effect = TrackShadow
                    };
                    Dot.PreviewMouseWheel += ImageDisplay_MouseWheel;
                    Dot.PreviewMouseMove += ImageDisplay_OnPreviewMouseMove;

                    CanvasTrack.Children.Add(Dot);
                    Canvas.SetLeft(Dot, MousePos.X + DotPosition.X - 2.0);
                    Canvas.SetTop(Dot, MousePos.Y + DotPosition.Y - 2.0);
                }
            }

            Movie TempMovie = Movie;

            Parallel.For(0, 10, y =>
            {
                for (int x = 0; x < 10; x++)
                {
                    float2 NormalizedPosition = new float2((x + 1) * (1f / 12f), (y + 1) * (1f / 12f));

                    float2[] TrackData = TempMovie.GetMotionTrack(NormalizedPosition, 1);
                    if (TrackData == null)
                        continue;
                    Point[] TrackPoints = TrackData.Select(v => new Point((-v.X + TrackData[0].X) * Scale, (-v.Y + TrackData[0].Y) * Scale)).ToArray();

                    // Construct path.
                    CanvasTrack.Dispatcher.InvokeAsync(() =>
                    {
                        Path TrackPath = new Path()
                        {
                            Stroke = new SolidColorBrush(Colors.White),
                            StrokeThickness = 2.0
                        };
                        PolyLineSegment PlotSegment = new PolyLineSegment(TrackPoints, true);
                        PathFigure PlotFigure = new PathFigure
                        {
                            Segments = new PathSegmentCollection { PlotSegment },
                            StartPoint = TrackPoints[0]
                        };
                        TrackPath.Data = new PathGeometry { Figures = new PathFigureCollection { PlotFigure } };

                        TrackPath.PreviewMouseWheel += ImageDisplay_MouseWheel;
                        TrackPath.PreviewMouseMove += ImageDisplay_OnPreviewMouseMove;

                        CanvasTrack.Children.Add(TrackPath);
                        Canvas.SetLeft(TrackPath, NormalizedPosition.X * CanvasTrack.Width);
                        Canvas.SetTop(TrackPath, NormalizedPosition.Y * CanvasTrack.Height);
                    });
                }
            });
        }
    }
}
