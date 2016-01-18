using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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

namespace Warp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MahApps.Metro.Controls.MetroWindow
    {
        public static Options Options = new Options();
        public static bool IsProcessing = false;

        readonly List<UIElement> DisableWhenRunning;

        FileDiscoverer ImageDiscoverer = new FileDiscoverer();

        public MainWindow()
        {
            try
            {
                Options.DeviceCount = GPU.GetDeviceCount();
                if (Options.DeviceCount <= 0)
                    throw new Exception();
            }
            catch (Exception)
            {
                MessageBox.Show("No CUDA devices found, shutting down.");
                Close();
            }

            DataContext = Options;
            Options.PropertyChanged += Options_PropertyChanged;
            Closing += MainWindow_Closing;

            InitializeComponent();

            DisableWhenRunning = new List<UIElement>
            {
                GridOptionsIO,
                GridOptionsPreprocessing,
                GridOptionsCTF,
                GridOptionsMovement,
                GridOptionsGrids,
                GridOptionsPostprocessing
            };

            if (File.Exists("Previous.settings"))
                Options.Load("Previous.settings");

            for (int i = 0; i < GPU.GetDeviceCount(); i++)
            {
                GPU.SetDevice(i);
                Console.WriteLine($"Device {i}:");
                Console.WriteLine($"Free memory: {GPU.GetFreeMemory()} MB");
                Console.WriteLine($"Total memory: {GPU.GetTotalMemory()} MB");
            }

            // Create mockup
            {
                float2[] SplinePoints = { new float2(0f, 0f), new float2(1f / 3f, 1f)};//, new float2(2f / 3f, 0f)};//, new float2(1f, 1f) };
                Cubic1D ReferenceSpline = new Cubic1D(SplinePoints);
                Cubic1DShort ShortSpline = Cubic1DShort.GetInterpolator(SplinePoints);
                for (float i = -1f; i < 2f; i += 0.01f)
                {
                    float Reference = ReferenceSpline.Interp(i);
                    float Test = ShortSpline.Interp(i);
                    if (Math.Abs(Reference - Test) > 1e-6f)
                        throw new Exception();
                }

                Random Rnd = new Random(123);
                int3 GridDim = new int3(1, 1, 1);
                float[] GridValues = new float[GridDim.Elements()];
                for (int i = 0; i < GridValues.Length; i++)
                    GridValues[i] = (float)Rnd.NextDouble();
                CubicGrid CGrid = new CubicGrid(GridDim, GridValues);
                float[] Managed = CGrid.GetInterpolated(new int3(16, 16, 16), new float3(0, 0, 0));
                float[] Native = CGrid.GetInterpolatedNative(new int3(16, 16, 16), new float3(0, 0, 0));
                for (int i = 0; i < Managed.Length; i++)
                    if (Math.Abs(Managed[i] - Native[i]) > 1e-6f)
                        throw new Exception();

                /*Options.Movies.Add(new Movie(@"D:\Dev\warp\May19_21.44.54.mrc"));
                Options.Movies.Add(new Movie(@"D:\Dev\warp\May19_21.49.06.mrc"));
                Options.Movies.Add(new Movie(@"D:\Dev\warp\May19_21.50.48.mrc"));
                Options.Movies.Add(new Movie(@"D:\Dev\warp\May19_21.52.16.mrc"));
                Options.Movies.Add(new Movie(@"D:\Dev\warp\May19_21.53.43.mrc"));

                CTFDisplay.PS2D = new BitmapImage();*/

                /*float2[] SimCoords = new float2[512 * 512];
                for (int y = 0; y < 512; y++)
                    for (int x = 0; x < 512; x++)
                    {
                        int xcoord = x - 512, ycoord = y - 512;
                        SimCoords[y * 512 + x] = new float2((float) Math.Sqrt(xcoord * xcoord + ycoord * ycoord),
                            (float) Math.Atan2(ycoord, xcoord));
                    }
                float[] Sim2D = new CTF {Defocus = -2M}.Get2D(SimCoords, 512, true);
                byte[] Sim2DBytes = new byte[Sim2D.Length];
                for (int i = 0; i < 512 * 512; i++)
                    Sim2DBytes[i] = (byte) (Sim2D[i] * 255f);
                BitmapSource Sim2DSource = BitmapSource.Create(512, 512, 96, 96, PixelFormats.Indexed8, BitmapPalettes.Gray256, Sim2DBytes, 512);
                CTFDisplay.Simulated2D = Sim2DSource;*/

                /*float2[] PointsPS1D = new float2[512];
                for (int i = 0; i < PointsPS1D.Length; i++)
                    PointsPS1D[i] = new float2(i, (float) Math.Exp(-i / 300f));
                CTFDisplay.PS1D = PointsPS1D;

                float[] SimCTF = new CTF { Defocus = -2M }.Get1D(512, true);
                float2[] PointsSim1D = new float2[SimCTF.Length];
                for (int i = 0; i < SimCTF.Length; i++)
                    PointsSim1D[i] = new float2(i, SimCTF[i] * (float)Math.Exp(-i / 100f) + (float)Math.Exp(-i / 300f));
                CTFDisplay.Simulated1D = PointsSim1D;*/

                /*CubicGrid Grid = new CubicGrid(new int3(5, 5, 5), 0, 0, Dimension.X);
                Grid.Values[2, 2, 2] = 1f;
                float[] Data = new float[11 * 11 * 11];
                int i = 0;
                for (float z = 0f; z < 1.05f; z += 0.1f)
                    for (float y = 0f; y < 1.05f; y += 0.1f)
                        for (float x = 0f; x < 1.05f; x += 0.1f)
                            Data[i++] = Grid.GetInterpolated(new float3(x, y, z));
                Image DataImage = new Image(Data, new int3(11, 11, 11));
                DataImage.WriteMRC("bla.mrc");

                Image GPUImage = new Image(DataImage.GetDevice(Intent.Read), new int3(11, 11, 11));
                GPUImage.WriteMRC("gpu.mrc");*/

                /*CubicGrid WiggleGrid = new CubicGrid(new int3(2, 2, 1));
                float[][] WiggleWeights = WiggleGrid.GetWiggleWeights(new int3(3, 3, 1));*/
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                Options.Save("Previous.settings");
                ImageDiscoverer.Shutdown();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void Options_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "InputFolder")
            {
                if (!IOHelper.CheckFolderPermission(Options.InputFolder))
                {
                    Options.InputFolder = "";
                    return;
                }
                ButtonInputPathText.Text = Options.InputFolder == "" ? "Select folder..." : Options.InputFolder;
                AdjustInput();
            }
            else if (e.PropertyName == "InputExtension")
            {
                AdjustInput();
            }
            else if (e.PropertyName == "OutputFolder")
            {
                if (!IOHelper.CheckFolderPermission(Options.OutputFolder))
                {
                    Options.OutputFolder = "";
                    return;
                }
                ButtonOutputPathText.Text = Options.OutputFolder == "" ? "Select folder..." : Options.OutputFolder;
            }
            else if (e.PropertyName == "ArchiveFolder")
            {
                if (!IOHelper.CheckFolderPermission(Options.ArchiveFolder))
                {
                    Options.ArchiveFolder = "";
                    return;
                }
                ButtonArchivePathText.Text = Options.ArchiveFolder == "" ? "Select folder..." : Options.ArchiveFolder;
            }
            else if (e.PropertyName == "GainPath")
            {
                if (!File.Exists(Options.GainPath))
                {
                    Options.GainPath = "";
                    return;
                }
                ButtonGainPathText.Text = Options.GainPath == "" ? "Select reference..." : Options.GainPath;
            }
            else if (e.PropertyName == "CTFWindow")
                CTFDisplay.Width = CTFDisplay.Height = Math.Min(1024, Options.CTFWindow);
            else if (e.PropertyName == "CTFPixelAngle")
                TransformPixelAngle.Angle = -(double) Options.CTFPixelAngle;
        }

        #region Button events

        private void ButtonInputPath_OnClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog Dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = Options.InputFolder
            };
            System.Windows.Forms.DialogResult Result = Dialog.ShowDialog();

            if (Result.ToString() == "OK")
            {
                if (!IOHelper.CheckFolderPermission(Dialog.SelectedPath))
                {
                    MessageBox.Show("Don't have permission to access the selected folder.");
                    return;
                }

                if (Dialog.SelectedPath[Dialog.SelectedPath.Length - 1] != '\\')
                    Dialog.SelectedPath += '\\';

                Options.InputFolder = Dialog.SelectedPath;
            }
        }

        private void ButtonInputExtension_OnClick(object sender, RoutedEventArgs e)
        {
            PopupInputExtension.IsOpen = true;
        }

        private void ButtonOutputPath_OnClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog Dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = Options.OutputFolder
            };
            System.Windows.Forms.DialogResult Result = Dialog.ShowDialog();
            
            if (Result.ToString() == "OK")
            {
                if (!IOHelper.CheckFolderPermission(Dialog.SelectedPath))
                {
                    MessageBox.Show("Don't have permission to access the selected folder.");
                    return;
                }

                if (Dialog.SelectedPath[Dialog.SelectedPath.Length - 1] != '\\')
                    Dialog.SelectedPath += '\\';

                Options.OutputFolder = Dialog.SelectedPath;
            }
        }

        private void ButtonOutputExtension_OnClick(object sender, RoutedEventArgs e)
        {
            PopupOutputExtension.IsOpen = true;
        }

        private void ButtonArchivePath_OnClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog Dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = Options.ArchiveFolder
            };
            System.Windows.Forms.DialogResult Result = Dialog.ShowDialog();

            if (Result.ToString() == "OK")
            {
                if (!IOHelper.CheckFolderPermission(Dialog.SelectedPath))
                {
                    MessageBox.Show("Don't have permission to access the selected folder.");
                    return;
                }

                if (Dialog.SelectedPath[Dialog.SelectedPath.Length - 1] != '\\')
                    Dialog.SelectedPath += '\\';

                Options.ArchiveFolder = Dialog.SelectedPath;
            }
        }

        private void ButtonArchiveOperation_OnClick(object sender, RoutedEventArgs e)
        {
            PopupArchiveOperation.IsOpen = true;
        }

        private void ButtonGainPath_OnClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog Dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Image Files|*.em;*.mrc",
                Multiselect = false
            };
            System.Windows.Forms.DialogResult Result = Dialog.ShowDialog();

            if (Result.ToString() == "OK")
            {
                Options.GainPath = Dialog.FileName;
                Options.CorrectGain = true;
            }
        }

        private void ButtonOptionsSave_OnClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog Dialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "Setting Files|*.settings"
            };
            System.Windows.Forms.DialogResult Result = Dialog.ShowDialog();
            if (Result == System.Windows.Forms.DialogResult.OK)
            {
                Options.Save(Dialog.FileName);
            }
        }

        private void ButtonOptionsLoad_OnClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog Dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Setting Files|*.settings",
                Multiselect = false
            };
            System.Windows.Forms.DialogResult Result = Dialog.ShowDialog();
            if (Result == System.Windows.Forms.DialogResult.OK)
            {
                Options.Load(Dialog.FileName);
            }
        }

        private void ButtonStartProcessing_OnClick(object sender, RoutedEventArgs e)
        {
            if (!IsProcessing)
            {
                foreach (var item in DisableWhenRunning)
                    item.IsEnabled = false;

                ButtonStartProcessing.Content = "STOP PROCESSING";
                IsProcessing = true;
            }
            else
            {
                foreach (var item in DisableWhenRunning)
                    item.IsEnabled = true;

                ButtonStartProcessing.Content = "START PROCESSING";
                IsProcessing = false;
            }
        }

        private void ButtonExportParticles_OnClick(object sender, RoutedEventArgs e)
        {
            
        }

        private void ButtonExportStatistics_OnClick(object sender, RoutedEventArgs e)
        {
            Options.DisplayedMovie?.ProcessCTF(true);
            //Options.DisplayedMovie?.ProcessShift();
        }

        #endregion

        #region Other events

        void AdjustInput()
        {
            ImageDiscoverer.ChangePath(Options.InputFolder, Options.InputExtension);
        }

        #endregion
    }
}
