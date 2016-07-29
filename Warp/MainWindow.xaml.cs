using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
using Warp.Headers;
using Warp.Stages;
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
                GridOptionsParticles,
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
                Console.WriteLine($"Free memory: {GPU.GetFreeMemory(i)} MB");
                Console.WriteLine($"Total memory: {GPU.GetTotalMemory(i)} MB");
            }

            Options.UpdateGPUStats();

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

                //Star Models = new Star("D:\\rado27\\Refine3D\\run1_ct5_it005_half1_model.star", "data_model_group_2");
                //Debug.WriteLine(Models.GetRow(0)[0]);

                /*Image Volume = StageDataLoad.LoadMap("F:\\carragher20s\\ref256.mrc", new int2(1, 1), 0, typeof (float));
                Image VolumePadded = Volume.AsPadded(new int3(512, 512, 512));
                VolumePadded.WriteMRC("d_padded.mrc");
                Volume.Dispose();
                VolumePadded.RemapToFT(true);
                Image VolumeFT = VolumePadded.AsFFT(true);
                VolumePadded.Dispose();

                Image VolumeProjFT = VolumeFT.AsProjections(new[] { new float3(Helper.ToRad * 0, Helper.ToRad * 0, Helper.ToRad * 0) }, new int2(256, 256), 2f);
                Image VolumeProj = VolumeProjFT.AsIFFT();
                VolumeProjFT.Dispose();
                VolumeProj.RemapFromFT();
                VolumeProj.WriteMRC("d_proj.mrc");
                VolumeProj.Dispose();*/

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
            else if (e.PropertyName == "DataStarPath")
            {
                if (!File.Exists(Options.DataStarPath))
                {
                    Options.DataStarPath = "";
                    return;
                }
                ButtonDataStarPathText.Text = Options.DataStarPath == "" ? "Select file..." : Options.DataStarPath;
            }
            else if (e.PropertyName == "ModelStarPath")
            {
                if (!File.Exists(Options.ModelStarPath))
                {
                    Options.ModelStarPath = "";
                    return;
                }
                ButtonModelStarPathText.Text = Options.ModelStarPath == "" ? "Select file..." : Options.ModelStarPath;
            }
            else if (e.PropertyName == "ReferencePath")
            {
                if (!File.Exists(Options.ReferencePath))
                {
                    Options.ReferencePath = "";
                    return;
                }
                ButtonReferencePathText.Text = Options.ReferencePath == "" ? "Select reference..." : Options.ReferencePath;
            }
            else if (e.PropertyName == "MaskPath")
            {
                if (!File.Exists(Options.MaskPath))
                {
                    Options.MaskPath = "";
                    return;
                }
                ButtonMaskPathText.Text = Options.MaskPath == "" ? "Select mask..." : Options.MaskPath;
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

                Thread ProcessThread = new Thread(() =>
                {
                    int MaxDevices = 999;
                    int UsedDevices = Math.Min(MaxDevices, GPU.GetDeviceCount());

                    Image[] ImageGain = new Image[UsedDevices];
                    if (!string.IsNullOrEmpty(Options.GainPath) && Options.CorrectGain && File.Exists(Options.GainPath))
                        for (int d = 0; d < UsedDevices; d++)
                            try
                            {
                                GPU.SetDevice(d);
                                ImageGain[d] = StageDataLoad.LoadMap(Options.GainPath,
                                                                     new int2(MainWindow.Options.InputDatWidth, MainWindow.Options.InputDatHeight),
                                                                     MainWindow.Options.InputDatOffset,
                                                                     ImageFormatsHelper.StringToType(MainWindow.Options.InputDatType));
                            }
                            catch
                            {
                                return;
                            }

                    Image[] VolRefFT = new Image[UsedDevices], VolMaskFT = new Image[UsedDevices];
                    if ((Options.ProcessCTF && Options.ProcessParticleCTF) || (Options.ProcessMovement && Options.ProcessParticleShift))
                        if (File.Exists(Options.ReferencePath) && File.Exists(Options.MaskPath))
                        {
                            for (int d = 0; d < UsedDevices; d++)
                            {
                                GPU.SetDevice(d);
                                {
                                    Image Volume = StageDataLoad.LoadMap(Options.ReferencePath, new int2(1, 1), 0, typeof (float));
                                    Image VolumePadded = Volume.AsPadded(Volume.Dims * Options.ProjectionOversample);
                                    Volume.Dispose();
                                    VolumePadded.RemapToFT(true);
                                    VolRefFT[d] = VolumePadded.AsFFT(true);
                                    VolumePadded.Dispose();
                                }
                                {
                                    Image Volume = StageDataLoad.LoadMap(Options.MaskPath, new int2(1, 1), 0, typeof (float));
                                    Image VolumePadded = Volume.AsPadded(Volume.Dims * Options.ProjectionOversample);
                                    Volume.Dispose();
                                    VolumePadded.RemapToFT(true);
                                    VolMaskFT[d] = VolumePadded.AsFFT(true);
                                    VolumePadded.Dispose();
                                }
                            }
                        }

                    Star ParticlesStar = null;
                    if (File.Exists(Options.DataStarPath))
                        ParticlesStar = new Star(Options.DataStarPath);

                    Queue<DeviceToken> Devices = new Queue<DeviceToken>();
                    for (int d = 0; d < UsedDevices; d++)
                        Devices.Enqueue(new DeviceToken(d));
                    for (int d = 0; d < UsedDevices; d++)
                        Devices.Enqueue(new DeviceToken(d));
                    int NTokens = Devices.Count;

                    DeviceToken[] IOSync = new DeviceToken[UsedDevices];
                    for (int d = 0; d < UsedDevices; d++)
                        IOSync[d] = new DeviceToken(d);

                    foreach (var Movie in Options.Movies)
                    {
                        if (!IsProcessing)
                            break;

                        if (Movie.Status != ProcessingStatus.Skip && Movie.Status != ProcessingStatus.Processed)
                        {
                            while (Devices.Count <= 0)
                                Thread.Sleep(20);

                            if (!IsProcessing)
                                break;

                            DeviceToken CurrentDevice;
                            lock (Devices)
                                CurrentDevice = Devices.Dequeue();

                            Thread DeviceThread = new Thread(() =>
                            {
                                GPU.SetDevice(CurrentDevice.ID);

                                MapHeader OriginalHeader = null;
                                Image OriginalStack = null;
                                decimal ScaleFactor = 1M / (decimal)Math.Pow(2, (double)Options.PostBinTimes);

                                lock (IOSync[CurrentDevice.ID])
                                    PrepareHeaderAndMap(Movie.Path, ImageGain[CurrentDevice.ID], ScaleFactor, out OriginalHeader, out OriginalStack);
                                /*OriginalHeader = MapHeader.ReadFromFile(Movie.Path,
                                                                    new int2(Options.InputDatWidth, Options.InputDatHeight),
                                                                    Options.InputDatOffset,
                                                                    ImageFormatsHelper.StringToType(Options.InputDatType));*/

                                //try
                                {
                                    if (Options.ProcessMovement)
                                    {
                                        if (!Options.ProcessParticleShift)
                                            Movie.ProcessShift(OriginalHeader, OriginalStack, ScaleFactor);
                                        else
                                            Movie.ProcessParticleShift(OriginalHeader,
                                                                       OriginalStack,
                                                                       ParticlesStar,
                                                                       VolRefFT[CurrentDevice.ID],
                                                                       VolMaskFT[CurrentDevice.ID],
                                                                       VolRefFT[CurrentDevice.ID].Dims.X / Options.ProjectionOversample,
                                                                       ScaleFactor);
                                    }
                                    if (Options.ProcessCTF)
                                    {
                                        if (!Options.ProcessParticleCTF)
                                            Movie.ProcessCTF(OriginalHeader, OriginalStack, true, ScaleFactor);
                                        else
                                            Movie.ProcessParticleCTF(OriginalHeader,
                                                                     OriginalStack,
                                                                     ParticlesStar,
                                                                     VolRefFT[CurrentDevice.ID],
                                                                     VolMaskFT[CurrentDevice.ID],
                                                                     VolRefFT[CurrentDevice.ID].Dims.X / Options.ProjectionOversample,
                                                                     ScaleFactor);
                                    }

                                    if (Options.PostAverage || Options.PostStack)
                                        Movie.CreateCorrected(OriginalHeader, OriginalStack);

                                    //Movie.PerformComparison(OriginalHeader, ParticlesStar, VolRefFT, VolMaskFT, ScaleFactor);

                                    Movie.Status = ProcessingStatus.Processed;
                                }
                                /*catch
                                {
                                    Movie.Status = ProcessingStatus.Unprocessed;
                                }*/

                                OriginalStack?.Dispose();

                                lock (Devices)
                                    Devices.Enqueue(CurrentDevice);
                            });

                            DeviceThread.Start();
                        }
                    }

                    while (Devices.Count != NTokens)
                        Thread.Sleep(20);

                    //ParticlesStar.Save("F:\\rado27\\20S_defocused_dataset_part1\\warpmaps2\\run1_it017_data_everything.star");
                    //MoviesStar.Save("D:\\rado27\\Refine3D\\run1_ct5_data_movies.star");

                    for (int d = 0; d < UsedDevices; d++)
                    {
                        ImageGain[d]?.Dispose();
                        VolRefFT[d]?.Dispose();
                        VolMaskFT[d]?.Dispose();
                    }
                });
                ProcessThread.Start();
            }
            else
            {
                foreach (var item in DisableWhenRunning)
                    item.IsEnabled = true;

                ButtonStartProcessing.Content = "START PROCESSING";
                IsProcessing = false;
            }
        }

        private void PrepareHeaderAndMap(string path, Image imageGain, decimal scaleFactor, out MapHeader header, out Image stack)
        {
            header = MapHeader.ReadFromFile(path,
                                            new int2(MainWindow.Options.InputDatWidth, MainWindow.Options.InputDatHeight),
                                            MainWindow.Options.InputDatOffset,
                                            ImageFormatsHelper.StringToType(MainWindow.Options.InputDatType));

            if (scaleFactor == 1M)
            {
                stack = StageDataLoad.LoadMap(path,
                                              new int2(MainWindow.Options.InputDatWidth, MainWindow.Options.InputDatHeight),
                                              MainWindow.Options.InputDatOffset,
                                              ImageFormatsHelper.StringToType(MainWindow.Options.InputDatType));

                if (imageGain != null)
                    stack.MultiplySlices(imageGain);
                stack.Xray(20f);
            }
            else
            {
                int3 ScaledDims = new int3((int)Math.Round(header.Dimensions.X * scaleFactor),
                                           (int)Math.Round(header.Dimensions.Y * scaleFactor),
                                           header.Dimensions.Z);
                header.Dimensions = ScaledDims;

                stack = new Image(ScaledDims);
                float[][] OriginalStackData = stack.GetHost(Intent.Write);

                //Parallel.For(0, ScaledDims.Z, new ParallelOptions {MaxDegreeOfParallelism = 4}, z =>
                for (int z = 0; z < ScaledDims.Z; z++)
                {
                    Image Layer = StageDataLoad.LoadMap(path,
                                                        new int2(MainWindow.Options.InputDatWidth, MainWindow.Options.InputDatHeight),
                                                        MainWindow.Options.InputDatOffset,
                                                        ImageFormatsHelper.StringToType(MainWindow.Options.InputDatType),
                                                        z);
                    //lock (OriginalStackData)
                    {
                        if (imageGain != null)
                            Layer.MultiplySlices(imageGain);
                        Layer.Xray(20f);

                        Image ScaledLayer = Layer.AsScaledMassive(new int2(ScaledDims));
                        Layer.Dispose();

                        OriginalStackData[z] = ScaledLayer.GetHost(Intent.Read)[0];
                        ScaledLayer.Dispose();
                    }
                }//);

                //stack.WriteMRC("d_stack.mrc");
            }
        }

        private void ButtonExportParticles_OnClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog Dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "STAR Files|*.star",
                Multiselect = false
            };
            System.Windows.Forms.DialogResult Result = Dialog.ShowDialog();
            if (Result == System.Windows.Forms.DialogResult.OK)
            {
                System.Windows.Forms.SaveFileDialog SaveDialog = new System.Windows.Forms.SaveFileDialog
                {
                    Filter = "STAR Files|*.star"
                };
                System.Windows.Forms.DialogResult SaveResult = SaveDialog.ShowDialog();
                if (SaveResult == System.Windows.Forms.DialogResult.OK)
                {
                    Thread ProcessThread = new Thread(() =>
                    {
                        Star TableIn = new Star(Dialog.FileName);
                        //if (TableIn.GetColumn("rlnCtfImage") == null)
                        //    TableIn.AddColumn("rlnCtfImage");

                        string[] ColumnNames = TableIn.GetColumn("rlnMicrographName");

                        string[] Excluded = Options.Movies.Where(m => m.Status == ProcessingStatus.Skip).Select(m => m.RootName).ToArray();
                        List<int> ForDelete = new List<int>();
                        for (int r = 0; r < TableIn.RowCount; r++)
                            for (int ex = 0; ex < Excluded.Length; ex++)
                                if (ColumnNames[r].Contains(Excluded[ex]))
                                    ForDelete.Add(r);
                        TableIn.RemoveRows(ForDelete.ToArray());

                        ColumnNames = TableIn.GetColumn("rlnMicrographName");
                        string[] ColumnCoordsX = TableIn.GetColumn("rlnCoordinateX");
                        string[] ColumnCoordsY = TableIn.GetColumn("rlnCoordinateY");

                        Star TableOut = new Star(TableIn.GetColumnNames());

                        Image ImageGain = null;
                        if (!string.IsNullOrEmpty(Options.GainPath) && Options.CorrectGain)
                            try
                            {
                                ImageGain = StageDataLoad.LoadMap(Options.GainPath,
                                                                  new int2(MainWindow.Options.InputDatWidth, MainWindow.Options.InputDatHeight),
                                                                  MainWindow.Options.InputDatOffset,
                                                                  ImageFormatsHelper.StringToType(MainWindow.Options.InputDatType));
                            }
                            catch
                            {
                                return;
                            }

                        foreach (var movie in Options.Movies)
                            if (movie.DoProcess)
                            {
                                MapHeader OriginalHeader = null;
                                Image OriginalStack = null;
                                decimal ScaleFactor = 1M / (decimal)Math.Pow(2, (double)Options.PostBinTimes);

                                //PrepareHeaderAndMap(movie.Path, ImageGain, ScaleFactor, out OriginalHeader, out OriginalStack);

                                //OriginalStack.WriteMRC("d_stack.mrc");
                                movie.UpdateStarDefocus(TableIn, ColumnNames, ColumnCoordsX, ColumnCoordsY);
                                //movie.ExportParticles(TableIn, TableOut, OriginalHeader, OriginalStack, Options.ExportParticleSize, Options.ExportParticleRadius, ScaleFactor);

                                OriginalStack?.Dispose();
                                //Debug.WriteLine(movie.Path);
                                //TableIn.Save(SaveDialog.FileName);
                            }

                        TableIn.Save(SaveDialog.FileName);

                        ImageGain?.Dispose();
                    });
                    ProcessThread.Start();
                }
            }
        }

        private void ButtonPolishParticles_OnClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog Dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "STAR Files|*.star",
                Multiselect = false
            };
            System.Windows.Forms.DialogResult Result = Dialog.ShowDialog();
            if (Result == System.Windows.Forms.DialogResult.OK)
            {
                System.Windows.Forms.SaveFileDialog SaveDialog = new System.Windows.Forms.SaveFileDialog
                {
                    Filter = "STAR Files|*.star"
                };
                System.Windows.Forms.DialogResult SaveResult = SaveDialog.ShowDialog();
                if (SaveResult == System.Windows.Forms.DialogResult.OK)
                {
                    Thread ProcessThread = new Thread(() =>
                    {
                        Star TableIn = new Star(Dialog.FileName);
                        if (TableIn.GetColumn("rlnCtfImage") == null)
                            TableIn.AddColumn("rlnCtfImage");

                        string[] ColumnNames = TableIn.GetColumn("rlnMicrographName");
                        string[] ColumnCoordsX = TableIn.GetColumn("rlnCoordinateX");
                        string[] ColumnCoordsY = TableIn.GetColumn("rlnCoordinateY");

                        Star TableOut = new Star(TableIn.GetColumnNames());

                        Image ImageGain = null;
                        if (!string.IsNullOrEmpty(Options.GainPath) && Options.CorrectGain)
                            try
                            {
                                ImageGain = StageDataLoad.LoadMap(Options.GainPath,
                                                                  new int2(MainWindow.Options.InputDatWidth, MainWindow.Options.InputDatHeight),
                                                                  MainWindow.Options.InputDatOffset,
                                                                  ImageFormatsHelper.StringToType(MainWindow.Options.InputDatType));
                            }
                            catch
                            {
                                return;
                            }

                        foreach (var movie in Options.Movies)
                            if (movie.DoProcess)
                            {
                                MapHeader OriginalHeader = null;
                                Image OriginalStack = null;
                                decimal ScaleFactor = 1M / (decimal)Math.Pow(2, (double)Options.PostBinTimes);

                                PrepareHeaderAndMap(movie.Path, ImageGain, ScaleFactor, out OriginalHeader, out OriginalStack);

                                //OriginalStack.WriteMRC("d_stack.mrc");
                                movie.UpdateStarDefocus(TableIn, ColumnNames, ColumnCoordsX, ColumnCoordsY);
                                movie.ExportParticlesMovie(TableIn, TableOut, OriginalHeader, OriginalStack, Options.ExportParticleSize, Options.ExportParticleRadius, ScaleFactor);

                                OriginalStack?.Dispose();
                                //Debug.WriteLine(movie.Path);
                                TableOut.Save(SaveDialog.FileName);
                            }

                        TableOut.Save(SaveDialog.FileName);

                        ImageGain?.Dispose();
                    });
                    ProcessThread.Start();
                }
            }
        }

        private void ButtonExportList_OnClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.SaveFileDialog SaveDialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "STAR Files|*.star"
            };
            System.Windows.Forms.DialogResult SaveResult = SaveDialog.ShowDialog();
            if (SaveResult == System.Windows.Forms.DialogResult.OK)
            {
                using (TextWriter Writer = File.CreateText(SaveDialog.FileName))
                {
                    Writer.WriteLine("");
                    Writer.WriteLine("data_");
                    Writer.WriteLine("");
                    Writer.WriteLine("loop_");
                    Writer.WriteLine("_rlnMicrographName #1");
                    Writer.WriteLine("_rlnDefocusU #2");
                    Writer.WriteLine("_rlnDefocusV #3");
                    Writer.WriteLine("_rlnDefocusAngle #4");
                    Writer.WriteLine("_rlnPhaseShift #5");
                    Writer.WriteLine("_rlnVoltage #6");
                    Writer.WriteLine("_rlnSphericalAberration #7");
                    Writer.WriteLine("_rlnAmplitudeContrast #8");
                    Writer.WriteLine("_rlnMagnification #9");
                    Writer.WriteLine("_rlnDetectorPixelSize #10");
                    Writer.WriteLine("_rlnCtfFigureOfMerit #11");
                    Writer.WriteLine("_rlnCtfImage #12");

                    foreach (Movie movie in Options.Movies)
                    {
                        if (movie.Status == ProcessingStatus.Skip)
                            continue;

                        List<string> Values = new List<string>();

                        Values.Add("average/" + movie.RootName + ".mrc");
                        Values.Add(((movie.CTF.Defocus + movie.CTF.DefocusDelta / 2M) * 1e4M).ToString(CultureInfo.InvariantCulture));
                        Values.Add(((movie.CTF.Defocus - movie.CTF.DefocusDelta / 2M) * 1e4M).ToString(CultureInfo.InvariantCulture));
                        Values.Add((movie.CTF.DefocusAngle).ToString(CultureInfo.InvariantCulture));
                        Values.Add((movie.CTF.PhaseShift * 180M).ToString(CultureInfo.InvariantCulture));
                        Values.Add(movie.CTF.Voltage.ToString(CultureInfo.InvariantCulture));
                        Values.Add(movie.CTF.Cs.ToString(CultureInfo.InvariantCulture));
                        Values.Add(movie.CTF.Amplitude.ToString(CultureInfo.InvariantCulture));
                        Values.Add((Options.CTFDetectorPixel * 10000M / movie.CTF.PixelSize).ToString(CultureInfo.InvariantCulture));
                        Values.Add(Options.CTFDetectorPixel.ToString(CultureInfo.InvariantCulture));
                        Values.Add("1");
                        Values.Add("spectrum/" + movie.RootName + ".mrc");

                        Writer.WriteLine(string.Join("  ", Values));
                    }
                }
            }
        }

        private void ButtonExportStatistics_OnClick(object sender, RoutedEventArgs e)
        {
            using (TextWriter Writer = new StreamWriter(File.Create("D:\\rubisco\\series\\defoci.txt")))
                foreach (Movie movie in Options.Movies)
                    Writer.WriteLine(string.Join("\t", movie.GridCTF.FlatValues));
        }

        #endregion

        #region Other events

        void AdjustInput()
        {
            ImageDiscoverer.ChangePath(Options.InputFolder, Options.InputExtension);
        }

        #endregion

        private void ButtonDataStarPath_OnClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog Dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "*_data.star Files|*_data.star",
                Multiselect = false
            };
            System.Windows.Forms.DialogResult Result = Dialog.ShowDialog();

            if (Result.ToString() == "OK")
            {
                Options.DataStarPath = Dialog.FileName;
            }
        }

        private void ButtonModelStarPath_OnClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog Dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "*_model.star Files|*_model.star",
                Multiselect = false
            };
            System.Windows.Forms.DialogResult Result = Dialog.ShowDialog();

            if (Result.ToString() == "OK")
            {
                Options.ModelStarPath = Dialog.FileName;
            }
        }

        private void ButtonReferencePath_OnClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog Dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "3D Map Files|*.mrc;*.em;*.tif",
                Multiselect = false
            };
            System.Windows.Forms.DialogResult Result = Dialog.ShowDialog();

            if (Result.ToString() == "OK")
            {
                Options.ReferencePath = Dialog.FileName;
            }
        }

        private void ButtonMaskPath_OnClick(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog Dialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "3D Map Files|*.mrc;*.em;*.tif",
                Multiselect = false
            };
            System.Windows.Forms.DialogResult Result = Dialog.ShowDialog();

            if (Result.ToString() == "OK")
            {
                Options.MaskPath = Dialog.FileName;
            }
        }
    }
}
