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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Warp.Tools;

namespace Warp.Controls
{
    /// <summary>
    /// Interaction logic for CTFDisplay.xaml
    /// </summary>
    public partial class CTFDisplay : UserControl
    {
        public ImageSource PS2D
        {
            get { return (ImageSource)GetValue(PS2DProperty); }
            set { SetValue(PS2DProperty, value); }
        }
        public static readonly DependencyProperty PS2DProperty = 
            DependencyProperty.Register("PS2D", typeof(ImageSource), typeof(CTFDisplay), new PropertyMetadata(null, (sender, e) => ((CTFDisplay)sender).UpdateElements()));

        public ImageSource Simulated2D
        {
            get { return (ImageSource)GetValue(Simulated2DProperty); }
            set { SetValue(Simulated2DProperty, value); }
        }
        public static readonly DependencyProperty Simulated2DProperty = 
            DependencyProperty.Register("Simulated2D", typeof(ImageSource), typeof(CTFDisplay), new PropertyMetadata(null, (sender, e) => ((CTFDisplay)sender).UpdateElements()));

        public float2[] PS1D
        {
            get { return (float2[])GetValue(PS1DProperty); }
            set { SetValue(PS1DProperty, value); }
        }
        public static readonly DependencyProperty PS1DProperty = 
            DependencyProperty.Register("PS1D", typeof(float2[]), typeof(CTFDisplay), new PropertyMetadata(null, (sender, e) => ((CTFDisplay)sender).UpdateElements()));

        public float2[] Simulated1D
        {
            get { return (float2[])GetValue(Simulated1DProperty); }
            set { SetValue(Simulated1DProperty, value); }
        }
        public static readonly DependencyProperty Simulated1DProperty = 
            DependencyProperty.Register("Simulated1D", typeof(float2[]), typeof(CTFDisplay), new PropertyMetadata(null, (sender, e) => ((CTFDisplay)sender).UpdateElements()));

        public float2[] Quality
        {
            get { return (float2[])GetValue(QualityProperty); }
            set { SetValue(QualityProperty, value); }
        }
        public static readonly DependencyProperty QualityProperty = 
            DependencyProperty.Register("Quality", typeof (float2[]), typeof (CTFDisplay), new PropertyMetadata(null, (sender, e) => ((CTFDisplay) sender).UpdateElements()));

        public decimal QualityThreshold
        {
            get { return (decimal)GetValue(QualityThresholdProperty); }
            set { SetValue(QualityThresholdProperty, value); }
        }
        public static readonly DependencyProperty QualityThresholdProperty =
            DependencyProperty.Register("QualityThreshold", typeof(decimal), typeof(CTFDisplay), new PropertyMetadata(0M, (sender, e) => ((CTFDisplay)sender).UpdateElements()));

        public decimal FreqRangeMin
        {
            get { return (decimal)GetValue(FreqRangeMinProperty); }
            set { SetValue(FreqRangeMinProperty, value); }
        }
        public static readonly DependencyProperty FreqRangeMinProperty =
            DependencyProperty.Register("FreqRangeMin", typeof(decimal), typeof(CTFDisplay), new PropertyMetadata(0M, (sender, e) => ((CTFDisplay)sender).UpdateElements()));

        public decimal FreqRangeMax
        {
            get { return (decimal)GetValue(FreqRangeMaxProperty); }
            set { SetValue(FreqRangeMaxProperty, value); }
        }
        public static readonly DependencyProperty FreqRangeMaxProperty =
            DependencyProperty.Register("FreqRangeMax", typeof(decimal), typeof(CTFDisplay), new PropertyMetadata(0M, (sender, e) => ((CTFDisplay)sender).UpdateElements()));


        public CTFDisplay()
        {
            InitializeComponent();
            SizeChanged += CTFDisplay_SizeChanged;
            UpdateElements();
        }

        private void CTFDisplay_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateElements();
        }

