using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Warp.Controls
{
    /// <summary>
    /// Interaction logic for StatusBar.xaml
    /// </summary>
    public partial class StatusBar : UserControl
    {
        public ObservableCollection<Movie> Movies
        {
            get { return (ObservableCollection<Movie>)GetValue(MoviesProperty); }
            set { SetValue(MoviesProperty, value); }
        }
        public static readonly DependencyProperty MoviesProperty =
            DependencyProperty.Register("Movies", typeof(ObservableCollection<Movie>), typeof(StatusBar), new PropertyMetadata(new ObservableCollection<Movie>(),
                (sender, args) =>
                {
                    StatusBar Sender = (StatusBar)sender;
                    ObservableCollection<Movie> OldValue = args.OldValue as ObservableCollection<Movie>;
                    if (OldValue != null)
                        OldValue.CollectionChanged -= Sender.Movies_CollectionChanged;
                    ObservableCollection<Movie> NewValue = args.NewValue as ObservableCollection<Movie>;
                    if (NewValue != null)
                        NewValue.CollectionChanged += Sender.Movies_CollectionChanged;
                }));

        public StatusBar()
        {
            SizeChanged += (sender, e) => UpdateElements();
            DataContextChanged += (sender, e) => { if (DataContext?.GetType() == typeof(Options)) ((Options)DataContext).PropertyChanged += Options_PropertyChanged; };
            InitializeComponent();

            UpdateElements();
        }

        private void Options_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "DisplayedMovie")
                UpdateElements();
        }

        private void Movies_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
                foreach (var item in e.OldItems.Cast<Movie>())
                    item.PropertyChanged -= Movie_PropertyChanged;

            if (e.NewItems != null)
                foreach (var item in e.NewItems.Cast<Movie>())
                    item.PropertyChanged += Movie_PropertyChanged;

            UpdateElements();
        }

        private void Movie_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "Status")
                return;

            UpdateElements();
        }

        private void UpdateElements()
        {
            Dispatcher.Invoke(() =>
            {
                lock (this)
                {
                    PanelSegments.Children.Clear();
                    List<Movie> ImmutableMovies = Movies.ToList();

                    // Update number of processed movies.
                    int NProcessed = ImmutableMovies.Sum(m => m.Status == ProcessingStatus.Processed ? 1 : 0);
                    int NProcessable = ImmutableMovies.Sum(m => m.Status != ProcessingStatus.Skip ? 1 : 0);
                    TextNumberProcessed.Text = $"Processed {NProcessed}/{NProcessable}.";

                    if (DataContext?.GetType() != typeof (Options))
                        return;
                    Movie CurrentMovie = ((Options) DataContext).DisplayedMovie;

                    if (ImmutableMovies.Count == 0) // Hide unnecessary elements.
                    {
                        HideElements();
                    }
                    else
                    {
                        ShowElements();

                        // Create colored navigation bar.
                        float StepLength = (float) ActualWidth / ImmutableMovies.Count;
                        ProcessingStatus CurrentStatus = ImmutableMovies[0].Status;
                        int CurrentSteps = 0;
                        foreach (var movie in ImmutableMovies)
                        {
                            CurrentSteps++;

                            if (movie.Status != CurrentStatus)
                            {
                                PanelSegments.Children.Add(new Rectangle
                                {
                                    Width = CurrentSteps * StepLength,
                                    Height = 4,
                                    Fill = StatusToBrush(CurrentStatus)
                                });

                                CurrentStatus = movie.Status;
                                CurrentSteps = 0;
                            }
                        }
                        if (CurrentSteps > 0)
                        {
                            PanelSegments.Children.Add(new Rectangle
                            {
                                Width = CurrentSteps * StepLength,
                                Height = 4,
                                Fill = StatusToBrush(CurrentStatus)
                            });
                        }

                        // Update tracker position
                        if (CurrentMovie != null)
                        {
                            int PositionIndex = ImmutableMovies.IndexOf(CurrentMovie);
                            float IdealOffset = (PositionIndex + 0.5f) * StepLength;
                            PathPosition.Margin =
                                new Thickness(Math.Max(0, Math.Min(IdealOffset - (float) PathPosition.ActualWidth / 2f,
                                              ActualWidth - PathPosition.ActualWidth)), 0, 0, 0);

                            //TextCurrentName.Content = CurrentMovie.RootName;
                            TextCurrentName.Margin =
                                new Thickness(Math.Max(0, Math.Min(IdealOffset - (float) TextCurrentName.ActualWidth / 2f,
                                              ActualWidth - TextCurrentName.ActualWidth)), 0, 0, 0);
                        }
                        else
                        {
                            HideElements();
                        }
                    }
                }
            });
        }

        private void ShowElements()
        {
            PathPosition.Visibility = Visibility.Visible;
            TextCurrentName.Visibility = Visibility.Visible;
        }

        private void HideElements()
        {
            PathPosition.Visibility = Visibility.Hidden;
            TextCurrentName.Visibility = Visibility.Hidden;
        }

        private static Brush StatusToBrush(ProcessingStatus status)
        {
            if (status == ProcessingStatus.Processed)
                return new SolidColorBrush(Colors.Green);
            else if (status == ProcessingStatus.Outdated)
                return new SolidColorBrush(Colors.Orange);
            else if (status == ProcessingStatus.Unprocessed)
                return new SolidColorBrush(Colors.Red);
            else
                return new SolidColorBrush(Colors.DarkGray);
        }

        private void MainGrid_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Movies.Count == 0)
                return;

            if (MainWindow.Options.DisplayedMovie == null)
            {
                MainWindow.Options.DisplayedMovie = Movies[0];
                return;
            }
            else
            {
                int NewIndex = Movies.IndexOf(MainWindow.Options.DisplayedMovie) + (e.Delta < 0 ? 1 : -1);
                MainWindow.Options.DisplayedMovie = Movies[Math.Max(0, Math.Min(NewIndex, Movies.Count - 1))];
            }
        }

        private void MainGrid_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (Movies.Count == 0)
                return;

            float StepLength = (float)ActualWidth / Movies.Count;
            int NewIndex = (int) ((float) e.GetPosition(this).X / StepLength);
            NewIndex = Math.Max(0, Math.Min(NewIndex, Movies.Count - 1));

            MainWindow.Options.DisplayedMovie = Movies[NewIndex];
        }
    }
}
