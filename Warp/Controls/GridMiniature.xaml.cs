using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
    /// Interaction logic for GridMiniature.xaml
    /// </summary>
    public partial class GridMiniature : UserControl
    {
        public int PointsX
        {
            get { return (int)GetValue(PointsXProperty); }
            set { SetValue(PointsXProperty, value); }
        }

        public static readonly DependencyProperty PointsXProperty =
            DependencyProperty.Register("PointsX", typeof (int), typeof (GridMiniature),
                new PropertyMetadata(1, (sender, e) => ((GridMiniature) sender).UpdateLines()));

        public int PointsY
        {
            get { return (int)GetValue(PointsYProperty); }
            set { SetValue(PointsYProperty, value); }
        }
        public static readonly DependencyProperty PointsYProperty =
            DependencyProperty.Register("PointsY", typeof(int), typeof(GridMiniature),
                new PropertyMetadata(1, (sender, e) => ((GridMiniature)sender).UpdateLines()));

        public int PointsZ
        {
            get { return (int)GetValue(PointsZProperty); }
            set { SetValue(PointsZProperty, value); }
        }
        public static readonly DependencyProperty PointsZProperty =
            DependencyProperty.Register("PointsZ", typeof(int), typeof(GridMiniature),
                new PropertyMetadata(1, (sender, e) => ((GridMiniature)sender).UpdateLines()));

        public GridMiniature()
        {
            InitializeComponent();
            SizeChanged += (sender, args) => UpdateLines();
        }

        private void UpdateLines()
        {
            MainCanvas.Children.Clear();

            float2 Center = new float2((float)ActualWidth / 2f, (float)ActualHeight / 2f);
            float StepX = PointsX > 1 ? 1f / (float)(PointsX - 1) : 0f;
            float StepY = PointsY > 1 ? 1f / (float)(PointsY - 1) : 0f;
            float2 Origin = new float2(PointsX > 1 ? 0 : 0.5f, PointsY > 1 ? 0 : 0.5f);

            float2 VecX = new float2((float) ActualWidth / 2f, (float) ActualHeight / 2f);
            float2 VecY = new float2(-(float) ActualWidth / 2f, (float) ActualHeight / 2f);
            float2 Offset = new float2((float)ActualWidth / 2f, 0f);

            for (int x = 0; x < PointsX; x++)
            {
                float2 From = new float2(Origin.X + x * StepX, Origin.Y);
                From = new float2(VecX.X * From.X + VecY.X * From.Y + Offset.X, VecX.Y * From.X + VecY.Y * From.Y + Offset.Y);
                float2 To = new float2(Origin.X + x * StepX, Origin.Y + (PointsY - 1) * StepY);
                To = new float2(VecX.X * To.X + VecY.X * To.Y + Offset.X, VecX.Y * To.X + VecY.Y * To.Y + Offset.Y);

                if (PointsY > 1)
                {
                    Line XLine = new Line();
                    XLine.Stroke = new SolidColorBrush(Colors.Black);

                    XLine.X1 = From.X;
                    XLine.Y1 = From.Y;
                    XLine.X2 = To.X;
                    XLine.Y2 = To.Y;

                    MainCanvas.Children.Add(XLine);
                }
                else
                {
                    Ellipse XCircle = new Ellipse();
                    XCircle.Fill = new SolidColorBrush(Colors.Black);
                    XCircle.Width = 4;
                    XCircle.Height = 4;

                    MainCanvas.Children.Add(XCircle);
                    Canvas.SetLeft(XCircle, From.X - 2f);
                    Canvas.SetTop(XCircle, From.Y - 2f);
                }

                if (PointsZ > 1 && (x == PointsX - 1 || PointsY <= 1))
                {
                    for (int z = 1; z < Math.Min(4, PointsZ); z++)
                    {
                        float GreyValue = z / 5f;
                        Color LineColor = Color.FromScRgb(1f, GreyValue, GreyValue, GreyValue);

                        if (PointsY > 1)
                        {
                            Line XLine = new Line();
                            XLine.Stroke = new SolidColorBrush(LineColor);

                            XLine.X1 = From.X;
                            XLine.Y1 = From.Y + 4 * z;
                            XLine.X2 = To.X;
                            XLine.Y2 = To.Y + 4 * z;

                            MainCanvas.Children.Add(XLine);
                        }
                        else
                        {
                            Ellipse XCircle = new Ellipse();
                            XCircle.Fill = new SolidColorBrush(LineColor);
                            XCircle.Width = 4;
                            XCircle.Height = 4;

                            MainCanvas.Children.Add(XCircle);
                            Canvas.SetLeft(XCircle, From.X - 2f);
                            Canvas.SetTop(XCircle, From.Y - 2f + 4 * z);
                        }
                    }
                }
            }

            for (int y = 0; y < PointsY; y++)
            {
                float2 From = new float2(Origin.X, Origin.Y + y * StepY);
                From = new float2(VecX.X * From.X + VecY.X * From.Y + Offset.X, VecX.Y * From.X + VecY.Y * From.Y + Offset.Y);
                float2 To = new float2(Origin.X + (PointsX - 1) * StepX, Origin.Y + y * StepY);
                To = new float2(VecX.X * To.X + VecY.X * To.Y + Offset.X, VecX.Y * To.X + VecY.Y * To.Y + Offset.Y);

                if (PointsX > 1)
                {
                    Line YLine = new Line();
                    YLine.Stroke = new SolidColorBrush(Colors.Black);

                    YLine.X1 = From.X;
                    YLine.Y1 = From.Y;
                    YLine.X2 = To.X;
                    YLine.Y2 = To.Y;

                    MainCanvas.Children.Add(YLine);
                }
                else
                {
                    Ellipse YCircle = new Ellipse();
                    YCircle.Fill = new SolidColorBrush(Colors.Black);
                    YCircle.Width = 4;
                    YCircle.Height = 4;

                    MainCanvas.Children.Add(YCircle);
                    Canvas.SetLeft(YCircle, From.X - 2f);
                    Canvas.SetTop(YCircle, From.Y - 2f);
                }

                if (PointsZ > 1 && (y == PointsY - 1 || PointsX <= 1))
                {
                    for (int z = 1; z < Math.Min(4, PointsZ); z++)
                    {
                        float GreyValue = z / 5f;
                        Color LineColor = Color.FromScRgb(1f, GreyValue, GreyValue, GreyValue);

                        if (PointsX > 1)
                        {
                            Line YLine = new Line();
                            YLine.Stroke = new SolidColorBrush(LineColor);

                            YLine.X1 = From.X;
                            YLine.Y1 = From.Y + 4 * z;
                            YLine.X2 = To.X;
                            YLine.Y2 = To.Y + 4 * z;

                            MainCanvas.Children.Add(YLine);
                        }
                        else
                        {
                            Ellipse YCircle = new Ellipse();
                            YCircle.Fill = new SolidColorBrush(LineColor);
                            YCircle.Width = 4;
                            YCircle.Height = 4;

                            MainCanvas.Children.Add(YCircle);
                            Canvas.SetLeft(YCircle, From.X - 2f);
                            Canvas.SetTop(YCircle, From.Y - 2f + 4 * z);
                        }
                    }
                }
            }
        }
    }
}