        private void UpdateElements()
        {
            ImagePS2D.Clip = new EllipseGeometry(new Point(ImagePS2D.ActualWidth / 2, 0), ImagePS2D.ActualWidth / 2, ImagePS2D.ActualHeight);
            ImageSimulated2D.Clip = new EllipseGeometry(new Point(ImageSimulated2D.ActualWidth, ImageSimulated2D.ActualHeight), ImageSimulated2D.ActualWidth, ImageSimulated2D.ActualHeight);

            if (PS2D == null)
            {
                ImagePS2D.Source = null;
                ImageSimulated2D.Source = null;
                CanvasCurves1D.Visibility = Visibility.Hidden;
                return;
            }

            CanvasCurves1D.Visibility = Visibility.Visible;
            ImagePS2D.Source = PS2D;
            ImageSimulated2D.Source = Simulated2D;

            double MinPS = float.MaxValue, MaxPS = -float.MaxValue;
            double MaxX = -float.MaxValue;

            if (PS1D == null && Simulated1D == null)
            {
                MinPS = 0;
                MaxPS = MaxX = 1;
                RectangleRange.Width = 0;
            }
            else
            {
                Canvas.SetLeft(RectangleRange, (double)FreqRangeMin * CanvasCurves1D.ActualWidth);
                RectangleRange.Width = (double) Math.Max(0, FreqRangeMax - FreqRangeMin) * CanvasCurves1D.ActualWidth;
                RectangleRange.Height = CanvasCurves1D.ActualHeight;

                // Min is absolute minimum, max is min(mean + 2.5 * stddev, absolute maximum).
                if (PS1D != null)
                {
                    int IndexMin = (int)(FreqRangeMin * PS1D.Length);
                    int IndexMax = (int) (FreqRangeMax * PS1D.Length);
                    float2[] ForPS1D = PS1D.Skip(IndexMin).Take(Math.Max(1, IndexMax - IndexMin)).ToArray();

                    MinPS = ForPS1D.Aggregate(MinPS, (current, p) => Math.Min(current, p.Y));
                    float Max = MathHelper.Max(ForPS1D.Select(i => i.Y));
                    MaxPS = Math.Max(MaxPS, Math.Min(Max, MathHelper.Mean(ForPS1D.Select(i => i.Y)) + MathHelper.StdDev(ForPS1D.Select(i => i.Y)) * 3.0));

                    MaxX = Math.Max(MathHelper.Max(PS1D.Select(i => i.X)), MaxX);
                }
                if (Simulated1D != null)
                {
                    int IndexMin = (int)(FreqRangeMin * Simulated1D.Length);
                    int IndexMax = (int)(FreqRangeMax * Simulated1D.Length);
                    float2[] ForSimulated1D = Simulated1D.Skip(IndexMin).Take(Math.Max(1, IndexMax - IndexMin)).ToArray();

                    MinPS = ForSimulated1D.Aggregate(MinPS, (current, p) => Math.Min(current, p.Y));
                    float Max = MathHelper.Max(ForSimulated1D.Select(i => i.Y));
                    MaxPS = Math.Max(MaxPS, Math.Min(Max, MathHelper.Mean(ForSimulated1D.Select(i => i.Y)) + MathHelper.StdDev(ForSimulated1D.Select(i => i.Y)) * 3.0));

                    MaxX = Math.Max(MathHelper.Max(Simulated1D.Select(i => i.X)), MaxX);
                }
            }
            double ExtentPS = MaxPS - MinPS;
            double HeightPS = 1.0 * CanvasCurves1D.ActualHeight - 8.0;
            double WidthPS = 1.0 * CanvasCurves1D.ActualWidth;
            double ScaleX = WidthPS / MaxX;
            double ScaleY = HeightPS / ExtentPS;

            double MinBottom = 0;
            if (PS1D != null)
            {
                int IndexMin = (int)(FreqRangeMin * PS1D.Length);
                int IndexMax = (int)(FreqRangeMax * PS1D.Length);
                float2[] ForPS1D = PS1D.Skip(IndexMin).Take(Math.Max(1, IndexMax - IndexMin)).ToArray();

                List<Point> PlotPoints = PS1D.Select(point => new Point(point.X * ScaleX, HeightPS - (point.Y - MinPS) * ScaleY + 4.0)).ToList();
                MinBottom = Math.Min(MinBottom, (MathHelper.Min(ForPS1D.Select(i => i.Y)) - MinPS) * ScaleY + (HeightPS - MathHelper.Max(PlotPoints.Select(point => (float)point.Y))) + 6.0);
            }
            if (Simulated1D != null)
            {
                int IndexMin = (int)(FreqRangeMin * Simulated1D.Length);
                int IndexMax = (int)(FreqRangeMax * Simulated1D.Length);
                float2[] ForSimulated1D = Simulated1D.Skip(IndexMin).Take(Math.Max(1, IndexMax - IndexMin)).ToArray();

                List<Point> PlotPoints = Simulated1D.Select(point => new Point(point.X * ScaleX, HeightPS - (point.Y - MinPS) * ScaleY + 4.0)).ToList();
                MinBottom = Math.Min(MinBottom, (MathHelper.Min(ForSimulated1D.Select(i => i.Y)) - MinPS) * ScaleY + (HeightPS - MathHelper.Max(PlotPoints.Select(point => (float)point.Y))) + 6.0);
            }

            if (PS1D != null)
            {
                int IndexMin = (int)(FreqRangeMin * PS1D.Length);
                int IndexMax = (int)(FreqRangeMax * PS1D.Length);
                float2[] ForPS1D = PS1D.Skip(IndexMin).Take(Math.Max(1, IndexMax - IndexMin)).ToArray();

                List<Point> PlotPoints = PS1D.Select(point => new Point(point.X * ScaleX, HeightPS - (point.Y - MinPS) * ScaleY + 4.0)).ToList();
                PolyLineSegment PlotSegment = new PolyLineSegment(PlotPoints, true);
                PathFigure PlotFigure = new PathFigure { Segments = new PathSegmentCollection { PlotSegment }, StartPoint = PlotPoints[0]};

                PathPS1D.Data = new PathGeometry { Figures = new PathFigureCollection { PlotFigure } };
                Canvas.SetBottom(PathPS1D, HeightPS - MathHelper.Max(PlotPoints.Select(point => (float)point.Y)) + 6.0);
            }

            if (Simulated1D != null)
            {
                int IndexMin = (int)(FreqRangeMin * Simulated1D.Length);
                int IndexMax = (int)(FreqRangeMax * Simulated1D.Length);
                float2[] ForSimulated1D = Simulated1D.Skip(IndexMin).Take(Math.Max(1, IndexMax - IndexMin)).ToArray();

                List<Point> PlotPoints = ForSimulated1D.Select(point => new Point(point.X * ScaleX, HeightPS - (point.Y - MinPS) * ScaleY + 4.0)).ToList();
                PolyLineSegment PlotSegment = new PolyLineSegment(PlotPoints, true);
                PathFigure PlotFigure = new PathFigure { Segments = new PathSegmentCollection { PlotSegment }, StartPoint = PlotPoints[0] };

                PathSimulated1D.Data = new PathGeometry { Figures = new PathFigureCollection { PlotFigure } };
                Canvas.SetBottom(PathSimulated1D, HeightPS - MathHelper.Max(PlotPoints.Select(point => (float)point.Y)) + 6.0);
            }

            if (Quality != null)
            {
                int QualityLimit = Quality.Length - 1;
                for (int i = 0; i < Quality.Length - 1; i++)
                    if (Math.Max(Quality[i].Y, Quality[i + 1].Y) < (float) QualityThreshold)
                    {
                        QualityLimit = i;
                        break;
                    }
                float2[] QualityGood = Quality.Take(QualityLimit + 1).ToArray();
                float2[] QualityBad = Quality.Skip(QualityLimit).ToArray();
                float MinGlobal = MathHelper.Min(Quality.Select(v => v.Y));

                if (QualityGood.Length > 1)
                {
                    float MinGood = MathHelper.Min(QualityGood.Select(v => v.Y));
                    List<Point> PlotPoints = QualityGood.Select(point => new Point(point.X * ScaleX, (1.0 - point.Y) * 2.0 / 3.0 * HeightPS)).ToList();
                    PolyLineSegment PlotSegment = new PolyLineSegment(PlotPoints, true);
                    PathFigure PlotFigure = new PathFigure
                    {
                        Segments = new PathSegmentCollection {PlotSegment},
                        StartPoint = PlotPoints[0]
                    };

                    PathGoodQuality.Data = new PathGeometry {Figures = new PathFigureCollection {PlotFigure}};
                    CanvasGoodQuality.Height = MathHelper.Max(PlotPoints.Select(point => (float) point.Y));
                    Canvas.SetBottom(CanvasGoodQuality, HeightPS * 2.0 / 3.0 - MathHelper.Max(PlotPoints.Select(point => (float)point.Y)));
                }
                else
                    PathGoodQuality.Data = null;

                if (QualityBad.Length > 1)
                {
                    float MinBad = MathHelper.Min(QualityBad.Select(v => v.Y));
                    List<Point> PlotPoints = QualityBad.Select(point => new Point(point.X * ScaleX, (1.0 - point.Y) * 2.0 / 3.0 * HeightPS)).ToList();
                    PolyLineSegment PlotSegment = new PolyLineSegment(PlotPoints, true);
                    PathFigure PlotFigure = new PathFigure
                    {
                        Segments = new PathSegmentCollection {PlotSegment},
                        StartPoint = PlotPoints[0]
                    };

                    PathBadQuality.Data = new PathGeometry {Figures = new PathFigureCollection {PlotFigure}};
                    CanvasBadQuality.Height = MathHelper.Max(PlotPoints.Select(point => (float)point.Y));
                    Canvas.SetBottom(CanvasBadQuality, HeightPS * 2.0 / 3.0 - MathHelper.Max(PlotPoints.Select(point => (float)point.Y)));
                }
                else
                    PathBadQuality.Data = null;
            }
            else
            {
                PathGoodQuality.Data = null;
                PathBadQuality.Data = null;
            }

            // Quality threshold
            {
                Canvas.SetBottom(PanelQualityThreshold, HeightPS * 2.0 / 3.0 * (double) QualityThreshold);
                TextQualityThreshold.Text = $"{QualityThreshold:0.00}";

                double QualityMinX = 0;
                if (Quality != null)
                    QualityMinX = MathHelper.Min(Quality.Select(i => i.X));
                double QualityWidth = (MaxX - QualityMinX) * ScaleX;
                LineQualityThreshold.X2 = QualityWidth;
            }
        }
    }
}
