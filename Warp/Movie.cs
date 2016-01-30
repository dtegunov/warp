using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.XPath;
using Accord.Math.Optimization;
using Warp.Headers;
using Warp.Stages;
using Warp.Tools;

namespace Warp
{
    public class Movie : DataBase
    {
        private string _Path = "";
        public string Path
        {
            get { return _Path; }
            set { if (value != _Path) { _Path = value; OnPropertyChanged(); } }
        }

        public string Name
        {
            get
            {
                if (Path.Length == 0)
                    return "";

                return Path.Substring(Path.LastIndexOf('\\') + 1);
            }
        }

        public string RootName
        {
            get
            {
                if (Path.Length == 0)
                    return "";
                
                return Name.Substring(0, Name.LastIndexOf('.'));
            }
        }

        public string DirectoryName
        {
            get
            {
                if (Path.Length == 0)
                    return "";

                FileInfo Info = new FileInfo(Path);
                string Dir = Info.DirectoryName;
                if (Dir[Dir.Length - 1] != '\\')
                    Dir += "\\";
                return Dir;
            }
        }

        public string PowerSpectrumDir => DirectoryName + "spectrum\\";
        public string CTFDir => DirectoryName + "ctf\\";
        public string AverageDir => DirectoryName + "average\\";
        public string ShiftedStackDir => DirectoryName + "stack\\";
        
        public string PowerSpectrumPath => PowerSpectrumDir + Name;
        public string CTFPath => CTFDir + Name;
        public string AveragePath => AverageDir + Name;
        public string ShiftedStackPath => ShiftedStackDir + Name;

        public string XMLName => RootName + ".xml";
        public string XMLPath => DirectoryName + XMLName;

        public int ResidentDevice = 0;

        private ProcessingStatus _Status = ProcessingStatus.Unprocessed;
        public ProcessingStatus Status
        {
            get { return _Status; }
            set { if (value != _Status) { _Status = value; OnPropertyChanged(); } }
        }

        private int3 _Dimensions = new int3(1, 1, 1);
        public int3 Dimensions
        {
            get { return _Dimensions; }
            set { if (value != _Dimensions) { _Dimensions = value; OnPropertyChanged(); } }
        }

        private CTF _CTF = new CTF();
        public CTF CTF
        {
            get { return _CTF; }
            set { if (value != _CTF) { _CTF = value; OnPropertyChanged(); } }
        }

