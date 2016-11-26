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
using Accord.Math.Optimization;
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

            GPU.MemoryChanged += () => Options.UpdateGPUStats();

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
            GPU.SetDevice(0);

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

                Matrix3 A = new Matrix3(1, 2, 3, 4, 5, 6, 7, 8, 9);
                Matrix3 B = new Matrix3(11, 12, 13, 14, 15, 16, 17, 18, 19);
                Matrix3 C = A * B;

                // Euler matrix
                {
                    Matrix3 E = Matrix3.Euler(0 * Helper.ToRad, 20 * Helper.ToRad, 0 * Helper.ToRad);
                    float3 EE = Matrix3.EulerFromMatrix(E.Transposed()) * Helper.ToDeg;

                    float3 Transformed = E * new float3(1, 0, 0);
                    Transformed.Y = 0;
                }

                //float3[] HealpixAngles = Helper.GetHealpixAngles(3, "D4");

                // Deconvolve reconstructions using a separate CTF
                //{
                //    for (int i = 1; i <= 24; i++)
                //    {
                //        Image Map = StageDataLoad.LoadMap($"F:\\stefanribo\\vlion\\warped_{i}.mrc", new int2(1, 1), 0, typeof(float));
                //        Image MapFT = Map.AsFFT(true);
                //        Map.Dispose();

                //        Image CTF = StageDataLoad.LoadMap($"F:\\stefanribo\\vlion\\warped_ctf_{i}.mrc", new int2(1, 1), 0, typeof(float));
                //        foreach (var slice in CTF.GetHost(Intent.ReadWrite))
                //            for (int s = 0; s < slice.Length; s++)
                //                slice[s] = Math.Max(1e-3f, slice[s]);

                //        MapFT.Divide(CTF);
                //        Map = MapFT.AsIFFT(true);
                //        MapFT.Dispose();

                //        Map.WriteMRC($"F:\\stefanribo\\vlion\\warped_deconv_{i}.mrc");
                //        Map.Dispose();
                //    }
                //}

                //{
                //    Image SumFT = new Image(new int3(220, 220, 220), true, true);
                //    Image SumWeights = new Image(new int3(220, 220, 220), true);

                //    int read = 0;
                //    foreach (var tomoPath in Directory.EnumerateFiles("F:\\stefanribo\\oridata\\particles", "tomo*.mrc"))
                //    {
                //        FileInfo Info = new FileInfo(tomoPath);

                //        Image Tomo = StageDataLoad.LoadMap(tomoPath, new int2(1, 1), 0, typeof(float));
                //        Image TomoFT = Tomo.AsFFT(true);
                //        Tomo.Dispose();

                //        Image TomoWeights = StageDataLoad.LoadMap("F:\\stefanribo\\oridata\\particlectf\\" + Info.Name, new int2(1, 1), 0, typeof(float));

                //        TomoFT.Multiply(TomoWeights);
                //        TomoWeights.Multiply(TomoWeights);

                //        SumFT.Add(TomoFT);
                //        SumWeights.Add(TomoWeights);

                //        TomoFT.Dispose();
                //        TomoWeights.Dispose();

                //        Debug.WriteLine(read++);
                //    }

                //    foreach (var slice in SumWeights.GetHost(Intent.ReadWrite))
                //    {
                //        for (int i = 0; i < slice.Length; i++)
                //        {
                //            slice[i] = Math.Max(1e-3f, slice[i]);
                //        }
                //    }

                //    SumFT.Divide(SumWeights);
                //    Image Sum = SumFT.AsIFFT(true);
                //    Sum.WriteMRC("F:\\stefanribo\\oridata\\particles\\weightedaverage.mrc");

                //    SumFT.Dispose();
                //    SumWeights.Dispose();
                //    Sum.Dispose();
                //}

                //{
                //    Image Subtrahend = StageDataLoad.LoadMap("E:\\martinsried\\stefan\\membranebound\\vlion\\relion_subtrahend.mrc", new int2(1, 1), 0, typeof(float));
                //    Image SubtrahendFT = Subtrahend.AsFFT(true);

                //    int read = 0;
                //    foreach (var tomoPath in Directory.EnumerateFiles("E:\\martinsried\\stefan\\membranebound\\oridata\\particles", "tomo*.mrc"))
                //    {
                //        FileInfo Info = new FileInfo(tomoPath);

                //        Image Tomo = StageDataLoad.LoadMap(tomoPath, new int2(1, 1), 0, typeof(float));
                //        Image TomoFT = Tomo.AsFFT(true);
                //        Tomo.Dispose();

                //        Image TomoWeights = StageDataLoad.LoadMap("E:\\martinsried\\stefan\\membranebound\\oridata\\particlectf\\" + Info.Name, new int2(1, 1), 0, typeof(float));

                //        Image SubtrahendFTMult = new Image(SubtrahendFT.GetDevice(Intent.Read), SubtrahendFT.Dims, true, true);
                //        SubtrahendFTMult.Multiply(TomoWeights);

                //        TomoFT.Subtract(SubtrahendFTMult);
                //        Tomo = TomoFT.AsIFFT(true);

                //        Tomo.WriteMRC("D:\\stefanribo\\particles\\" + Info.Name);

                //        Tomo.Dispose();
                //        TomoFT.Dispose();
                //        SubtrahendFTMult.Dispose();
                //        TomoWeights.Dispose();

                //        Debug.WriteLine(read++);
                //    }
                //}

                //{
                //    Image SubtRef1 = StageDataLoad.LoadMap("E:\\martinsried\\stefan\\membranebound\\vlion\\warp_subtrahend.mrc", new int2(1, 1), 0, typeof(float));
                //    Projector Subt = new Projector(SubtRef1, 2);
                //    SubtRef1.Dispose();

                //    Image ProjFT = Subt.Project(new int2(220, 220), new[] { new float3(0, 0, 0) }, 110);
                //    Image Proj = ProjFT.AsIFFT();
                //    Proj.RemapFromFT();

                //    Proj.WriteMRC("d_testproj.mrc");
                //}

                // Projector
                /*{
                    Image MapForProjector = StageDataLoad.LoadMap("E:\\youwei\\run36_half1_class001_unfil.mrc", new int2(1, 1), 0, typeof (float));
                    Projector Proj = new Projector(MapForProjector, 2);
                    Image Projected = Proj.Project(new int2(240, 240), new[] { new float3(0, 0, 0) }, 120);
                    Projected = Projected.AsIFFT();
                    Projected.RemapFromFT();
                    Projected.WriteMRC("d_projected.mrc");
                }*/

                // Backprojector
                /*{
                    Image Dot = new Image(new int3(32, 32, 360));
                    for (int a = 0; a < 360; a++)
                        Dot.GetHost(Intent.Write)[a][0] = 1;
                    Dot = Dot.AsFFT();
                    Dot.AsAmplitudes().WriteMRC("d_dot.mrc");

                    Image DotWeights = new Image(new int3(32, 32, 360), true);
                    for (int a = 0; a < 360; a++)
                        for (int i = 0; i < DotWeights.ElementsSliceReal; i++)
                            DotWeights.GetHost(Intent.Write)[a][i] = 1;

                    float3[] Angles = new float3[360];
                    for (int a = 0; a < 360; a++)
                        Angles[a] = new float3(0, a * Helper.ToRad * 0.05f, 0);

                    Projector Proj = new Projector(new int3(32, 32, 32), 2);
                    Proj.BackProject(Dot, DotWeights, Angles);

                    Proj.Weights.WriteMRC("d_weights.mrc");
                    //Image Re = Proj.Data.AsImaginary();
                    //Re.WriteMRC("d_projdata.mrc");

                    Image Rec = Proj.Reconstruct(true);
                    Rec.WriteMRC("d_rec.mrc");
                }*/

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
                    //for (int d = 0; d < UsedDevices; d++)
                    //    Devices.Enqueue(new DeviceToken(d));
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
                                    
                                    //((TiltSeries)Movie).Reconstruct(OriginalStack, 128, 3.42f * 4 * 2, new int3(3712, 3712, 1400));

                                    //Image Reference = StageDataLoad.LoadMap("F:\\badaben\\ref.mrc", new int2(1, 1), 0, typeof(float));
                                    //((TiltSeries)Movie).Correlate(OriginalStack, Reference, 128, 80, 400, new int3(3712, 3712, 1400), 2, "C1");
                                    //Reference.Dispose();

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

                        int MaxDevices = 999;
                        int UsedDevices = Math.Min(MaxDevices, GPU.GetDeviceCount());

                        Queue<DeviceToken> Devices = new Queue<DeviceToken>();
                        for (int d = 0; d < UsedDevices; d++)
                            Devices.Enqueue(new DeviceToken(d));
                        //for (int d = 0; d < UsedDevices; d++)
                        //    Devices.Enqueue(new DeviceToken(d));
                        int NTokens = Devices.Count;

                        DeviceToken[] IOSync = new DeviceToken[UsedDevices];
                        for (int d = 0; d < UsedDevices; d++)
                            IOSync[d] = new DeviceToken(d);

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

                        //Dictionary<int, Projector>[] DeviceReferences = new Dictionary<int, Projector>[GPU.GetDeviceCount()];
                        //Dictionary<int, Projector>[] DeviceReconstructions = new Dictionary<int, Projector>[GPU.GetDeviceCount()];
                        //Dictionary<int, Projector>[] DeviceCTFReconstructions = new Dictionary<int, Projector>[GPU.GetDeviceCount()];
                        //for (int d = 0; d < GPU.GetDeviceCount(); d++)
                        //{
                        //    GPU.SetDevice(d);

                        //    Dictionary<int, Projector> References = new Dictionary<int, Projector>();
                        //    {
                        //        Image Ref1 = StageDataLoad.LoadMap("F:\\badaben\\vlion123\\warp_ref1.mrc", new int2(1, 1), 0, typeof(float));
                        //        Image Ref2 = StageDataLoad.LoadMap("F:\\badaben\\vlion123\\warp_ref2.mrc", new int2(1, 1), 0, typeof(float));
                        //        Image Ref3 = StageDataLoad.LoadMap("F:\\badaben\\vlion123\\warp_ref3.mrc", new int2(1, 1), 0, typeof(float));
                        //        Image Ref4 = StageDataLoad.LoadMap("F:\\badaben\\vlion123\\warp_ref4.mrc", new int2(1, 1), 0, typeof(float));
                        //        //Image Ref5 = StageDataLoad.LoadMap("F:\\badaben\\vlion12\\warp_ref5.mrc", new int2(1, 1), 0, typeof(float));
                        //        //Image Ref6 = StageDataLoad.LoadMap("F:\\badaben\\vlion12\\warp_ref6.mrc", new int2(1, 1), 0, typeof(float));
                        //        //Image Ref1 = StageDataLoad.LoadMap("F:\\badaben\\chloro\\warp_ref1.mrc", new int2(1, 1), 0, typeof(float));
                        //        //Image Ref2 = StageDataLoad.LoadMap("F:\\badaben\\chloro\\warp_ref2.mrc", new int2(1, 1), 0, typeof(float));
                        //        //Image Ref3 = StageDataLoad.LoadMap("F:\\stefanribo\\vlion\\mini_warp_ref3.mrc", new int2(1, 1), 0, typeof(float));
                        //        //Image Ref4 = StageDataLoad.LoadMap("F:\\stefanribo\\vlion\\mini_warp_ref4.mrc", new int2(1, 1), 0, typeof(float));
                        //        References.Add(1, new Projector(Ref1, 2));
                        //        References.Add(2, new Projector(Ref2, 2));
                        //        References.Add(3, new Projector(Ref3, 2));
                        //        References.Add(4, new Projector(Ref4, 2));
                        //        //References.Add(5, new Projector(Ref5, 2));
                        //        //References.Add(6, new Projector(Ref6, 2));

                        //        //References.Add(3, new Projector(Ref3, 2));
                        //        Ref1.Dispose();
                        //        Ref2.Dispose();
                        //        Ref3.Dispose();
                        //        Ref4.Dispose();
                        //        //Ref5.Dispose();
                        //        //Ref6.Dispose();
                        //    }
                        //    DeviceReferences[d] = References;

                        //    Dictionary<int, Projector> Reconstructions = new Dictionary<int, Projector>();
                        //    foreach (var reference in References)
                        //    {
                        //        Reconstructions.Add(reference.Key, new Projector(reference.Value.Dims, reference.Value.Oversampling));
                        //        Reconstructions[reference.Key].FreeDevice();
                        //    }
                        //    DeviceReconstructions[d] = Reconstructions;

                        //    Dictionary<int, Projector> CTFReconstructions = new Dictionary<int, Projector>();
                        //    foreach (var reference in References)
                        //    {
                        //        CTFReconstructions.Add(reference.Key, new Projector(reference.Value.Dims, reference.Value.Oversampling));
                        //        CTFReconstructions[reference.Key].FreeDevice();
                        //    }
                        //    DeviceCTFReconstructions[d] = CTFReconstructions;
                        //}

                        int NTilts = (int)MathHelper.Max(Options.Movies.Select(m => (float)((TiltSeries)m).NTilts));
                        Dictionary<int, Projector[]> PerAngleReconstructions = new Dictionary<int, Projector[]>();
                        Dictionary<int, Projector[]> PerAngleWeightReconstructions = new Dictionary<int, Projector[]>();
                        //{
                        //    int[] ColumnSubset = TableIn.GetColumn("rlnRandomSubset").Select(s => int.Parse(s)).ToArray();
                        //    List<int> SubsetIDs = new List<int>();
                        //    foreach (var subset in ColumnSubset)
                        //        if (!SubsetIDs.Contains(subset))
                        //            SubsetIDs.Add(subset);
                        //    SubsetIDs.Sort();
                        //    SubsetIDs.Remove(1);
                        //    SubsetIDs.Remove(2);

                        //    int Size = Options.ExportParticleSize;

                        //    //foreach (var subsetID in SubsetIDs)
                        //    //{
                        //    //    PerAngleReconstructions.Add(subsetID, new Projector[NTilts]);
                        //    //    PerAngleWeightReconstructions.Add(subsetID, new Projector[NTilts]);

                        //    //    for (int t = 0; t < NTilts; t++)
                        //    //    {
                        //    //        PerAngleReconstructions[subsetID][t] = new Projector(new int3(Size, Size, Size), 2);
                        //    //        PerAngleReconstructions[subsetID][t].FreeDevice();
                        //    //        PerAngleWeightReconstructions[subsetID][t] = new Projector(new int3(Size, Size, Size), 2);
                        //    //        PerAngleWeightReconstructions[subsetID][t].FreeDevice();
                        //    //    }
                        //    //}
                        //}

                        foreach (var movie in Options.Movies)
                            if (movie.DoProcess)
                            {
                                //if (((TiltSeries)movie).GlobalBfactor < -200)
                                //    continue;

                                while (Devices.Count <= 0)
                                    Thread.Sleep(20);

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
                                        PrepareHeaderAndMap(movie.Path, ImageGain[CurrentDevice.ID], ScaleFactor, out OriginalHeader, out OriginalStack);

                                    if (movie.GetType() == typeof (Movie))
                                    {
                                        movie.UpdateStarDefocus(TableIn, ColumnNames, ColumnCoordsX, ColumnCoordsY);
                                        //movie.ExportParticles(TableIn, TableOut, OriginalHeader, OriginalStack, Options.ExportParticleSize, Options.ExportParticleRadius, ScaleFactor);
                                    }
                                    else if (movie.GetType() == typeof (TiltSeries))
                                    {
                                        //((TiltSeries)movie).ExportSubtomos(TableIn, OriginalStack, Options.ExportParticleSize, new int3(928, 928, 300));
                                        //((TiltSeries)movie).ExportSubtomos(TableIn, OriginalStack, Options.ExportParticleSize, new int3(3712, 3712, 1400));

                                        //((TiltSeries)movie).Reconstruct(OriginalStack, 128, 3.42f * 4 * 2, new int3(3712, 3712, 1400));

                                        /*for (int refinement = 0; refinement < 20; refinement++)
                                        {*/
                                            //OriginalStack.FreeDevice();
                                            //((TiltSeries)movie).PerformOptimizationStep(TableIn, OriginalStack, Options.ExportParticleSize, new int3(3712, 3712, 1400), DeviceReferences[CurrentDevice.ID], 22f, DeviceReconstructions[0], DeviceCTFReconstructions[0]);
                                            //((TiltSeries)movie).RealspaceRefineGlobal(TableIn, OriginalStack, Options.ExportParticleSize, new int3(3838, 3710, 1200), References, 30, 2, "D4", Reconstructions);


                                            //Image Simulated = ((TiltSeries)movie).SimulateTiltSeries(TableIn, OriginalStack.Dims, Options.ExportParticleSize, new int3(3712, 3712, 1200), References, 15);
                                            //Simulated.WriteMRC("d_simulatedseries.mrc");

                                            //((TiltSeries)movie).AlignTiltMovies(TableIn, OriginalStack.Dims, Options.ExportParticleSize, new int3(3712, 3712, 1200), References, 100);


                                            /*TableIn.Save(SaveDialog.FileName + $".it{refinement:D2}.star");
                                        }*/

                                        //((TiltSeries)movie).ExportSubtomos(TableIn, OriginalStack, Options.ExportParticleSize, new int3(3712, 3712, 1400));

                                        //Image Reference = StageDataLoad.LoadMap("F:\\chloroplastribo\\vlion\\warp_ref3.mrc", new int2(1, 1), 0, typeof (float));
                                        //((TiltSeries)movie).Correlate(OriginalStack, Reference, 128, 40, 400, new int3(3712, 3712, 1400), 2, "C1");
                                        //Reference.Dispose();

                                        //GPU.SetDevice(0);
                                        //((TiltSeries)movie).MakePerTomogramReconstructions(TableIn, OriginalStack, Options.ExportParticleSize, new int3(3712, 3712, 1400));
                                        //((TiltSeries)movie).AddToPerAngleReconstructions(TableIn, OriginalStack, Options.ExportParticleSize, new int3(3710, 3710, 1400), PerAngleReconstructions, PerAngleWeightReconstructions);
                                    }

                                    OriginalStack?.Dispose();
                                    //Debug.WriteLine(movie.Path);
                                    //TableIn.Save(SaveDialog.FileName);

                                    lock (Devices)
                                        Devices.Enqueue(CurrentDevice);

                                    Debug.WriteLine("Done: " + movie.RootName);
                                });

                                DeviceThread.Start();
                            }

                        while (Devices.Count != NTokens)
                            Thread.Sleep(20);

                        for (int d = 0; d < UsedDevices; d++)
                        {
                            ImageGain[d]?.Dispose();
                        }

                        //for (int d = 0; d < DeviceReferences.Length; d++)
                        //{
                        //    GPU.SetDevice(d);
                        //    foreach (var reconstruction in DeviceReconstructions[d])
                        //    {
                        //        if (d == 0)
                        //        {
                        //            Image ReconstructedMap = reconstruction.Value.Reconstruct(false);
                        //            //ReconstructedMap.WriteMRC($"F:\\chloroplastribo\\vlion\\warped_{reconstruction.Key}_nodeconv.mrc");

                        //            Image ReconstructedCTF = DeviceCTFReconstructions[d][reconstruction.Key].Reconstruct(true);

                        //            Image ReconstructedMapFT = ReconstructedMap.AsFFT(true);
                        //            ReconstructedMap.Dispose();

                        //            int Dim = ReconstructedMap.Dims.Y;
                        //            int DimFT = Dim / 2 + 1;
                        //            int R2 = Dim / 2 - 2;
                        //            R2 *= R2;
                        //            foreach (var slice in ReconstructedCTF.GetHost(Intent.ReadWrite))
                        //            {
                        //                for (int y = 0; y < Dim; y++)
                        //                {
                        //                    int yy = y < Dim / 2 + 1 ? y : y - Dim;
                        //                    yy *= yy;

                        //                    for (int x = 0; x < DimFT; x++)
                        //                    {
                        //                        int xx = x * x;

                        //                        slice[y * DimFT + x] = xx + yy < R2 ? Math.Max(1e-2f, slice[y * DimFT + x]) : 1f;
                        //                    }
                        //                }
                        //            }

                        //            ReconstructedMapFT.Divide(ReconstructedCTF);
                        //            ReconstructedMap = ReconstructedMapFT.AsIFFT(true);
                        //            ReconstructedMapFT.Dispose();

                        //            //GPU.SphereMask(ReconstructedMap.GetDevice(Intent.Read),
                        //            //               ReconstructedMap.GetDevice(Intent.Write),
                        //            //               ReconstructedMap.Dims,
                        //            //               (float)(ReconstructedMap.Dims.X / 2 - 8),
                        //            //               8,
                        //            //               1);

                        //            ReconstructedMap.WriteMRC($"F:\\badaben\\vlion123\\warped_{reconstruction.Key}.mrc");
                        //            ReconstructedMap.Dispose();
                        //        }

                        //        reconstruction.Value.Dispose();
                        //        DeviceReferences[d][reconstruction.Key].Dispose();
                        //        DeviceCTFReconstructions[d][reconstruction.Key].Dispose();
                        //    }
                        //}

                        //string WeightOptimizationDir = ((TiltSeries)Options.Movies[0]).WeightOptimizationDir;
                        //foreach (var subset in PerAngleReconstructions)
                        //{
                        //    for (int t = 0; t < NTilts; t++)
                        //    {
                        //        Image Reconstruction = PerAngleReconstructions[subset.Key][t].Reconstruct(false);
                        //        PerAngleReconstructions[subset.Key][t].Dispose();
                        //        Reconstruction.WriteMRC(WeightOptimizationDir + $"subset{subset.Key}_tilt{t.ToString("D3")}.mrc");
                        //        Reconstruction.Dispose();

                        //        foreach (var slice in PerAngleWeightReconstructions[subset.Key][t].Weights.GetHost(Intent.ReadWrite))
                        //            for (int i = 0; i < slice.Length; i++)
                        //                slice[i] = Math.Min(1, slice[i]);
                        //        Image WeightReconstruction = PerAngleWeightReconstructions[subset.Key][t].Reconstruct(true);
                        //        PerAngleWeightReconstructions[subset.Key][t].Dispose();
                        //        WeightReconstruction.WriteMRC(WeightOptimizationDir + $"subset{subset.Key}_tilt{t.ToString("D3")}.weight.mrc");
                        //        WeightReconstruction.Dispose();
                        //    }
                        //}

                        TableIn.Save(SaveDialog.FileName);
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
            //using (TextWriter Writer = new StreamWriter(File.Create("D:\\rubisco\\series\\defoci.txt")))
            //    foreach (Movie movie in Options.Movies)
            //        Writer.WriteLine(string.Join("\t", movie.GridCTF.FlatValues));

            OptimizePerTomoWeights();
        }

        private void OptimizePerTomoWeights()
        {
            if (!Options.Movies.Any(m => m.GetType() == typeof (TiltSeries)))
                return;

            Image Mask1 = StageDataLoad.LoadMap("F:\\chloroplastribo\\vlion\\mask_post.mrc", new int2(1, 1), 0, typeof (float));
            //Image Mask2 = StageDataLoad.LoadMap("F:\\badaben\\vlion\\mask_C3_post.mrc", new int2(1, 1), 0, typeof(float));
            //Image Mask3 = StageDataLoad.LoadMap("F:\\badaben\\vlion\\mask_C4_post.mrc", new int2(1, 1), 0, typeof(float));
            List<Image> SubsetMasks = new List<Image> { Mask1, Mask1 };

            int3 Dims = Mask1.Dims;
            List<WeightOptContainer> Reconstructions = new List<WeightOptContainer>();
            Dictionary<TiltSeries, int> SeriesIndices = new Dictionary<TiltSeries, int>();

            foreach (Movie movie in Options.Movies)
            {
                if (!movie.DoProcess)
                    continue;

                TiltSeries Series = (TiltSeries)movie;
                string[] FileNames = Directory.EnumerateFiles(Series.WeightOptimizationDir, Series.RootName + "_subset*.mrc").Where(v => v.Contains("subset1") || v.Contains("subset2")).Select(v => new FileInfo(v).Name).ToArray();
                if (FileNames.Length == 0)
                    continue;

                string[] MapNames = FileNames.Where(v => !v.Contains(".weight.mrc")).ToArray();
                string[] WeightNames = FileNames.Where(v => v.Contains(".weight.mrc")).ToArray();
                if (MapNames.Length != WeightNames.Length)
                    throw new Exception("Number of reconstructions and weights does not match!");

                string[] MapSuffixes = MapNames.Select(v => v.Substring(Series.RootName.Length)).ToArray();
                int[] MapSubsets = MapSuffixes.Select(v =>
                {
                    string S = v.Substring(v.IndexOf("subset") + "subset".Length);
                    return int.Parse(S.Substring(0, S.IndexOf(".mrc"))) - 1;
                }).ToArray();

                SeriesIndices.Add(Series, SeriesIndices.Count);

                for (int i = 0; i < MapNames.Length; i++)
                {
                    Image Map = StageDataLoad.LoadMap(Series.WeightOptimizationDir + MapNames[i], new int2(1, 1), 0, typeof (float));
                    Image MapFT = Map.AsFFT(true);
                    float[] MapData = MapFT.GetHostContinuousCopy();
                    Map.Dispose();
                    MapFT.Dispose();

                    Image Weights = StageDataLoad.LoadMap(Series.WeightOptimizationDir + WeightNames[i], new int2(1, 1), 0, typeof (float));
                    float[] WeightsData = Weights.GetHostContinuousCopy();
                    Weights.Dispose();

                    Reconstructions.Add(new WeightOptContainer(SeriesIndices[Series], MapSubsets[i], MapData, WeightsData, 0, 0));
                }

                //break;
            }

            float[][] PackedRecFT = new float[SeriesIndices.Count][];
            float[][] PackedRecWeights = new float[SeriesIndices.Count][];
            foreach (var s in SeriesIndices)
            {
                WeightOptContainer[] SeriesRecs = Reconstructions.Where(r => r.SeriesID == s.Value).ToArray();
                PackedRecFT[s.Value] = new float[SeriesRecs.Length * SeriesRecs[0].DataFT.Length];
                PackedRecWeights[s.Value] = new float[SeriesRecs.Length * SeriesRecs[0].DataWeights.Length];
                
                for (int n = 0; n < SeriesRecs.Length; n++)
                {
                    Array.Copy(SeriesRecs[n].DataFT, 0, PackedRecFT[s.Value], n * SeriesRecs[0].DataFT.Length, SeriesRecs[0].DataFT.Length);
                    Array.Copy(SeriesRecs[n].DataWeights, 0, PackedRecWeights[s.Value], n * SeriesRecs[0].DataWeights.Length, SeriesRecs[0].DataWeights.Length);
                }
            }

            float PixelSize = (float)Options.Movies[0].CTF.PixelSize;
            float FreqMin = 1f / (18.0f / PixelSize), FreqMin2 = FreqMin * FreqMin;
            float FreqMax = 1f / (14.5f / PixelSize), FreqMax2 = FreqMax * FreqMax;

            int ShellMin = (int)(Dims.X * FreqMin);
            int ShellMax = (int)(Dims.X * FreqMax);
            int NShells = ShellMax - ShellMin;

            float[] R2 = new float[(Dims.X / 2 + 1) * Dims.Y * Dims.Z];
            int[] ShellIndices = new int[R2.Length];

            for (int z = 0; z < Dims.Z; z++)
            {
                int zz = z < Dims.Z / 2 + 1 ? z : z - Dims.Z;
                zz *= zz;
                for (int y = 0; y < Dims.Y; y++)
                {
                    int yy = y < Dims.Y / 2 + 1 ? y : y - Dims.Y;
                    yy *= yy;
                    for (int x = 0; x < Dims.X / 2 + 1; x++)
                    {
                        int xx = x;
                        xx *= x;

                        float r = (float)Math.Sqrt(zz + yy + xx) / Dims.X / PixelSize;
                        R2[(z * Dims.Y + y) * (Dims.X / 2 + 1) + x] = r * r;
                        int ir = (int)Math.Round(Math.Sqrt(zz + yy + xx));
                        ShellIndices[(z * Dims.Y + y) * (Dims.X / 2 + 1) + x] = ir < Dims.X / 2 ? ir : -1;
                    }
                }
            }

            float[] SeriesWeights = new float[SeriesIndices.Count];
            float[] SeriesBfacs = new float[SeriesIndices.Count];

            Func<double[], float[]> WeightedFSC = input =>
            {
                // Set parameters from input vector
                //{
                    int Skip = 0;
                    SeriesWeights = input.Take(SeriesWeights.Length).Select(v => (float)v / 100f).ToArray();
                    Skip += SeriesWeights.Length;
                    SeriesBfacs = input.Skip(Skip).Take(SeriesBfacs.Length).Select(v => (float)v * 10f).ToArray();
                //}

                // Initialize sum vectors
                float[] FSC = new float[Dims.X / 2];

                float[] MapSum1 = new float[Dims.ElementsFFT() * 2], MapSum2 = new float[Dims.ElementsFFT() * 2];
                float[] WeightSum1 = new float[Dims.ElementsFFT()], WeightSum2 = new float[Dims.ElementsFFT()];

                int ElementsFT = (int)Dims.ElementsFFT();

                foreach (var s in SeriesIndices)
                {
                    WeightOptContainer[] SeriesRecs = Reconstructions.Where(r => r.SeriesID == s.Value).ToArray();

                    float[] PrecalcWeights = new float[SeriesRecs.Length];
                    float[] PrecalcBfacs = new float[SeriesRecs.Length];
                    int[] PrecalcSubsets = new int[SeriesRecs.Length];

                    for (int n = 0; n < SeriesRecs.Length; n++)
                    {
                        WeightOptContainer reconstruction = SeriesRecs[n];
                        // Weight is Weight(Series) * exp(Bfac(Series) / 4 * r^2)

                        float SeriesWeight = (float)Math.Exp(SeriesWeights[reconstruction.SeriesID]);
                        float SeriesBfac = SeriesBfacs[reconstruction.SeriesID];

                        PrecalcWeights[n] = SeriesWeight;
                        PrecalcBfacs[n] = SeriesBfac * 0.25f;
                        PrecalcSubsets[n] = reconstruction.Subset;
                    }

                    CPU.OptimizeWeights(SeriesRecs.Length,
                                        PackedRecFT[s.Value],
                                        PackedRecWeights[s.Value],
                                        R2,
                                        ElementsFT,
                                        PrecalcSubsets,
                                        PrecalcBfacs,
                                        PrecalcWeights,
                                        MapSum1,
                                        MapSum2,
                                        WeightSum1,
                                        WeightSum2);
                }

                for (int i = 0; i < ElementsFT; i++)
                {
                    float Weight = Math.Max(1e-3f, WeightSum1[i]);
                    MapSum1[i * 2] /= Weight;
                    MapSum1[i * 2 + 1] /= Weight;

                    Weight = Math.Max(1e-3f, WeightSum2[i]);
                    MapSum2[i * 2] /= Weight;
                    MapSum2[i * 2 + 1] /= Weight;
                }
                
                Image Map1FT = new Image(MapSum1, Dims, true, true);
                Image Map1 = Map1FT.AsIFFT(true);
                Map1.Multiply(SubsetMasks[0]);
                Image MaskedFT1 = Map1.AsFFT(true);
                float[] MaskedFT1Data = MaskedFT1.GetHostContinuousCopy();

                Map1FT.Dispose();
                Map1.Dispose();
                MaskedFT1.Dispose();

                Image Map2FT = new Image(MapSum2, Dims, true, true);
                Image Map2 = Map2FT.AsIFFT(true);
                Map2.Multiply(SubsetMasks[1]);
                Image MaskedFT2 = Map2.AsFFT(true);
                float[] MaskedFT2Data = MaskedFT2.GetHostContinuousCopy();

                Map2FT.Dispose();
                Map2.Dispose();
                MaskedFT2.Dispose();

                float[] Nums = new float[Dims.X / 2];
                float[] Denoms1 = new float[Dims.X / 2];
                float[] Denoms2 = new float[Dims.X / 2];
                for (int i = 0; i < ElementsFT; i++)
                {
                    int Shell = ShellIndices[i];
                    if (Shell < 0)
                        continue;

                    Nums[Shell] += MaskedFT1Data[i * 2] * MaskedFT2Data[i * 2] + MaskedFT1Data[i * 2 + 1] * MaskedFT2Data[i * 2 + 1];
                    Denoms1[Shell] += MaskedFT1Data[i * 2] * MaskedFT1Data[i * 2] + MaskedFT1Data[i * 2 + 1] * MaskedFT1Data[i * 2 + 1];
                    Denoms2[Shell] += MaskedFT2Data[i * 2] * MaskedFT2Data[i * 2] + MaskedFT2Data[i * 2 + 1] * MaskedFT2Data[i * 2 + 1];
                }

                for (int i = 0; i < Dims.X / 2; i++)
                    FSC[i] = Nums[i] / (float)Math.Sqrt(Denoms1[i] * Denoms2[i]);

                return FSC;
            };

            Func<double[], double> EvalForGrad = input =>
            {
                return WeightedFSC(input).Skip(ShellMin).Take(NShells).Sum() * Reconstructions.Count;
            };

            Func<double[], double> Eval = input =>
            {
                double Score = EvalForGrad(input);
                Debug.WriteLine(Score);

                return Score;
            };

            int Iterations = 0;

            Func<double[], double[]> Grad = input =>
            {
                double[] Result = new double[input.Length];
                double Step = 4;

                if (Iterations++ > 15)
                    return Result;

                //Parallel.For(0, input.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
                for (int i = 0; i < input.Length; i++)
                {
                    double[] InputCopy = input.ToList().ToArray();
                    double Original = InputCopy[i];
                    InputCopy[i] = Original + Step;
                    double ResultPlus = EvalForGrad(InputCopy);
                    InputCopy[i] = Original - Step;
                    double ResultMinus = EvalForGrad(InputCopy);
                    InputCopy[i] = Original;

                    Result[i] = (ResultPlus - ResultMinus) / (Step * 2);
                }//);

                return Result;
            };

            List<double> StartParamsList = new List<double>();
            StartParamsList.AddRange(SeriesWeights.Select(v => (double)v));
            StartParamsList.AddRange(SeriesBfacs.Select(v => (double)v));

            double[] StartParams = StartParamsList.ToArray();

            BroydenFletcherGoldfarbShanno Optimizer = new BroydenFletcherGoldfarbShanno(StartParams.Length, Eval, Grad);
            Optimizer.Epsilon = 3e-7;
            Optimizer.Maximize(StartParams);
            
            EvalForGrad(StartParams);
            
            foreach (var s in SeriesIndices)
            {
                s.Key.GlobalWeight = (float)Math.Exp(SeriesWeights[s.Value] - MathHelper.Max(SeriesWeights));   // Minus, because exponential
                s.Key.GlobalBfactor = SeriesBfacs[s.Value] - MathHelper.Max(SeriesBfacs);

                s.Key.SaveMeta();
            }
        }

        private void OptimizePerTiltWeights()
        {
            if (!Options.Movies.Any(m => m.GetType() == typeof(TiltSeries)))
                return;

            Image Mask = StageDataLoad.LoadMap("F:\\stefanribo\\vlion\\mask_warped2_OST_post.mrc", new int2(1, 1), 0, typeof(float));
            List<Image> SubsetMasks = new List<Image> { Mask, Mask };

            int3 Dims = Mask.Dims;
            float AngleMin = float.MaxValue, AngleMax = float.MinValue;
            float DoseMax = float.MinValue;
            List<WeightOptContainer> Reconstructions = new List<WeightOptContainer>();
            Dictionary<TiltSeries, int> SeriesIndices = new Dictionary<TiltSeries, int>();

            int NTilts = 0;
            
            {
                TiltSeries Series = (TiltSeries)Options.Movies[0];
                string[] FileNames = Directory.EnumerateFiles(Series.WeightOptimizationDir, "subset*.mrc").Where(p => p.Contains("subset3") || p.Contains("subset4")).Select(v => new FileInfo(v).Name).ToArray();

                string[] MapNames = FileNames.Where(v => !v.Contains(".weight.mrc")).ToArray();
                string[] WeightNames = FileNames.Where(v => v.Contains(".weight.mrc")).ToArray();
                if (MapNames.Length != WeightNames.Length)
                    throw new Exception("Number of reconstructions and weights does not match!");

                string[] MapSuffixes = MapNames;
                int[] MapSubsets = MapSuffixes.Select(v =>
                {
                    string S = v.Substring(v.IndexOf("subset") + "subset".Length);
                    return int.Parse(S.Substring(0, S.IndexOf("_"))) - 3;
                }).ToArray();
                int[] MapTilts = MapSuffixes.Select(v =>
                {
                    string S = v.Substring(v.IndexOf("tilt") + "tilt".Length);
                    return int.Parse(S.Substring(0, S.IndexOf(".mrc")));
                }).ToArray();

                SeriesIndices.Add(Series, SeriesIndices.Count);

                float[] MapAngles = MapTilts.Select(t => Series.AnglesCorrect[t]).ToArray();
                float[] MapDoses = MapTilts.Select(t => Series.Dose[t]).ToArray();

                for (int i = 0; i < MapNames.Length; i++)
                {
                    Image Map = StageDataLoad.LoadMap(Series.WeightOptimizationDir + MapNames[i], new int2(1, 1), 0, typeof(float));
                    Image MapFT = Map.AsFFT(true);
                    float[] MapData = MapFT.GetHostContinuousCopy();
                    Map.Dispose();
                    MapFT.Dispose();

                    Image Weights = StageDataLoad.LoadMap(Series.WeightOptimizationDir + WeightNames[i], new int2(1, 1), 0, typeof(float));
                    float[] WeightsData = Weights.GetHostContinuousCopy();
                    Weights.Dispose();

                    Reconstructions.Add(new WeightOptContainer(SeriesIndices[Series], MapSubsets[i], MapData, WeightsData, MapAngles[i], MapDoses[i]));
                }

                AngleMin = Math.Min(MathHelper.Min(MapAngles), AngleMin);
                AngleMax = Math.Max(MathHelper.Max(MapAngles), AngleMax);
                DoseMax = Math.Max(MathHelper.Max(MapDoses), DoseMax);

                NTilts = Series.NTilts;

                //break;
            }

            float[][] PackedRecFT = new float[SeriesIndices.Count][];
            float[][] PackedRecWeights = new float[SeriesIndices.Count][];
            foreach (var s in SeriesIndices)
            {
                WeightOptContainer[] SeriesRecs = Reconstructions.Where(r => r.SeriesID == s.Value).ToArray();
                PackedRecFT[s.Value] = new float[SeriesRecs.Length * SeriesRecs[0].DataFT.Length];
                PackedRecWeights[s.Value] = new float[SeriesRecs.Length * SeriesRecs[0].DataWeights.Length];

                for (int n = 0; n < SeriesRecs.Length; n++)
                {
                    Array.Copy(SeriesRecs[n].DataFT, 0, PackedRecFT[s.Value], n * SeriesRecs[0].DataFT.Length, SeriesRecs[0].DataFT.Length);
                    Array.Copy(SeriesRecs[n].DataWeights, 0, PackedRecWeights[s.Value], n * SeriesRecs[0].DataWeights.Length, SeriesRecs[0].DataWeights.Length);
                }
            }

            float PixelSize = (float)Options.Movies[0].CTF.PixelSize;
            float FreqMin = 1f / (10f / PixelSize), FreqMin2 = FreqMin * FreqMin;
            float FreqMax = 1f / (8.5f / PixelSize), FreqMax2 = FreqMax * FreqMax;

            int ShellMin = (int)(Dims.X * FreqMin);
            int ShellMax = (int)(Dims.X * FreqMax);
            int NShells = ShellMax - ShellMin;

            float[] R2 = new float[(Dims.X / 2 + 1) * Dims.Y * Dims.Z];
            int[] ShellIndices = new int[R2.Length];

            for (int z = 0; z < Dims.Z; z++)
            {
                int zz = z < Dims.Z / 2 + 1 ? z : z - Dims.Z;
                zz *= zz;
                for (int y = 0; y < Dims.Y; y++)
                {
                    int yy = y < Dims.Y / 2 + 1 ? y : y - Dims.Y;
                    yy *= yy;
                    for (int x = 0; x < Dims.X / 2 + 1; x++)
                    {
                        int xx = x;
                        xx *= x;

                        float r = (float)Math.Sqrt(zz + yy + xx) / Dims.X / PixelSize;
                        R2[(z * Dims.Y + y) * (Dims.X / 2 + 1) + x] = r * r;
                        int ir = (int)Math.Round(Math.Sqrt(zz + yy + xx));
                        ShellIndices[(z * Dims.Y + y) * (Dims.X / 2 + 1) + x] = ir < Dims.X / 2 ? ir : -1;
                    }
                }
            }

            float[] SeriesWeights = new float[SeriesIndices.Count];
            float[] SeriesBfacs = new float[SeriesIndices.Count];
            float[] InitGridAngle = new float[NTilts], InitGridDose = new float[NTilts];
            for (int i = 0; i < InitGridAngle.Length; i++)
            {
                InitGridAngle[i] = (float)Math.Cos((i / (float)(InitGridAngle.Length - 1) * (AngleMax - AngleMin) + AngleMin) * Helper.ToRad) * 100f;
                InitGridDose[i] = -8 * i / (float)(InitGridAngle.Length - 1) * DoseMax / 10f;
            }
            CubicGrid GridAngle = new CubicGrid(new int3(NTilts, 1, 1), InitGridAngle);
            CubicGrid GridDose = new CubicGrid(new int3(NTilts, 1, 1), InitGridDose);

            Func<double[], float[]> WeightedFSC = input =>
            {
                // Set parameters from input vector
                {
                    int Skip = 0;
                    GridAngle = new CubicGrid(GridAngle.Dimensions, input.Skip(Skip).Take((int)GridAngle.Dimensions.Elements()).Select(v => (float)v / 100f).ToArray());
                    Skip += (int)GridAngle.Dimensions.Elements();
                    GridDose = new CubicGrid(GridDose.Dimensions, input.Skip(Skip).Take((int)GridDose.Dimensions.Elements()).Select(v => (float)v * 10f).ToArray());
                }

                // Initialize sum vectors
                float[] FSC = new float[Dims.X / 2];

                float[] MapSum1 = new float[Dims.ElementsFFT() * 2], MapSum2 = new float[Dims.ElementsFFT() * 2];
                float[] WeightSum1 = new float[Dims.ElementsFFT()], WeightSum2 = new float[Dims.ElementsFFT()];

                int ElementsFT = (int)Dims.ElementsFFT();

                foreach (var s in SeriesIndices)
                {
                    WeightOptContainer[] SeriesRecs = Reconstructions.Where(r => r.SeriesID == s.Value).ToArray();

                    float[] PrecalcWeights = new float[SeriesRecs.Length];
                    float[] PrecalcBfacs = new float[SeriesRecs.Length];
                    int[] PrecalcSubsets = new int[SeriesRecs.Length];

                    for (int n = 0; n < SeriesRecs.Length; n++)
                    {
                        WeightOptContainer reconstruction = SeriesRecs[n];
                        // Weight is Weight(Series) * Weight(Angle) * exp((Bfac(Series) + Bfac(Dose)) / 4 * r^2)                        
                        
                        float AngleWeight = GridAngle.GetInterpolated(new float3((reconstruction.Angle - AngleMin) / (AngleMax - AngleMin), 0.5f, 0.5f));
                        float DoseBfac = GridDose.GetInterpolated(new float3(reconstruction.Dose / DoseMax, 0.5f, 0.5f));

                        PrecalcWeights[n] = AngleWeight;
                        PrecalcBfacs[n] = DoseBfac * 0.25f;
                        PrecalcSubsets[n] = reconstruction.Subset;
                    }

                    CPU.OptimizeWeights(SeriesRecs.Length,
                                        PackedRecFT[s.Value],
                                        PackedRecWeights[s.Value],
                                        R2,
                                        ElementsFT,
                                        PrecalcSubsets,
                                        PrecalcBfacs,
                                        PrecalcWeights,
                                        MapSum1,
                                        MapSum2,
                                        WeightSum1,
                                        WeightSum2);
                }

                for (int i = 0; i < ElementsFT; i++)
                {
                    float Weight = Math.Max(1e-3f, WeightSum1[i]);
                    MapSum1[i * 2] /= Weight;
                    MapSum1[i * 2 + 1] /= Weight;

                    Weight = Math.Max(1e-3f, WeightSum2[i]);
                    MapSum2[i * 2] /= Weight;
                    MapSum2[i * 2 + 1] /= Weight;
                }

                lock (GridAngle)
                {
                    Image Map1FT = new Image(MapSum1, Dims, true, true);
                    Image Map1 = Map1FT.AsIFFT(true);
                    Map1.Multiply(SubsetMasks[0]);
                    Image MaskedFT1 = Map1.AsFFT(true);
                    float[] MaskedFT1Data = MaskedFT1.GetHostContinuousCopy();

                    Map1FT.Dispose();
                    Map1.Dispose();
                    MaskedFT1.Dispose();

                    Image Map2FT = new Image(MapSum2, Dims, true, true);
                    Image Map2 = Map2FT.AsIFFT(true);
                    Map2.Multiply(SubsetMasks[1]);
                    Image MaskedFT2 = Map2.AsFFT(true);
                    float[] MaskedFT2Data = MaskedFT2.GetHostContinuousCopy();

                    Map2FT.Dispose();
                    Map2.Dispose();
                    MaskedFT2.Dispose();

                    float[] Nums = new float[Dims.X / 2];
                    float[] Denoms1 = new float[Dims.X / 2];
                    float[] Denoms2 = new float[Dims.X / 2];
                    for (int i = 0; i < ElementsFT; i++)
                    {
                        int Shell = ShellIndices[i];
                        if (Shell < 0)
                            continue;

                        Nums[Shell] += MaskedFT1Data[i * 2] * MaskedFT2Data[i * 2] + MaskedFT1Data[i * 2 + 1] * MaskedFT2Data[i * 2 + 1];
                        Denoms1[Shell] += MaskedFT1Data[i * 2] * MaskedFT1Data[i * 2] + MaskedFT1Data[i * 2 + 1] * MaskedFT1Data[i * 2 + 1];
                        Denoms2[Shell] += MaskedFT2Data[i * 2] * MaskedFT2Data[i * 2] + MaskedFT2Data[i * 2 + 1] * MaskedFT2Data[i * 2 + 1];
                    }

                    for (int i = 0; i < Dims.X / 2; i++)
                        FSC[i] = Nums[i] / (float)Math.Sqrt(Denoms1[i] * Denoms2[i]);
                }

                return FSC;
            };

            Func<double[], double> EvalForGrad = input =>
            {
                return WeightedFSC(input).Skip(ShellMin).Take(NShells).Sum() * Reconstructions.Count;
            };

            Func<double[], double> Eval = input =>
            {
                double Score = EvalForGrad(input);
                Debug.WriteLine(Score);

                return Score;
            };

            int Iterations = 0;

            Func<double[], double[]> Grad = input =>
            {
                double[] Result = new double[input.Length];
                double Step = 1;

                if (Iterations++ > 15)
                    return Result;

                //Parallel.For(0, input.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
                for (int i = 0; i < input.Length; i++)
                {
                    double[] InputCopy = input.ToList().ToArray();
                    double Original = InputCopy[i];
                    InputCopy[i] = Original + Step;
                    double ResultPlus = EvalForGrad(InputCopy);
                    InputCopy[i] = Original - Step;
                    double ResultMinus = EvalForGrad(InputCopy);
                    InputCopy[i] = Original;

                    Result[i] = (ResultPlus - ResultMinus) / (Step * 2);
                }//);

                return Result;
            };

            List<double> StartParamsList = new List<double>();
            StartParamsList.AddRange(GridAngle.FlatValues.Select(v => (double)v));
            StartParamsList.AddRange(GridDose.FlatValues.Select(v => (double)v));

            double[] StartParams = StartParamsList.ToArray();

            BroydenFletcherGoldfarbShanno Optimizer = new BroydenFletcherGoldfarbShanno(StartParams.Length, Eval, Grad);
            Optimizer.Epsilon = 3e-7;
            Optimizer.Maximize(StartParams);

            EvalForGrad(StartParams);

            float MaxAngleWeight = MathHelper.Max(GridAngle.FlatValues);
            GridAngle = new CubicGrid(GridAngle.Dimensions, GridAngle.FlatValues.Select(v => v / MaxAngleWeight).ToArray());

            float MaxDoseBfac = MathHelper.Max(GridDose.FlatValues);
            GridDose = new CubicGrid(GridDose.Dimensions, GridDose.FlatValues.Select(v => v - MaxDoseBfac).ToArray());

            foreach (var s in Options.Movies)
            {
                TiltSeries Series = (TiltSeries)s;
                
                List<float> AngleWeights = new List<float>();
                List<float> DoseBfacs = new List<float>();
                for (int i = 0; i < Series.Angles.Length; i++)
                {
                    float AngleWeight = GridAngle.GetInterpolated(new float3(Math.Min(1, (Series.AnglesCorrect[i] - AngleMin) / (AngleMax - AngleMin)), 0.5f, 0.5f));
                    float DoseBfac = GridDose.GetInterpolated(new float3(Math.Min(1, Series.Dose[i] / DoseMax), 0.5f, 0.5f));

                    AngleWeights.Add(AngleWeight);
                    DoseBfacs.Add(DoseBfac);
                }

                Series.GridAngleWeights = new CubicGrid(new int3(1, 1, AngleWeights.Count), AngleWeights.ToArray());
                Series.GridDoseBfacs = new CubicGrid(new int3(1, 1, DoseBfacs.Count), DoseBfacs.ToArray());

                Series.SaveMeta();
            }
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

    class WeightOptContainer
    {
        public int SeriesID;
        public int Subset;
        public float[] DataFT;
        public float[] DataWeights;
        public float Angle;
        public float Dose;

        public WeightOptContainer(int seriesID, int subset, float[] dataFT, float[] dataWeights, float angle, float dose)
        {
            SeriesID = seriesID;
            Subset = subset;
            DataFT = dataFT;
            DataWeights = dataWeights;
            Angle = angle;
            Dose = dose;
        }
    }
}