        public ImageSource PS2D
        {
            get
            {
                if (!File.Exists(PowerSpectrumPath))
                    return null;

                unsafe
                {
                    MapHeader Header = MapHeader.ReadFromFile(PowerSpectrumPath);
                    float[] Data = IOHelper.ReadSmallMapFloat(PowerSpectrumPath, new int2(1, 1), 0, typeof(float));

                    int Width = Header.Dimensions.X;
                    int HalfWidth = Width / 2;
                    int Height = Header.Dimensions.Y;  // The usual x / 2 + 1

                    int RadiusMin2 = (int)(MainWindow.Options.CTFRangeMin * HalfWidth);
                    RadiusMin2 *= RadiusMin2;
                    int RadiusMax2 = (int)(MainWindow.Options.CTFRangeMax * HalfWidth);
                    RadiusMax2 *= RadiusMax2;

                    float ValueMin = float.MaxValue;
                    float ValueMax = -float.MaxValue;

                    fixed (float* DataPtr = Data)
                    {
                        float* DataP = DataPtr;
                        for (int y = 0; y < Height; y++)
                        {
                            for (int x = 0; x < Width; x++)
                            {
                                int XCentered = x - HalfWidth;
                                int Radius2 = XCentered * XCentered + y * y;
                                if (Radius2 >= RadiusMin2 && Radius2 <= RadiusMax2)
                                {
                                    ValueMin = Math.Min(ValueMin, *DataP);
                                    ValueMax = Math.Max(ValueMax, *DataP);
                                }

                                DataP++;
                            }
                        }

                        float Range = ValueMax - ValueMin;
                        if (Range == 0f)
                            return BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Indexed8, BitmapPalettes.Gray256, new byte[Data.Length], Width);

                        byte[] DataBytes = new byte[Data.Length];
                        fixed (byte* DataBytesPtr = DataBytes)
                        {
                            for (int i = 0; i < DataBytes.Length; i++)
                                DataBytesPtr[i] = (byte)(Math.Max(Math.Min(1f, (DataPtr[i] - ValueMin) / Range), 0f) * 255f);
                        }

                        return BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Indexed8, BitmapPalettes.Gray256, DataBytes, Width);
                    }
                }
            }
        }

        private float2[] _PS1D;
        public float2[] PS1D
        {
            get { return _PS1D; }
            set { if (value != _PS1D) { _PS1D = value; OnPropertyChanged(); OnPropertyChanged("CTFQuality"); } }
        }

        private CubicGrid _GridCTF = new CubicGrid(new int3(1, 1, 1));
        public CubicGrid GridCTF
        {
            get { return _GridCTF; }
            set { if (value != _GridCTF) { _GridCTF = value; OnPropertyChanged(); } }
        }

        private CubicGrid _GridMovementX = new CubicGrid(new int3(1, 1, 1));
        public CubicGrid GridMovementX
        {
            get { return _GridMovementX; }
            set { if (value != _GridMovementX) { _GridMovementX = value; OnPropertyChanged(); } }
        }

        private CubicGrid _GridMovementY = new CubicGrid(new int3(1, 1, 1));
        public CubicGrid GridMovementY
        {
            get { return _GridMovementY; }
            set { if (value != _GridMovementY) { _GridMovementY = value; OnPropertyChanged(); } }
        }

        public ImageSource Simulated2D
        {
            get
            {
                int Width = MainWindow.Options.CTFWindow / 2;

                unsafe
                {
                    float2[] SimCoords = new float2[Width * Width];
                    fixed (float2* SimCoordsPtr = SimCoords)
                    {
                        float2* SimCoordsP = SimCoordsPtr;
                        for (int y = 0; y < Width; y++)
                        {
                            int ycoord = y - Width;
                            int ycoord2 = ycoord * ycoord;
                            for (int x = 0; x < Width; x++)
                            {
                                int xcoord = x - Width;
                                *SimCoordsP++ = new float2((float)Math.Sqrt(xcoord * xcoord + ycoord2) / (Width * 2), (float)Math.Atan2(ycoord, xcoord));
                            }
                        }
                    }
                    float[] Sim2D = CTF.Get2D(SimCoords, true, true, true);
                    byte[] Sim2DBytes = new byte[Sim2D.Length];
                    fixed (byte* Sim2DBytesPtr = Sim2DBytes)
                    fixed (float* Sim2DPtr = Sim2D)
                    {
                        byte* Sim2DBytesP = Sim2DBytesPtr;
                        float* Sim2DP = Sim2DPtr;
                        for (int i = 0; i < Width * Width; i++)
                            *Sim2DBytesP++ = (byte)(*Sim2DP++ * 128f + 127f);
                    }

                    return BitmapSource.Create(Width, Width, 96, 96, PixelFormats.Indexed8, BitmapPalettes.Gray256, Sim2DBytes, Width);
                }
            }
        }

        public float2[] Simulated1D
        {
            get
            {
                if (PS1D == null || _SimulatedBackground == null)
                    return null;

                float[] SimulatedCTF = CTF.Get1D(PS1D.Length, true);

                float2[] Result = new float2[PS1D.Length];
                for (int i = 0; i < Result.Length; i++)
                    Result[i] = new float2(PS1D[i].X, _SimulatedBackground.Interp(PS1D[i].X) + SimulatedCTF[i] * _SimulatedScale.Interp(PS1D[i].X));

                return Result;
            }
        }

        private Cubic1D _SimulatedBackground;
        public Cubic1D SimulatedBackground
        {
            get { return _SimulatedBackground; }
            set { if (value != _SimulatedBackground) { _SimulatedBackground = value; OnPropertyChanged(); } }
        }

        private Cubic1D _SimulatedScale = new Cubic1D(new[] { new float2(0, 1), new float2(1, 1) });
        public Cubic1D SimulatedScale
        {
            get { return _SimulatedScale; }
            set { if (value != _SimulatedScale) { _SimulatedScale = value; OnPropertyChanged(); } }
        }

        public float2[] CTFQuality
        {
            // Calculate the correlation between experimental and simulated peaks.
            // If there is no single complete peak (i. e. <=1 zeros), just take the entire
            // fitted range and treat it as one peak.
            get
            {
                if (PS1D == null || CTF == null || Simulated1D == null)
                    return null;

                float[] Zeros = CTF.GetZeros();
                float MinX = (float)MainWindow.Options.CTFRangeMin * 0.5f;
                float MaxX = MathHelper.Max(PS1D.Select(v => v.X));
                Zeros = Zeros.Where(v => v >= MinX && v <= MaxX).ToArray();
                if (Zeros.Length <= 1)
                    Zeros = new[] { MinX, MaxX };

                int[] Offsets = Zeros.Select(v => (int)(v / 0.5f * (PS1D.Length - 1))).ToArray();
                float2[] Result = new float2[Zeros.Length - 1];
                float[] Experimental = PS1D.Select(v => v.Y).ToArray();
                float[] Simulated = Simulated1D.Select(v => v.Y).ToArray();

                for (int i = 0; i < Zeros.Length - 1; i++)
                {
                    float[] PeakExperimental = Experimental.Skip(Offsets[i]).Take(Offsets[i + 1] - Offsets[i] + 1).ToArray();
                    float[] PeakSimulated = Simulated.Skip(Offsets[i]).Take(Offsets[i + 1] - Offsets[i] + 1).ToArray();
                    float Correlation = MathHelper.CrossCorrelateNormalized(PeakExperimental, PeakSimulated);

                    Result[i] = new float2((Zeros[i] + Zeros[i + 1]) * 0.5f, Math.Max(0.0f, Correlation));
                }

                // Sanitation
                Result = Result.Select(v => float.IsNaN(v.Y) ? new float2(v.X, 0f) : v).ToArray();

                return Result;
            }
        }

        public Movie(string path)
        {
            Path = path;
            CTF.PropertyChanged += CTF_PropertyChanged;
            MainWindow.Options.PropertyChanged += Options_PropertyChanged;

            LoadMeta();
        }

        private void CTF_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            OnPropertyChanged("Simulated2D");
            OnPropertyChanged("Simulated1D");
            OnPropertyChanged("CTFQuality");
        }

        private void Options_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Substring(0, 3) == "CTF")
            {
                OnPropertyChanged("Simulated2D");
                OnPropertyChanged("Simulated1D");
                OnPropertyChanged("CTFQuality");
            }
            else if (e.PropertyName == "CTFRangeMin" || e.PropertyName == "CTFRangeMax")
            {
                OnPropertyChanged("PS2D");
                OnPropertyChanged("CTFQuality");
            }
        }

        public void LoadData()
        {

        }

        public void FreeData()
        {

        }

        public void LoadMeta()
        {
            if (!File.Exists(XMLPath))
                return;

            using (Stream SettingsStream = File.OpenRead(XMLPath))
            {
                XPathDocument Doc = new XPathDocument(SettingsStream);
                XPathNavigator Reader = Doc.CreateNavigator();
                Reader.MoveToRoot();
                Reader.MoveToFirstChild();

                XPathNavigator NavPS1D = Reader.SelectSingleNode("//PS1D");
                if (NavPS1D != null)
                    PS1D = NavPS1D.InnerXml.Split(';').Select(v =>
                    {
                        string[] Pair = v.Split('|');
                        return new float2(float.Parse(Pair[0], CultureInfo.InvariantCulture), float.Parse(Pair[1], CultureInfo.InvariantCulture));
                    }).ToArray();

                XPathNavigator NavSimBackground = Reader.SelectSingleNode("//SimulatedBackground");
                if (NavSimBackground != null)
                    _SimulatedBackground = new Cubic1D(NavSimBackground.InnerXml.Split(';').Select(v =>
                    {
                        string[] Pair = v.Split('|');
                        return new float2(float.Parse(Pair[0], CultureInfo.InvariantCulture), float.Parse(Pair[1], CultureInfo.InvariantCulture));
                    }).ToArray());

                XPathNavigator NavSimScale = Reader.SelectSingleNode("//SimulatedScale");
                if (NavSimScale != null)
                    _SimulatedScale = new Cubic1D(NavSimScale.InnerXml.Split(';').Select(v =>
                    {
                        string[] Pair = v.Split('|');
                        return new float2(float.Parse(Pair[0], CultureInfo.InvariantCulture), float.Parse(Pair[1], CultureInfo.InvariantCulture));
                    }).ToArray());

                XPathNavigator NavCTF = Reader.SelectSingleNode("//CTF");
                if (NavCTF != null)
                    CTF.Load(NavCTF);

                XPathNavigator NavGridCTF = Reader.SelectSingleNode("//GridCTF");
                if (NavGridCTF != null)
                    GridCTF = CubicGrid.Load(NavGridCTF);

                XPathNavigator NavMoveX = Reader.SelectSingleNode("//GridMovementX");
                if (NavMoveX != null)
                    GridMovementX = CubicGrid.Load(NavMoveX);

                XPathNavigator NavMoveY = Reader.SelectSingleNode("//GridMovementY");
                if (NavMoveY != null)
                    GridMovementY = CubicGrid.Load(NavMoveY);
            }
        }

        public void SaveMeta()
        {
            using (XmlTextWriter Writer = new XmlTextWriter(XMLPath, Encoding.Unicode))
            {
                Writer.Formatting = Formatting.Indented;
                Writer.IndentChar = '\t';
                Writer.Indentation = 1;
                Writer.WriteStartDocument();
                Writer.WriteStartElement("Movie");

                if (PS1D != null)
                {
                    Writer.WriteStartElement("PS1D");
                    Writer.WriteString(string.Join(";", PS1D.Select(v => v.X.ToString(CultureInfo.InvariantCulture) + "|" + v.Y.ToString(CultureInfo.InvariantCulture))));
                    Writer.WriteEndElement();
                }

                if (SimulatedBackground != null)
                {
                    Writer.WriteStartElement("SimulatedBackground");
                    Writer.WriteString(string.Join(";",
                                       _SimulatedBackground.Data.Select(
                                           v => v.X.ToString(CultureInfo.InvariantCulture) +
                                                "|" +
                                                v.Y.ToString(CultureInfo.InvariantCulture))));
                    Writer.WriteEndElement();
                }

                if (SimulatedScale != null)
                {
                    Writer.WriteStartElement("SimulatedScale");
                    Writer.WriteString(string.Join(";",
                                       _SimulatedScale.Data.Select(
                                           v => v.X.ToString(CultureInfo.InvariantCulture) +
                                                "|" +
                                                v.Y.ToString(CultureInfo.InvariantCulture))));
                    Writer.WriteEndElement();
                }

                Writer.WriteStartElement("CTF");
                CTF.Save(Writer);
                Writer.WriteEndElement();

                Writer.WriteStartElement("GridCTF");
                GridCTF.Save(Writer);
                Writer.WriteEndElement();

                Writer.WriteStartElement("GridMovementX");
                GridMovementX.Save(Writer);
                Writer.WriteEndElement();

                Writer.WriteStartElement("GridMovementY");
                GridMovementY.Save(Writer);
                Writer.WriteEndElement();

                Writer.WriteEndElement();
                Writer.WriteEndDocument();
            }
        }

        public void ProcessCTF(MapHeader originalHeader, Image originalStack, bool doastigmatism)
        {
            if (!Directory.Exists(PowerSpectrumDir))
                Directory.CreateDirectory(PowerSpectrumDir);

            CTF = new CTF();
            PS1D = null;
            _SimulatedBackground = null;
            _SimulatedScale = new Cubic1D(new[] { new float2(0, 1), new float2(1, 1) });
            
            #region Dimensions and grids

            int NFrames = originalHeader.Dimensions.Z;
            int2 DimsImage = new int2(originalHeader.Dimensions);
            int2 DimsRegion = new int2(MainWindow.Options.CTFWindow, MainWindow.Options.CTFWindow);

            float OverlapFraction = 0.5f;
            int2 DimsPositionGrid;
            int3[] PositionGrid = Helper.GetEqualGridSpacing(DimsImage, new int2(DimsRegion.X / 1, DimsRegion.Y / 1), OverlapFraction, out DimsPositionGrid);
            int NPositions = (int)DimsPositionGrid.Elements();

            int CTFGridX = Math.Min(DimsPositionGrid.X, MainWindow.Options.GridCTFX);
            int CTFGridY = Math.Min(DimsPositionGrid.Y, MainWindow.Options.GridCTFY);
            int CTFGridZ = Math.Min(NFrames, MainWindow.Options.GridCTFZ);
            GridCTF = new CubicGrid(new int3(CTFGridX, CTFGridY, CTFGridZ));

            bool CTFSpace = CTFGridX * CTFGridY > 1;
            bool CTFTime = CTFGridZ > 1;
            int3 CTFSpectraGrid = new int3(CTFSpace ? DimsPositionGrid.X : 1,
                                           CTFSpace ? DimsPositionGrid.Y : 1,
                                           CTFTime ? NFrames : 1);

            int MinFreqInclusive = (int)(MainWindow.Options.CTFRangeMin * DimsRegion.X / 2);
            int MaxFreqExclusive = (int)(MainWindow.Options.CTFRangeMax * DimsRegion.X / 2);
            int NFreq = MaxFreqExclusive - MinFreqInclusive;

            float PixelSize = (float)(MainWindow.Options.CTFPixelMin + MainWindow.Options.CTFPixelMax) * 0.5f;
            float PixelDelta = (float)(MainWindow.Options.CTFPixelMax - MainWindow.Options.CTFPixelMin) * 0.5f;
            float PixelAngle = (float)MainWindow.Options.CTFPixelAngle / 180f * (float)Math.PI;

            #endregion

            #region Allocate GPU memory

            Image CTFSpectra = new Image(IntPtr.Zero, new int3(DimsRegion.X, DimsRegion.X, (int)CTFSpectraGrid.Elements()), true);
            Image CTFMean = new Image(IntPtr.Zero, new int3(DimsRegion), true);
            Image CTFCoordsCart = new Image(new int3(DimsRegion), true, true);
            Image CTFCoordsPolarTrimmed = new Image(new int3(NFreq, DimsRegion.X, 1), false, true);

            #endregion

            // Extract movie regions, create individual spectra in Cartesian coordinates and their mean.
            #region Create spectra
            GPU.CreateSpectra(originalStack.GetDevice(Intent.Read),
                                DimsImage,
                                NFrames,
                                PositionGrid,
                                NPositions,
                                DimsRegion,
                                new int3(CTFGridX, CTFGridY, CTFGridZ),
                                CTFSpectra.GetDevice(Intent.Write),
                                CTFMean.GetDevice(Intent.Write));
            originalStack.FreeDevice(); // Won't need it in this method anymore.

            #endregion

            // Populate address arrays for later.
            #region Init addresses
            {
                float2[] CoordsData = new float2[CTFCoordsCart.ElementsSliceComplex];
                
                Helper.ForEachElementFT(DimsRegion, (x, y, xx, yy, r, a) => CoordsData[y * (DimsRegion.X / 2 + 1) + x] = new float2(r, a));
                CTFCoordsCart.UpdateHostWithComplex(new [] { CoordsData });

                CoordsData = new float2[NFreq * DimsRegion.X];
                Helper.ForEachElement(CTFCoordsPolarTrimmed.DimsSlice, (x, y) =>
                {
                    float Angle = ((float)y / DimsRegion.X + 0.5f) * (float)Math.PI;
                    float Ny = 1f / ((PixelSize + PixelDelta * (float)Math.Cos(2.0 * (Angle - PixelAngle))) * DimsRegion.X);
                    CoordsData[y * NFreq + x] = new float2((x + MinFreqInclusive) * Ny, Angle);
                });
                CTFCoordsPolarTrimmed.UpdateHostWithComplex(new [] { CoordsData });
            }
            #endregion

            // Retrieve average 1D spectrum from CTFMean (not corrected for astigmatism yet).
            #region Initial 1D spectrum
            {
                Image CTFAverage1D = new Image(IntPtr.Zero, new int3(DimsRegion.X / 2, 1, 1));

                GPU.CTFMakeAverage(CTFMean.GetDevice(Intent.Read),
                                   CTFCoordsCart.GetDevice(Intent.Read),
                                   (uint) CTFMean.ElementsSliceReal,
                                   (uint) DimsRegion.X,
                                   new[] {new CTF().ToStruct()},
                                   new CTF().ToStruct(),
                                   0,
                                   (uint) DimsRegion.X / 2,
                                   null,
                                   1,
                                   CTFAverage1D.GetDevice(Intent.Write));

                //CTFAverage1D.WriteMRC("CTFAverage1D.mrc");

                float[] CTFAverage1DData = CTFAverage1D.GetHost(Intent.Read)[0];
                float2[] ForPS1D = new float2[DimsRegion.X / 2];
                for (int i = 0; i < ForPS1D.Length; i++)
                    ForPS1D[i] = new float2((float)i / DimsRegion.X, (float)Math.Round(CTFAverage1DData[i], 4));
                PS1D = ForPS1D;

                CTFAverage1D.FreeDevice();
            }
            #endregion

            // Fit background to currently best average (not corrected for astigmatism yet).
            {
                float2[] ForPS1D = PS1D.Skip(MinFreqInclusive).Take(Math.Max(2, NFreq)).ToArray();

                int NumNodes = Math.Max(2, (int)((MainWindow.Options.CTFRangeMax - MainWindow.Options.CTFRangeMin) * 10M));
                _SimulatedBackground = Cubic1D.Fit(ForPS1D, NumNodes); // This won't fit falloff and scale, because approx function is 0
            }

            #region Background fitting methods
            Action UpdateBackgroundFit = () =>
            {
                float2[] ForPS1D = PS1D.Skip(Math.Max(5, MinFreqInclusive / 2)).ToArray();
                Cubic1D.FitCTF(ForPS1D,
                               v => v.Select(x => CTF.Get1D(x / (float)CTF.PixelSize, true)).ToArray(),
                               CTF.GetZeros(),
                               CTF.GetPeaks(),
                               out _SimulatedBackground,
                               out _SimulatedScale);
            };

            Action<bool> UpdateRotationalAverage = keepbackground =>
            {
                float[] MeanData = CTFMean.GetHost(Intent.Read)[0];

                Image CTFMeanCorrected = new Image(new int3(DimsRegion), true);
                float[] MeanCorrectedData = CTFMeanCorrected.GetHost(Intent.Write)[0];

                // Subtract current background estimate from spectra, populate coords.
                Helper.ForEachElementFT(DimsRegion,
                                        (x, y, xx, yy, r, a) =>
                                        {
                                            int i = y * (DimsRegion.X / 2 + 1) + x;
                                            MeanCorrectedData[i] = MeanData[i] - _SimulatedBackground.Interp(r / DimsRegion.X);
                                        });
                
                Image CTFAverage1D = new Image(IntPtr.Zero, new int3(DimsRegion.X / 2, 1, 1));

                GPU.CTFMakeAverage(CTFMeanCorrected.GetDevice(Intent.Read),
                                   CTFCoordsCart.GetDevice(Intent.Read),
                                   (uint)CTFMeanCorrected.DimsEffective.ElementsSlice(),
                                   (uint)DimsRegion.X,
                                   new[] { CTF.ToStruct() },
                                   CTF.ToStruct(),
                                   0,
                                   (uint)DimsRegion.X / 2,
                                   null,
                                   1,
                                   CTFAverage1D.GetDevice(Intent.Write));

                //CTFAverage1D.WriteMRC("CTFAverage1D.mrc");

                float[] RotationalAverageData = CTFAverage1D.GetHost(Intent.Read)[0];
                float2[] ForPS1D = new float2[PS1D.Length];
                if (keepbackground)
                    for (int i = 0; i < ForPS1D.Length; i++)
                        ForPS1D[i] = new float2((float)i / DimsRegion.X, RotationalAverageData[i] + _SimulatedBackground.Interp((float)i / DimsRegion.X));
                else
                    for (int i = 0; i < ForPS1D.Length; i++)
                        ForPS1D[i] = new float2((float)i / DimsRegion.X, RotationalAverageData[i]);

                PS1D = ForPS1D;
                
                CTFMeanCorrected.FreeDevice();
                CTFAverage1D.FreeDevice();
            };

            #endregion

            // Fit defocus, (phase shift), (astigmatism) to average background-subtracted spectrum, 
            // which is in polar coords at this point (for equal weighting of all frequencies).
            #region Grid search
            {
                Image CTFMeanPolarTrimmed = CTFMean.AsPolar((uint) MinFreqInclusive, (uint) MaxFreqExclusive);

                // Subtract current background.
                Image CurrentBackground = new Image(_SimulatedBackground.Interp(PS1D.Select(p => p.X).ToArray()).Skip(MinFreqInclusive).Take(NFreq).ToArray());
                CTFMeanPolarTrimmed.SubtractFromLines(CurrentBackground);
                CurrentBackground.FreeDevice();

                // Normalize for CC (not strictly needed, but it's converted for fp16 later, so let's be on the safe side of the fp16 range.
                GPU.Normalize(CTFMeanPolarTrimmed.GetDevice(Intent.Read), CTFMeanPolarTrimmed.GetDevice(Intent.Write), (uint) CTFMeanPolarTrimmed.ElementsReal, 1);

                CTF StartParams = new CTF
                {
                    PixelSize = (MainWindow.Options.CTFPixelMin + MainWindow.Options.CTFPixelMax) * 0.5M,
                    PixelSizeDelta = Math.Abs(MainWindow.Options.CTFPixelMax - MainWindow.Options.CTFPixelMin),
                    PixelSizeAngle = MainWindow.Options.CTFPixelAngle,

                    Defocus = (MainWindow.Options.CTFZMin + MainWindow.Options.CTFZMax) * 0.5M,
                    DefocusDelta = doastigmatism ? 0 : MainWindow.Options.CTFAstigmatism,
                    DefocusAngle = doastigmatism ? 0 : MainWindow.Options.CTFAstigmatismAngle,

                    Cs = MainWindow.Options.CTFCs,
                    Voltage = MainWindow.Options.CTFVoltage,
                    Amplitude = MainWindow.Options.CTFAmplitude
                };

                CTFFitStruct FitParams = new CTFFitStruct
                {
                    Defocus = new float3((float) (MainWindow.Options.CTFZMin - StartParams.Defocus) * 1e-6f,
                                         (float) (MainWindow.Options.CTFZMax - StartParams.Defocus) * 1e-6f,
                                         0.1e-6f),

                    Defocusdelta = doastigmatism ? new float3(0, 0.8e-6f, 0.05e-6f) : new float3(0, 0, 0),
                    Astigmatismangle = doastigmatism ? new float3(0, 2 * (float)Math.PI, 2 * (float)Math.PI / 18) : new float3(0, 0, 0),
                    Phaseshift = MainWindow.Options.CTFDoPhase ? new float3(0, (float) Math.PI, 0.05f * (float) Math.PI) : new float3(0, 0, 0)
                };

                CTFStruct ResultStruct = GPU.CTFFitMean(CTFMeanPolarTrimmed.GetDevice(Intent.Read),
                                                        CTFCoordsPolarTrimmed.GetDevice(Intent.Read),
                                                        CTFMeanPolarTrimmed.DimsSlice,
                                                        StartParams.ToStruct(),
                                                        FitParams,
                                                        doastigmatism);
                CTF.FromStruct(ResultStruct);

                UpdateRotationalAverage(true); // This doesn't have a nice background yet.
                UpdateBackgroundFit(); // Now get a reasonably nice background.

                UpdateRotationalAverage(true); // This time, with the nice background.
                UpdateBackgroundFit(); // Make the background even nicer!
            }
            #endregion

            // Do BFGS optimization of defocus, astigmatism and phase shift,
            // using 2D simulation for comparison
            #region BFGS

            bool[] CTFSpectraConsider = new bool[CTFSpectraGrid.Elements()];
            for (int i = 0; i < CTFSpectraConsider.Length; i++)
                CTFSpectraConsider[i] = true;
            int NCTFSpectraConsider = CTFSpectraConsider.Length;

            GridCTF = new CubicGrid(GridCTF.Dimensions, (float)CTF.Defocus, (float)CTF.Defocus, Dimension.X);

            for (int preciseFit = 0; preciseFit < 3; preciseFit++)
            {
                Image CTFSpectraPolarTrimmed = CTFSpectra.AsPolar((uint)MinFreqInclusive, (uint)MaxFreqExclusive);
                CTFSpectra.FreeDevice();    // This will only be needed again for the final PS1D.

                #region Create background and scale
                float[] CurrentScale = _SimulatedScale.Interp(PS1D.Select(p => p.X).ToArray());
                
                Image CTFSpectraScale = new Image(new int3(NFreq, DimsRegion.X, 1));
                float[] CTFSpectraScaleData = CTFSpectraScale.GetHost(Intent.Write)[0];

                // Trim polar to relevant frequencies, and populate coordinates.
                Parallel.For(0, DimsRegion.X, y =>
                {
                    float Angle = ((float) y / DimsRegion.X + 0.5f) * (float) Math.PI;
                    float Ny = 1f / ((PixelSize + PixelDelta * (float) Math.Cos(2.0 * (Angle - PixelAngle))) * DimsRegion.X);
                    for (int x = 0; x < NFreq; x++)
                    {
                        CTFSpectraScaleData[y * NFreq + x] = CurrentScale[x + MinFreqInclusive];
                    }
                });

                // Background is just 1 line since we're in polar.
                Image CurrentBackground = new Image(_SimulatedBackground.Interp(PS1D.Select(p => p.X).ToArray()).Skip(MinFreqInclusive).Take(NFreq).ToArray());
                #endregion
                
                CTFSpectraPolarTrimmed.SubtractFromLines(CurrentBackground);
                CurrentBackground.FreeDevice();

                // Normalize background-subtracted spectra.
                GPU.Normalize(CTFSpectraPolarTrimmed.GetDevice(Intent.Read),
                                 CTFSpectraPolarTrimmed.GetDevice(Intent.Write),
                                 (uint) CTFSpectraPolarTrimmed.ElementsSliceReal,
                                 (uint) CTFSpectraGrid.Elements());

                #region Convert to fp16
                Image CTFSpectraPolarTrimmedHalf = CTFSpectraPolarTrimmed.AsHalf();
                CTFSpectraPolarTrimmed.FreeDevice();
                
                Image CTFSpectraScaleHalf = CTFSpectraScale.AsHalf();
                CTFSpectraScale.FreeDevice();
                Image CTFCoordsPolarTrimmedHalf = CTFCoordsPolarTrimmed.AsHalf();
                #endregion

                // Wiggle weights show how the defocus on the spectra grid is altered 
                // by changes in individual anchor points of the spline grid.
                // They are used later to compute the dScore/dDefocus values for each spectrum 
                // only once, and derive the values for each anchor point from them.
                float[][] WiggleWeights = GridCTF.GetWiggleWeights(CTFSpectraGrid, new float3(OverlapFraction, OverlapFraction, 0f));

                // Helper method for getting CTFStructs for the entire spectra grid.
                Func<double[], CTF, float[], CTFStruct[]> EvalGetCTF = (input, ctf, defocusValues) =>
                {
                    decimal AlteredDelta = (decimal)input[input.Length - 3];
                    decimal AlteredAngle = (decimal)(input[input.Length - 2] * 20 / (Math.PI / 180));
                    decimal AlteredPhase = (decimal)input[input.Length - 1];

                    CTF Local = ctf.GetCopy();
                    Local.DefocusDelta = AlteredDelta;
                    Local.DefocusAngle = AlteredAngle;
                    if (MainWindow.Options.CTFDoPhase)
                        Local.PhaseShift = AlteredPhase;

                    CTFStruct LocalStruct = Local.ToStruct();
                    CTFStruct[] LocalParams = new CTFStruct[defocusValues.Length];
                    for (int i = 0; i < LocalParams.Length; i++)
                    {
                        LocalParams[i] = LocalStruct;
                        LocalParams[i].Defocus = defocusValues[i] * -1e-6f;
                    }

                    return LocalParams;
                };

                // Simulate with adjusted CTF, compare to originals
                #region Eval and Gradient methods
                Func<double[], double> Eval = input =>
                {
                    CubicGrid Altered = new CubicGrid(GridCTF.Dimensions, input.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v).ToArray());
                    float[] DefocusValues = Altered.GetInterpolatedNative(CTFSpectraGrid, new float3(OverlapFraction, OverlapFraction, 0f));
                    CTFStruct[] LocalParams = EvalGetCTF(input, CTF, DefocusValues);

                    float[] Result = new float[LocalParams.Length];

                    GPU.CTFCompareToSim(CTFSpectraPolarTrimmedHalf.GetDevice(Intent.Read),
                                        CTFCoordsPolarTrimmedHalf.GetDevice(Intent.Read),
                                        CTFSpectraScaleHalf.GetDevice(Intent.Read),
                                        (uint)CTFSpectraPolarTrimmedHalf.ElementsSliceReal,
                                        LocalParams,
                                        Result,
                                        (uint)LocalParams.Length);
                        
                    float Score = 0;
                    for (int i = 0; i < Result.Length; i++)
                        if (CTFSpectraConsider[i])
                            Score += Result[i];

                    Score /= NCTFSpectraConsider;

                    return (1.0 - Score) * 1000.0;
                };

                Func<double[], double[]> Gradient = input =>
                {
                    const float Step = 0.005f;
                    double[] Result = new double[input.Length];

                    // In 0D grid case, just get gradient for all 4 parameters.
                    // In 1+D grid case, do simple gradient for astigmatism and phase...
                    int StartComponent = input.Length - 3;
                    //int StartComponent = 0;
                    for (int i = StartComponent; i < input.Length; i++)
                    {
                        double[] UpperInput = new double[input.Length];
                        input.CopyTo(UpperInput, 0);
                        UpperInput[i] += Step;
                        double UpperValue = Eval(UpperInput);

                        double[] LowerInput = new double[input.Length];
                        input.CopyTo(LowerInput, 0);
                        LowerInput[i] -= Step;
                        double LowerValue = Eval(LowerInput);

                        Result[i] = (UpperValue - LowerValue) / (2f * Step);
                    }

                    // ... and take shortcut for defoci.
                    float[] ResultPlus = new float[CTFSpectraGrid.Elements()];
                    float[] ResultMinus = new float[CTFSpectraGrid.Elements()];

                    {
                        CubicGrid AlteredPlus = new CubicGrid(GridCTF.Dimensions, input.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v + Step).ToArray());
                        float[] DefocusValues = AlteredPlus.GetInterpolatedNative(CTFSpectraGrid, new float3(OverlapFraction, OverlapFraction, 0f));
                        CTFStruct[] LocalParams = EvalGetCTF(input, CTF, DefocusValues);

                        GPU.CTFCompareToSim(CTFSpectraPolarTrimmedHalf.GetDevice(Intent.Read),
                                            CTFCoordsPolarTrimmedHalf.GetDevice(Intent.Read),
                                            CTFSpectraScaleHalf.GetDevice(Intent.Read),
                                            (uint)CTFSpectraPolarTrimmedHalf.ElementsSliceReal,
                                            LocalParams,
                                            ResultPlus,
                                            (uint)LocalParams.Length);
                    }
                    {
                        CubicGrid AlteredMinus = new CubicGrid(GridCTF.Dimensions, input.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v - Step).ToArray());
                        float[] DefocusValues = AlteredMinus.GetInterpolatedNative(CTFSpectraGrid, new float3(OverlapFraction, OverlapFraction, 0f));
                        CTFStruct[] LocalParams = EvalGetCTF(input, CTF, DefocusValues);

                        GPU.CTFCompareToSim(CTFSpectraPolarTrimmedHalf.GetDevice(Intent.Read),
                                            CTFCoordsPolarTrimmedHalf.GetDevice(Intent.Read),
                                            CTFSpectraScaleHalf.GetDevice(Intent.Read),
                                            (uint)CTFSpectraPolarTrimmedHalf.ElementsSliceReal,
                                            LocalParams,
                                            ResultMinus,
                                            (uint)LocalParams.Length);
                    }
                    float[] LocalGradients = new float[ResultPlus.Length];
                    for (int i = 0; i < LocalGradients.Length; i++)
                        LocalGradients[i] = ResultMinus[i] - ResultPlus[i];

                    // Now compute gradients per grid anchor point using the precomputed individual gradients and wiggle factors.
                    Parallel.For(0, StartComponent, i => Result[i] = MathHelper.ReduceWeighted(LocalGradients, WiggleWeights[i]) / LocalGradients.Length / (2f * Step) * 1000f);

                    return Result;
                };
                #endregion

                #region Minimize first time with potential outpiers
                double[] StartParams = new double[GridCTF.Dimensions.Elements() + 3];
                for (int i = 0; i < GridCTF.Dimensions.Elements(); i++)
                    StartParams[i] = GridCTF.FlatValues[i];
                StartParams[StartParams.Length - 3] = (double)CTF.DefocusDelta;
                StartParams[StartParams.Length - 2] = (double)CTF.DefocusAngle / 20 * (Math.PI / 180);
                StartParams[StartParams.Length - 1] = (double)CTF.PhaseShift;

                // Compute correlation for individual spectra, and throw away those that are >.75 sigma worse than mean.
                #region Discard outliers

                if (CTFSpace || CTFTime)
                {
                    CubicGrid Altered = new CubicGrid(GridCTF.Dimensions, StartParams.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v).ToArray());
                    float[] DefocusValues = Altered.GetInterpolated(CTFSpectraGrid,
                                                                    new float3(OverlapFraction, OverlapFraction, 0f));
                    CTFStruct[] LocalParams = EvalGetCTF(StartParams, CTF, DefocusValues);

                    float[] Result = new float[LocalParams.Length];

                    GPU.CTFCompareToSim(CTFSpectraPolarTrimmedHalf.GetDevice(Intent.Read),
                                        CTFCoordsPolarTrimmedHalf.GetDevice(Intent.Read),
                                        CTFSpectraScaleHalf.GetDevice(Intent.Read),
                                        (uint)CTFSpectraPolarTrimmedHalf.ElementsSliceReal,
                                        LocalParams,
                                        Result,
                                        (uint)LocalParams.Length);

                    float MeanResult = MathHelper.Mean(Result);
                    float StdResult = MathHelper.StdDev(Result);
                    CTFSpectraConsider = new bool[CTFSpectraGrid.Elements()];
                    Parallel.For(0, CTFSpectraConsider.Length, i =>
                    {
                        if (Result[i] > MeanResult - StdResult * .75f)
                            CTFSpectraConsider[i] = true;
                        else
                        {
                            CTFSpectraConsider[i] = false;
                            for (int j = 0; j < WiggleWeights.Length; j++)
                                // Make sure the spectrum's gradient doesn't affect the overall gradient.
                                WiggleWeights[j][i] = 0;
                        }
                    });
                    NCTFSpectraConsider = CTFSpectraConsider.Where(v => v).Count();
                }

                #endregion

                BroydenFletcherGoldfarbShanno Optimizer = new BroydenFletcherGoldfarbShanno(StartParams.Length, Eval, Gradient)
                {
                    Past = 1,
                    Delta = 1e-6,
                    MaxLineSearch = 15,
                    Corrections = 20
                };
                Optimizer.Minimize(StartParams);
                #endregion

                #region Retrieve parameters
                CTF.Defocus = (decimal)MathHelper.Mean(Optimizer.Solution.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v));
                CTF.DefocusDelta = (decimal)Optimizer.Solution[StartParams.Length - 3];
                CTF.DefocusAngle = (decimal)(Optimizer.Solution[StartParams.Length - 2] * 20 / (Math.PI / 180));
                CTF.PhaseShift = (decimal)Optimizer.Solution[StartParams.Length - 1];

                GridCTF = new CubicGrid(GridCTF.Dimensions, Optimizer.Solution.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v).ToArray());
                #endregion

                // Dispose GPU resources manually because GC can't be bothered to do it in time.
                CTFSpectraPolarTrimmedHalf.FreeDevice();
                CTFCoordsPolarTrimmedHalf.FreeDevice();
                CTFSpectraScaleHalf.FreeDevice();

                #region Get nicer envelope fit
                {
                    if (!CTFSpace && !CTFTime)
                    {
                        UpdateRotationalAverage(true);
                    }
                    else
                    {
                        Image CTFSpectraBackground = new Image(new int3(DimsRegion), true);
                        float[] CTFSpectraBackgroundData = CTFSpectraBackground.GetHost(Intent.Write)[0];

                        // Construct background in Cartesian coordinates.
                        Helper.ForEachElementFT(DimsRegion, (x, y, xx, yy, r, a) =>
                        {
                            CTFSpectraBackgroundData[y * CTFSpectraBackground.DimsEffective.X + x] = _SimulatedBackground.Interp(r / DimsRegion.X);
                        });

                        CTFSpectra.SubtractFromSlices(CTFSpectraBackground);

                        float[] DefocusValues = GridCTF.GetInterpolated(CTFSpectraGrid, new float3(OverlapFraction, OverlapFraction, 0f));
                        CTFStruct[] LocalParams = DefocusValues.Select(v =>
                        {
                            CTF Local = CTF.GetCopy();
                            Local.Defocus = (decimal)v;

                            return Local.ToStruct();
                        }).ToArray();

                        Image CTFAverage1D = new Image(IntPtr.Zero, new int3(DimsRegion.X / 2, 1, 1));

                        GPU.CTFMakeAverage(CTFSpectra.GetDevice(Intent.Read),
                                           CTFCoordsCart.GetDevice(Intent.Read),
                                           (uint)CTFSpectra.ElementsSliceReal,
                                           (uint)DimsRegion.X,
                                           LocalParams,
                                           CTF.ToStruct(),
                                           0,
                                           (uint)DimsRegion.X / 2,
                                           CTFSpectraConsider.Select(v => v ? 1 : 0).ToArray(),
                                           (uint)CTFSpectraGrid.Elements(),
                                           CTFAverage1D.GetDevice(Intent.Write));

                        CTFSpectra.AddToSlices(CTFSpectraBackground);

                        float[] RotationalAverageData = CTFAverage1D.GetHost(Intent.Read)[0];
                        float2[] ForPS1D = new float2[PS1D.Length];
                        for (int i = 0; i < ForPS1D.Length; i++)
                            ForPS1D[i] = new float2((float)i / DimsRegion.X, (float)Math.Round(RotationalAverageData[i], 4) + _SimulatedBackground.Interp((float)i / DimsRegion.X));
                        PS1D = ForPS1D;

                        CTFSpectraBackground.FreeDevice();
                        CTFSpectra.FreeDevice();
                        CTFAverage1D.FreeDevice();
                    }

                    UpdateBackgroundFit();
                }
                #endregion
            }

            #endregion
            
            // Subtract background from 2D average and write it to disk. 
            // This image is used for quick visualization purposes only.
            #region PS2D update
            {
                int3 DimsAverage = new int3(DimsRegion.X, DimsRegion.X / 2, 1);
                float[] Average2DData = new float[DimsAverage.Elements()];
                float[] OriginalAverageData = CTFMean.GetHost(Intent.Read)[0];

                for (int y = 0; y < DimsAverage.Y; y++)
                {
                    int yy = y * y;
                    for (int x = 0; x < DimsAverage.Y; x++)
                    {
                        int xx = DimsRegion.X / 2 - x - 1;
                        xx *= xx;
                        float r = (float)Math.Sqrt(xx + yy) / DimsRegion.X;
                        Average2DData[y * DimsAverage.X + x] = OriginalAverageData[(y + DimsRegion.X / 2) * (DimsRegion.X / 2 + 1) + x] - SimulatedBackground.Interp(r);
                    }

                    for (int x = 0; x < DimsRegion.X / 2; x++)
                    {
                        int xx = x * x;
                        float r = (float)Math.Sqrt(xx + yy) / DimsRegion.X;
                        Average2DData[y * DimsAverage.X + x + DimsRegion.X / 2] = OriginalAverageData[(DimsRegion.X / 2 - y) * (DimsRegion.X / 2 + 1) + (DimsRegion.X / 2 - 1 - x)] - SimulatedBackground.Interp(r);
                    }
                }

                IOHelper.WriteMapFloat(PowerSpectrumPath,
                    new HeaderMRC
                    {
                        Dimensions = DimsAverage,
                        MinValue = MathHelper.Min(Average2DData),
                        MaxValue = MathHelper.Max(Average2DData)
                    },
                    Average2DData);
                OnPropertyChanged("PS2D");
            }
            #endregion

            for (int i = 0; i < PS1D.Length; i++)
                PS1D[i].Y -= SimulatedBackground.Interp(PS1D[i].X);
            SimulatedBackground = new Cubic1D(SimulatedBackground.Data.Select(v => new float2(v.X, 0f)).ToArray());

            CTFSpectra.FreeDevice();
            CTFMean.FreeDevice();
            CTFCoordsCart.FreeDevice();
            CTFCoordsPolarTrimmed.FreeDevice();

            OnPropertyChanged("Simulated1D");
            OnPropertyChanged("CTFQuality");

            SaveMeta();
        }

        public void ProcessShift(MapHeader originalHeader, Image originalStack)
        {
            // Deal with dimensions and grids.

            int NFrames = originalHeader.Dimensions.Z;
            int2 DimsImage = new int2(originalHeader.Dimensions);
            int2 DimsRegion = new int2(256, 256);

            float OverlapFraction = 0.25f;
            int2 DimsPositionGrid;
            int3[] PositionGrid = Helper.GetEqualGridSpacing(DimsImage, DimsRegion, OverlapFraction, out DimsPositionGrid);
            //PositionGrid = new[] { new int3(0, 0, 0) };
            //DimsPositionGrid = new int2(1, 1);
            int NPositions = PositionGrid.Length;

            int ShiftGridX = Math.Min(DimsPositionGrid.X, MainWindow.Options.GridMoveX);
            int ShiftGridY = Math.Min(DimsPositionGrid.Y, MainWindow.Options.GridMoveY);
            int ShiftGridZ = Math.Min(NFrames, MainWindow.Options.GridMoveZ);
            GridMovementX = new CubicGrid(new int3(ShiftGridX, ShiftGridY, ShiftGridZ));
            GridMovementY = new CubicGrid(new int3(ShiftGridX, ShiftGridY, ShiftGridZ));

            int3 ShiftGrid = new int3(DimsPositionGrid.X, DimsPositionGrid.Y, NFrames);

            int MinFreqInclusive = (int)(MainWindow.Options.MovementRangeMin * DimsRegion.X / 2);
            int MaxFreqExclusive = (int)(MainWindow.Options.MovementRangeMax * DimsRegion.X / 2);
            int NFreq = MaxFreqExclusive - MinFreqInclusive;

            int CentralFrame = NFrames / 2;

            int MaskExpansions = Math.Max(1, NFrames / 2);
            int[] MaskSizes = new int[MaskExpansions];

            // Allocate memory and create all prerequisites:
            int MaskLength;
            Image ShiftFactors;
            Image FreqWeights;
            float[][] FreqWeightSums = new float[MaskExpansions][];
            Image Phases;
            Image PhasesAverage;
            Image Shifts;
            {
                List<long> Positions = new List<long>();
                List<float2> Factors = new List<float2>();
                List<float2> Freq = new List<float2>();
                int Min2 = MinFreqInclusive * MinFreqInclusive;
                int Max2 = MaxFreqExclusive * MaxFreqExclusive;
                float PixelSize = (float)(MainWindow.Options.CTFPixelMin + MainWindow.Options.CTFPixelMax) * 0.5f;
                float PixelDelta = (float)(MainWindow.Options.CTFPixelMax - MainWindow.Options.CTFPixelMin) * 0.5f;
                float PixelAngle = (float)MainWindow.Options.CTFPixelAngle;

                for (int y = 0; y < DimsRegion.Y; y++)
                {
                    int yy = y - DimsRegion.X / 2;
                    for (int x = 0; x < DimsRegion.X / 2 + 1; x++)
                    {
                        int xx = x - DimsRegion.X / 2;
                        int r2 = xx * xx + yy * yy;
                        if (r2 >= Min2 && r2 < Max2)
                        {
                            Positions.Add(y * (DimsRegion.X / 2 + 1) + x);
                            Factors.Add(new float2((float)xx / DimsRegion.X * 2f * (float)Math.PI,
                                                   (float)yy / DimsRegion.X * 2f * (float)Math.PI));

                            float Angle = (float)Math.Atan2(yy, xx);
                            float r = (float)Math.Sqrt(r2);
                            Freq.Add(new float2(r, Angle));
                        }
                    }
                }

                // Sort everyone with ascending distance from center.
                List<KeyValuePair<float, int>> FreqIndices = Freq.Select((v, i) => new KeyValuePair<float, int>(v.X, i)).ToList();
                FreqIndices.Sort((a, b) => a.Key.CompareTo(b.Key));
                int[] SortedIndices = FreqIndices.Select(v => v.Value).ToArray();

                Helper.Reorder(Positions, SortedIndices);
                Helper.Reorder(Factors, SortedIndices);
                Helper.Reorder(Freq, SortedIndices);

                long[] RelevantMask = Positions.ToArray();
                Image ShiftFactorsFloat = new Image(Helper.ToInterleaved(Factors.ToArray()));
                MaskLength = RelevantMask.Length;
                ShiftFactors = new Image(IntPtr.Zero, new int3(MaskLength, 1, 1));

                // Get mask sizes for different expansion steps.
                for (int i = 0; i < MaskExpansions; i++)
                {
                    float CurrentMaxFreq = MinFreqInclusive + (MaxFreqExclusive - MinFreqInclusive) / (float)MaskExpansions * (i + 1);
                    MaskSizes[i] = Freq.Count(v => v.X * v.X < CurrentMaxFreq * CurrentMaxFreq);
                }

                GPU.SingleToHalf(ShiftFactorsFloat.GetDevice(Intent.Read), ShiftFactors.GetDevice(Intent.Write), ShiftFactorsFloat.ElementsReal);
                ShiftFactorsFloat.FreeDevice();

                Phases = new Image(IntPtr.Zero, new int3(MaskLength, DimsPositionGrid.X * DimsPositionGrid.Y, NFrames));

                GPU.CreateShift(originalStack.GetDevice(Intent.Read),
                                new int2(originalHeader.Dimensions),
                                originalHeader.Dimensions.Z,
                                PositionGrid,
                                PositionGrid.Length,
                                DimsRegion,
                                RelevantMask,
                                (uint)MaskLength,
                                Phases.GetDevice(Intent.Write));

                originalStack.FreeDevice();
                PhasesAverage = new Image(IntPtr.Zero, new int3(MaskLength, NPositions, 1), false, true, true);
                Shifts = new Image(new float[NPositions * NFrames * 2]);
            }

            int MinXSteps = 1, MinYSteps = 1;
            int MinZSteps = Math.Min(NFrames, 3);
            int3 ExpansionGridSize = new int3(MinXSteps, MinYSteps, MinZSteps);
            float[][] WiggleWeights = new CubicGrid(ExpansionGridSize).GetWiggleWeights(ShiftGrid, new float3(OverlapFraction, OverlapFraction, 0f));
            double[] StartParams = new double[ExpansionGridSize.Elements() * 2];

            for (int m = 0; m < MaskExpansions; m++)
            {
                double[] LastAverage = null;

                Action<double[]> SetPositions = input =>
                {
                    // Construct CubicGrids and get interpolated shift values.
                    CubicGrid AlteredGridX = new CubicGrid(ExpansionGridSize, input.Where((v, i) => i % 2 == 0).Select(v => (float)v).ToArray());
                    float[] AlteredX = AlteredGridX.GetInterpolatedNative(new int3(DimsPositionGrid.X, DimsPositionGrid.Y, NFrames), new float3(OverlapFraction, OverlapFraction, 0f));
                    CubicGrid AlteredGridY = new CubicGrid(ExpansionGridSize, input.Where((v, i) => i % 2 == 1).Select(v => (float)v).ToArray());
                    float[] AlteredY = AlteredGridY.GetInterpolatedNative(new int3(DimsPositionGrid.X, DimsPositionGrid.Y, NFrames), new float3(OverlapFraction, OverlapFraction, 0f));

                    // Let movement start at 0 in the central frame.
                    float2[] CenterFrameOffsets = new float2[NPositions];
                    for (int i = 0; i < NPositions; i++)
                        CenterFrameOffsets[i] = new float2(AlteredX[CentralFrame * NPositions + i], AlteredY[CentralFrame * NPositions + i]);
                    
                    // Finally, set the shift values in the device array.
                    float[] ShiftData = Shifts.GetHost(Intent.Write)[0];
                    Parallel.For(0, AlteredX.Length, i =>
                    {
                        ShiftData[i * 2] = AlteredX[i] - CenterFrameOffsets[i % NPositions].X;
                        ShiftData[i * 2 + 1] = AlteredY[i] - CenterFrameOffsets[i % NPositions].Y;
                    });
                };

                Action<double[]> DoAverage = input =>
                {
                    if (LastAverage == null || input.Where((t, i) => t != LastAverage[i]).Any())
                    {
                        SetPositions(input);
                        GPU.ShiftGetAverage(Phases.GetDevice(Intent.Read),
                                            PhasesAverage.GetDevice(Intent.Write),
                                            ShiftFactors.GetDevice(Intent.Read),
                                            (uint) MaskLength,
                                            (uint) MaskSizes[m],
                                            Shifts.GetDevice(Intent.Read),
                                            (uint) NPositions,
                                            (uint) NFrames);

                        if (LastAverage == null)
                            LastAverage = new double[input.Length];
                        Array.Copy(input, LastAverage, input.Length);
                    }
                };

                Func<double[], double> Eval = input =>
                {
                    DoAverage(input);

                    float[] Diff = new float[NPositions * NFrames];
                    GPU.ShiftGetDiff(Phases.GetDevice(Intent.Read),
                                     PhasesAverage.GetDevice(Intent.Read),
                                     ShiftFactors.GetDevice(Intent.Read),
                                     (uint)MaskLength,
                                     (uint)MaskSizes[m],
                                     Shifts.GetDevice(Intent.Read),
                                     Diff,
                                     (uint)NPositions,
                                     (uint)NFrames);

                    for (int i = 0; i < Diff.Length; i++)
                        Diff[i] = Diff[i] * 100f;// / FreqWeightSums[m][i % NPositions];

                    return MathHelper.Mean(Diff);
                };

                Func<double[], double[]> Grad = input =>
                {
                    DoAverage(input);

                    float[] Diff = new float[NPositions * NFrames * 2];
                    GPU.ShiftGetGrad(Phases.GetDevice(Intent.Read),
                                     PhasesAverage.GetDevice(Intent.Read),
                                     ShiftFactors.GetDevice(Intent.Read),
                                     (uint)MaskLength,
                                     (uint)MaskSizes[m],
                                     Shifts.GetDevice(Intent.Read),
                                     Diff,
                                     (uint)NPositions,
                                     (uint)NFrames);

                    for (int i = 0; i < Diff.Length; i++)
                        Diff[i] = Diff[i] * 100f;// / FreqWeightSums[m][i % NPositions];
                    
                    float[] DiffX = new float[NPositions * NFrames], DiffY = new float[NPositions * NFrames];
                    for (int i = 0; i < DiffX.Length; i++)
                    {
                        DiffX[i] = Diff[i * 2];
                        DiffY[i] = Diff[i * 2 + 1];
                    }

                    double[] Result = new double[input.Length];
                    Parallel.For(0, input.Length / 2, i =>
                    {
                        Result[i * 2] = MathHelper.ReduceWeighted(DiffX, WiggleWeights[i]);
                        Result[i * 2 + 1] = MathHelper.ReduceWeighted(DiffY, WiggleWeights[i]);
                    });
                    return Result;
                };

                BroydenFletcherGoldfarbShanno Optimizer = new BroydenFletcherGoldfarbShanno(StartParams.Length, Eval, Grad);
                Optimizer.Corrections = 20;
                Optimizer.Minimize(StartParams);

                float MeanX = MathHelper.Mean(Optimizer.Solution.Where((v, i) => i % 2 == 0).Select(v => (float)v));
                float MeanY = MathHelper.Mean(Optimizer.Solution.Where((v, i) => i % 2 == 1).Select(v => (float)v));
                for (int i = 0; i < ExpansionGridSize.Elements(); i++)
                {
                    Optimizer.Solution[i * 2] -= MeanX;
                    Optimizer.Solution[i * 2 + 1] -= MeanY;
                }

                // Store coarse values in grids.
                GridMovementX = new CubicGrid(ExpansionGridSize, Optimizer.Solution.Where((v, i) => i % 2 == 0).Select(v => (float)v).ToArray());
                GridMovementY = new CubicGrid(ExpansionGridSize, Optimizer.Solution.Where((v, i) => i % 2 == 1).Select(v => (float)v).ToArray());

                if (m < MaskExpansions - 1)
                {
                    // Refine sampling.
                    ExpansionGridSize = new int3((int)Math.Round((float)(ShiftGridX - MinXSteps) / (MaskExpansions - 1) * (m + 1) + MinXSteps),
                                                 (int)Math.Round((float)(ShiftGridY - MinYSteps) / (MaskExpansions - 1) * (m + 1) + MinYSteps), 
                                                 (int) Math.Round((float) (ShiftGridZ - MinZSteps) / (MaskExpansions - 1) * (m + 1) + MinZSteps));
                    WiggleWeights = new CubicGrid(ExpansionGridSize).GetWiggleWeights(ShiftGrid, new float3(OverlapFraction, OverlapFraction, 0f));

                    // Resize the grids to account for finer sampling.
                    GridMovementX = GridMovementX.Resize(ExpansionGridSize);
                    GridMovementY = GridMovementY.Resize(ExpansionGridSize);

                    // Construct start parameters for next optimization iteration.
                    StartParams = new double[ExpansionGridSize.Elements() * 2];
                    for (int i = 0; i < ExpansionGridSize.Elements(); i++)
                    {
                        StartParams[i * 2] = GridMovementX.FlatValues[i];
                        StartParams[i * 2 + 1] = GridMovementY.FlatValues[i];
                    }
                }
            }

            ShiftFactors.FreeDevice();
            Phases.FreeDevice();
            PhasesAverage.FreeDevice();
            Shifts.FreeDevice();

            // Center the shifts
            {
                float2[] AverageShifts = new float2[ShiftGridZ];
                for (int i = 0; i < AverageShifts.Length; i++)
                    AverageShifts[i] = new float2(MathHelper.Mean(GridMovementX.GetSliceXY(i)),
                                                  MathHelper.Mean(GridMovementY.GetSliceXY(i)));
                float2 CenterShift = MathHelper.Mean(AverageShifts);

                GridMovementX = new CubicGrid(GridMovementX.Dimensions, GridMovementX.FlatValues.Select(v => v - CenterShift.X).ToArray());
                GridMovementY = new CubicGrid(GridMovementY.Dimensions, GridMovementY.FlatValues.Select(v => v - CenterShift.Y).ToArray());
            }

            SaveMeta();
        }

        public void CreateCorrected(MapHeader originalHeader, Image originalStack, string pathShiftedStack)
        {
            {
                if (!Directory.Exists(AverageDir))
                    Directory.CreateDirectory(AverageDir);
                if (!Directory.Exists(CTFDir))
                    Directory.CreateDirectory(CTFDir);

                if (pathShiftedStack != null && !Directory.Exists(ShiftedStackDir))
                    Directory.CreateDirectory(ShiftedStackDir);
            }

            int3 Dims = originalStack.Dims;

            float PixelSize = (float)(MainWindow.Options.CTFPixelMin + MainWindow.Options.CTFPixelMax) * 0.5f;
            Image CTFCoords;
            {
                float2[] CTFCoordsData = new float2[Dims.ElementsSlice()];
                float PixelDelta = (float)(MainWindow.Options.CTFPixelMax - MainWindow.Options.CTFPixelMin) * 0.5f;
                float PixelAngle = (float)MainWindow.Options.CTFPixelAngle;
                Helper.ForEachElementFT(new int2(Dims), (x, y, xx, yy) =>
                {
                    float xs = xx / (float)Dims.X;
                    float ys = yy / (float)Dims.Y;
                    float r = (float)Math.Sqrt(xs * xs + ys * ys);
                    float angle = (float)(Math.Atan2(yy, xx) + Math.PI / 2.0);
                    float CurrentPixelSize = PixelSize + PixelDelta * (float)Math.Cos(2f * (angle - PixelAngle));

                    CTFCoordsData[y * (Dims.X / 2 + 1) + x] = new float2(r / CurrentPixelSize, angle);
                });

                CTFCoords = new Image(CTFCoordsData, Dims.Slice(), true);
                CTFCoords.RemapToFT();
            }
            Image CTFFreq = CTFCoords.AsReal();

            CubicGrid CollapsedMovementX = GridMovementX.CollapseXY();
            CubicGrid CollapsedMovementY = GridMovementY.CollapseXY();
            CubicGrid CollapsedCTF = GridCTF.CollapseXY();
            Image AverageFT = new Image(Dims.Slice(), true, true);
            Image AveragePS = new Image(Dims.Slice(), true, false);
            Image Weights = new Image(Dims.Slice(), true, false);
            Weights.Fill(1e-4f);

            for (int nframe = 0; nframe < Dims.Z; nframe++)
            {
                float StepZ = 1f / Math.Max(Dims.Z - 1, 1);

                Image PS = new Image(Dims.Slice(), true);
                PS.Fill(1f);

                // Apply motion blur filter.
                {
                    float StartZ = (nframe - 0.5f) * StepZ;
                    float StopZ = (nframe + 0.5f) * StepZ;

                    float2[] Shifts = new float2[21];
                    for (int z = 0; z < Shifts.Length; z++)
                    {
                        float zp = StartZ + (StopZ - StartZ) / (Shifts.Length - 1) * z;
                        Shifts[z] = new float2(CollapsedMovementX.GetInterpolated(new float3(0.5f, 0.5f, zp)),
                                               CollapsedMovementY.GetInterpolated(new float3(0.5f, 0.5f, zp)));
                    }
                    // Center the shifts around 0
                    float2 ShiftMean = MathHelper.Mean(Shifts);
                    Shifts = Shifts.Select(v => v - ShiftMean).ToArray();

                    Image MotionFilter = new Image(IntPtr.Zero, Dims.Slice(), true);
                    GPU.CreateMotionBlur(MotionFilter.GetDevice(Intent.Write), 
                                         MotionFilter.Dims, 
                                         Helper.ToInterleaved(Shifts.Select(v => new float3(v.X, v.Y, 0)).ToArray()), 
                                         (uint)Shifts.Length, 
                                         1);
                    PS.Multiply(MotionFilter);
                    //MotionFilter.WriteMRC("motion.mrc");
                    MotionFilter.Dispose();
                }

                // Apply CTF.
                if (CTF != null)
                {
                    CTF Altered = CTF.GetCopy();
                    Altered.Defocus = (decimal)CollapsedCTF.GetInterpolated(new float3(0.5f, 0.5f, nframe * StepZ));

                    Image CTFImage = new Image(IntPtr.Zero, Dims.Slice(), true);
                    GPU.CreateCTF(CTFImage.GetDevice(Intent.Write),
                                  CTFCoords.GetDevice(Intent.Read),
                                  (uint)CTFCoords.ElementsSliceComplex, 
                                  new[] { Altered.ToStruct() }, 
                                  false, 
                                  1);

                    PS.Multiply(CTFImage);
                    //CTFImage.WriteMRC("ctf.mrc");
                    CTFImage.Dispose();
                }

                // Apply dose weighting.
                {
                    float3 NikoConst = new float3(0.245f, -1.665f, 2.81f);

                    // Niko's formula expects e-/A2/frame, we've got e-/px/frame - convert!
                    float FrameDose = (float)MainWindow.Options.CorrectDosePerFrame * (nframe + 0.5f) / (PixelSize * PixelSize);

                    Image DoseImage = new Image(IntPtr.Zero, Dims.Slice(), true);
                    GPU.DoseWeighting(CTFFreq.GetDevice(Intent.Read),
                                      DoseImage.GetDevice(Intent.Write),
                                      (uint)DoseImage.ElementsSliceComplex,
                                      new[] { FrameDose },
                                      NikoConst,
                                      1);
                    PS.Multiply(DoseImage);
                    //DoseImage.WriteMRC("dose.mrc");
                    DoseImage.Dispose();
                }

                Image Frame = new Image(originalStack.GetHost(Intent.Read)[nframe], Dims.Slice());
                Frame.ShiftSlices(new[]
                {
                    new float3(CollapsedMovementX.GetInterpolated(new float3(0.5f, 0.5f, nframe * StepZ)),
                               CollapsedMovementY.GetInterpolated(new float3(0.5f, 0.5f, nframe * StepZ)), 
                               0f)
                });
                Image FrameFT = Frame.AsFFT();
                Frame.Dispose();

                Image PSSign = new Image(PS.GetDevice(Intent.Read), Dims.Slice(), true);
                PSSign.Sign();

                // Do phase flipping before averaging.
                FrameFT.Multiply(PSSign);
                PS.Multiply(PSSign);
                PSSign.Dispose();

                FrameFT.Multiply(PS);
                AverageFT.Add(FrameFT);
                Weights.Add(PS);

                //PS.WriteMRC("ps.mrc");

                PS.Multiply(PS);
                AveragePS.Add(PS);

                PS.Dispose();
                FrameFT.Dispose();

            }
            CTFCoords.Dispose();
            CTFFreq.Dispose();

            AverageFT.Divide(Weights);
            AveragePS.Divide(Weights);

            Image Average = AverageFT.AsIFFT();
            AverageFT.Dispose();

            MapHeader Header = originalHeader;
            Header.Dimensions = Dims.Slice();

            Average.WriteMRC(AveragePath);
            AveragePS.WriteMRC(CTFPath);
        }
    }

    public enum ProcessingStatus
    {
        Unprocessed,
        Outdated,
        Processed
    }
}
