using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.XPath;
using Accord.Math.Optimization;
using Warp.Headers;
using Warp.Stages;
using Warp.Tools;
using System.Threading;

namespace Warp
{
    public class Movie : DataBase
    {
        private string _Path = "";

        public string Path
        {
            get { return _Path; }
            set
            {
                if (value != _Path)
                {
                    _Path = value;
                    OnPropertyChanged();
                }
            }
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
        public string ParticlesDir => DirectoryName + "particles\\";
        public string ParticleCTFDir => DirectoryName + "particlectf\\";
        public string ParticleMoviesDir => DirectoryName + "particlemovies\\";
        public string ParticleCTFMoviesDir => DirectoryName + "particlectfmovies\\";

        public string PowerSpectrumPath => PowerSpectrumDir + RootName + ".mrc";
        public string CTFPath => CTFDir + RootName + ".mrc";
        public string AveragePath => AverageDir + RootName + ".mrc";
        public string ShiftedStackPath => ShiftedStackDir + RootName + "_movie.mrcs";
        public string ParticlesPath => ParticlesDir + RootName + "_particles.mrcs";
        public string ParticleCTFPath => ParticleCTFDir + RootName + "_particlectf.mrcs";
        public string ParticleMoviesPath => ParticleMoviesDir + RootName + "_particles.mrcs";
        public string ParticleCTFMoviesPath => ParticleCTFMoviesDir + RootName + "_particlectf.mrcs";

        public string XMLName => RootName + ".xml";
        public string XMLPath => DirectoryName + XMLName;

        public int ResidentDevice = 0;

        private ProcessingStatus _Status = ProcessingStatus.Unprocessed;

        public ProcessingStatus Status
        {
            get { return _Status; }
            set
            {
                if (value != _Status)
                {
                    _Status = value;
                    OnPropertyChanged();
                    SaveMeta();
                }
            }
        }

        public bool DoProcess
        {
            get { return _Status != ProcessingStatus.Skip; }
            set { Status = value ? ProcessingStatus.Unprocessed : ProcessingStatus.Skip; }
        }

        private int3 _Dimensions = new int3(1, 1, 1);

        public int3 Dimensions
        {
            get { return _Dimensions; }
            set
            {
                if (value != _Dimensions)
                {
                    _Dimensions = value;
                    OnPropertyChanged();
                }
            }
        }

        private CTF _CTF = new CTF();

        public CTF CTF
        {
            get { return _CTF; }
            set
            {
                if (value != _CTF)
                {
                    _CTF = value;
                    OnPropertyChanged();
                }
            }
        }

        private static ImageSource PS2DTemp;
        private static Movie PS2DTempOwner;

        public ImageSource PS2D
        {
            get
            {
                if (!File.Exists(PowerSpectrumPath))
                {
                    PS2DTemp = null;
                    PS2DTempOwner = null;
                    return null;
                }

                if (PS2DTempOwner == this && PS2DTemp != null)
                    return PS2DTemp;

                unsafe
                {
                    MapHeader Header = MapHeader.ReadFromFile(PowerSpectrumPath);
                    float[] Data = IOHelper.ReadSmallMapFloat(PowerSpectrumPath, new int2(1, 1), 0, typeof (float));

                    int Width = Header.Dimensions.X;
                    int HalfWidth = Width / 2;
                    int Height = Header.Dimensions.Y; // The usual x / 2 + 1

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

                        PS2DTemp = BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Indexed8, BitmapPalettes.Gray256, DataBytes, Width);
                        PS2DTempOwner = this;
                        return PS2DTemp;
                    }
                }
            }
        }

        private float2[] _PS1D;

        public float2[] PS1D
        {
            get { return _PS1D; }
            set
            {
                if (value != _PS1D)
                {
                    _PS1D = value;
                    OnPropertyChanged();
                    CTFQuality = GetCTFQuality();
                }
            }
        }

        private CubicGrid _GridCTF = new CubicGrid(new int3(1, 1, 1));

        public CubicGrid GridCTF
        {
            get { return _GridCTF; }
            set
            {
                if (value != _GridCTF)
                {
                    _GridCTF = value;
                    OnPropertyChanged();
                }
            }
        }

        private CubicGrid _GridCTFPhase = new CubicGrid(new int3(1, 1, 1));

        public CubicGrid GridCTFPhase
        {
            get { return _GridCTFPhase; }
            set
            {
                if (value != _GridCTFPhase)
                {
                    _GridCTFPhase = value;
                    OnPropertyChanged();
                }
            }
        }

        private CubicGrid _GridMovementX = new CubicGrid(new int3(1, 1, 1));

        public CubicGrid GridMovementX
        {
            get { return _GridMovementX; }
            set
            {
                if (value != _GridMovementX)
                {
                    _GridMovementX = value;
                    OnPropertyChanged();
                }
            }
        }

        private CubicGrid _GridMovementY = new CubicGrid(new int3(1, 1, 1));

        public CubicGrid GridMovementY
        {
            get { return _GridMovementY; }
            set
            {
                if (value != _GridMovementY)
                {
                    _GridMovementY = value;
                    OnPropertyChanged();
                }
            }
        }

        private CubicGrid _GridLocalX = new CubicGrid(new int3(1, 1, 1));

        public CubicGrid GridLocalX
        {
            get { return _GridLocalX; }
            set
            {
                if (value != _GridLocalX)
                {
                    _GridLocalX = value;
                    OnPropertyChanged();
                }
            }
        }

        private CubicGrid _GridLocalY = new CubicGrid(new int3(1, 1, 1));

        public CubicGrid GridLocalY
        {
            get { return _GridLocalY; }
            set
            {
                if (value != _GridLocalY)
                {
                    _GridLocalY = value;
                    OnPropertyChanged();
                }
            }
        }

        private List<CubicGrid> _PyramidShiftX = new List<CubicGrid>();

        public List<CubicGrid> PyramidShiftX
        {
            get { return _PyramidShiftX; }
            set
            {
                if (value != _PyramidShiftX)
                {
                    _PyramidShiftX = value;
                    OnPropertyChanged();
                }
            }
        }

        private List<CubicGrid> _PyramidShiftY = new List<CubicGrid>();

        public List<CubicGrid> PyramidShiftY
        {
            get { return _PyramidShiftY; }
            set
            {
                if (value != _PyramidShiftY)
                {
                    _PyramidShiftY = value;
                    OnPropertyChanged();
                }
            }
        }

        private static ImageSource TempSimulated2D;
        private static Movie TempSimulated2DOwner;

        public ImageSource Simulated2D
        {
            get
            {
                if (TempSimulated2DOwner == this && TempSimulated2D != null)
                    return TempSimulated2D;

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

                    TempSimulated2D = BitmapSource.Create(Width, Width, 96, 96, PixelFormats.Indexed8, BitmapPalettes.Gray256, Sim2DBytes, Width);
                    TempSimulated2DOwner = this;
                    return TempSimulated2D;
                }
            }
        }

        private float2[] _Simulated1D;

        public float2[] Simulated1D
        {
            get { return _Simulated1D ?? (_Simulated1D = GetSimulated1D()); }
            set
            {
                if (value != _Simulated1D)
                {
                    _Simulated1D = value;
                    OnPropertyChanged();
                }
            }
        }

        private float2[] GetSimulated1D()
        {
            if (PS1D == null || _SimulatedBackground == null)
                return null;

            float[] SimulatedCTF = CTF.Get1D(PS1D.Length, true);

            float2[] Result = new float2[PS1D.Length];
            for (int i = 0; i < Result.Length; i++)
                Result[i] = new float2(PS1D[i].X, _SimulatedBackground.Interp(PS1D[i].X) + SimulatedCTF[i] * _SimulatedScale.Interp(PS1D[i].X));

            return Result;
        }

        private Cubic1D _SimulatedBackground;

        public Cubic1D SimulatedBackground
        {
            get { return _SimulatedBackground; }
            set
            {
                if (value != _SimulatedBackground)
                {
                    _SimulatedBackground = value;
                    OnPropertyChanged();
                }
            }
        }

        private Cubic1D _SimulatedScale = new Cubic1D(new[] { new float2(0, 1), new float2(1, 1) });

        public Cubic1D SimulatedScale
        {
            get { return _SimulatedScale; }
            set
            {
                if (value != _SimulatedScale)
                {
                    _SimulatedScale = value;
                    OnPropertyChanged();
                }
            }
        }

        private static ImageSource TempAverageImage;
        private static Movie TempAverageImageOwner;

        public ImageSource AverageImage
        {
            get
            {
                if (!File.Exists(AveragePath))
                {
                    TempAverageImage = null;
                    TempAverageImageOwner = null;
                    return null;
                }

                if (TempAverageImageOwner == this && TempAverageImage != null)
                    return TempAverageImage;

                unsafe
                {
                    MapHeader Header = null;
                    float[] Data = null;
                    int Attempts = 0;

                    while (Attempts < 10)
                    {
                        try
                        {
                            Header = MapHeader.ReadFromFile(AveragePath);
                            Data = IOHelper.ReadSmallMapFloat(AveragePath, new int2(1, 1), 0, typeof (float));
                            break;
                        }
                        catch (Exception)
                        {
                            Header = new HeaderMRC();
                            Header.Dimensions = new int3(1, 1, 1);
                            Data = new float[1];

                            Attempts++;
                        }
                    }

                    int Width = Header.Dimensions.X;
                    int Height = Header.Dimensions.Y;
                    int Elements = Width * Height;

                    double Sum = 0, Sum2 = 0;

                    fixed (float* DataPtr = Data)
                    {
                        float* DataP = DataPtr;
                        for (int i = 0; i < Elements; i++)
                        {
                            double Val = *DataP++;
                            Sum += Val;
                            Sum2 += Val * Val;
                        }

                        float Mean = (float)(Sum / Elements);
                        float StdDev = (float)Math.Sqrt(Elements * Sum2 - Sum * Sum) / Elements;
                        if (StdDev == 0f)
                            return BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Indexed8, BitmapPalettes.Gray256, new byte[Data.Length], Width);

                        float Min = Mean - 2.0f * StdDev;
                        float Range = 3.5f * StdDev;

                        byte[] DataBytes = new byte[Data.Length];
                        fixed (byte* DataBytesPtr = DataBytes)
                        {
                            for (int i = 0; i < Elements; i++)
                                DataBytesPtr[i] = (byte)(Math.Max(Math.Min(1f, (DataPtr[i] - (Mean - Min)) / Range), 0f) * 255f);
                        }

                        TempAverageImage = BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Indexed8, BitmapPalettes.Gray256, DataBytes, Width);
                        TempAverageImageOwner = this;
                        return TempAverageImage;
                    }
                }
            }
        }

        private float2[] _CTFQuality = null;

        public float2[] CTFQuality
        {
            get { return _CTFQuality ?? (_CTFQuality = GetCTFQuality()); }
            set
            {
                if (value != _CTFQuality)
                {
                    _CTFQuality = value;
                    OnPropertyChanged();
                }
            }
        }

        private float2[] GetCTFQuality()
        {
            // Calculate the correlation between experimental and simulated peaks.
            // If there is no single complete peak (i. e. <=1 zeros), just take the entire
            // fitted range and treat it as one peak.
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

        public Movie(string path)
        {
            Path = path;
            CTF.PropertyChanged += CTF_PropertyChanged;
            MainWindow.Options.PropertyChanged += Options_PropertyChanged;

            LoadMeta();

            MapHeader OriginalHeader = MapHeader.ReadFromFile(Path,
                                                              new int2(MainWindow.Options.InputDatWidth, MainWindow.Options.InputDatHeight),
                                                              MainWindow.Options.InputDatOffset,
                                                              ImageFormatsHelper.StringToType(MainWindow.Options.InputDatType));
            Dimensions = OriginalHeader.Dimensions;
        }

        private void CTF_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (MainWindow.Options.DisplayedMovie == this)
            {
                TempSimulated2D = null;
                OnPropertyChanged("Simulated2D");
                Simulated1D = GetSimulated1D();
                CTFQuality = GetCTFQuality();

                SaveMeta();
            }
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

        public virtual void LoadMeta()
        {
            if (!File.Exists(XMLPath))
                return;

            using (Stream SettingsStream = File.OpenRead(XMLPath))
            {
                XPathDocument Doc = new XPathDocument(SettingsStream);
                XPathNavigator Reader = Doc.CreateNavigator();
                Reader.MoveToRoot();
                Reader.MoveToFirstChild();

                {
                    string StatusString = Reader.GetAttribute("Status", "");
                    if (StatusString != null && StatusString.Length > 0)
                    {
                        switch (StatusString)
                        {
                            case "Processed":
                                _Status = ProcessingStatus.Processed;
                                break;
                            case "Outdated":
                                _Status = ProcessingStatus.Outdated;
                                break;
                            case "Unprocessed":
                                _Status = ProcessingStatus.Unprocessed;
                                break;
                            case "Skip":
                                _Status = ProcessingStatus.Skip;
                                break;
                        }
                    }
                }

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

                XPathNavigator NavGridCTFPhase = Reader.SelectSingleNode("//GridCTFPhase");
                if (NavGridCTFPhase != null)
                    GridCTFPhase = CubicGrid.Load(NavGridCTFPhase);

                XPathNavigator NavMoveX = Reader.SelectSingleNode("//GridMovementX");
                if (NavMoveX != null)
                    GridMovementX = CubicGrid.Load(NavMoveX);

                XPathNavigator NavMoveY = Reader.SelectSingleNode("//GridMovementY");
                if (NavMoveY != null)
                    GridMovementY = CubicGrid.Load(NavMoveY);

                XPathNavigator NavLocalX = Reader.SelectSingleNode("//GridLocalMovementX");
                if (NavLocalX != null)
                    GridLocalX = CubicGrid.Load(NavLocalX);

                XPathNavigator NavLocalY = Reader.SelectSingleNode("//GridLocalMovementY");
                if (NavLocalY != null)
                    GridLocalY = CubicGrid.Load(NavLocalY);

                PyramidShiftX.Clear();
                foreach (XPathNavigator NavShiftX in Reader.Select("//PyramidShiftX"))
                    PyramidShiftX.Add(CubicGrid.Load(NavShiftX));

                PyramidShiftY.Clear();
                foreach (XPathNavigator NavShiftY in Reader.Select("//PyramidShiftY"))
                    PyramidShiftY.Add(CubicGrid.Load(NavShiftY));
            }
        }

        public virtual void SaveMeta()
        {
            using (XmlTextWriter Writer = new XmlTextWriter(XMLPath, Encoding.Unicode))
            {
                Writer.Formatting = Formatting.Indented;
                Writer.IndentChar = '\t';
                Writer.Indentation = 1;
                Writer.WriteStartDocument();
                Writer.WriteStartElement("Movie");

                Writer.WriteAttributeString("Status", Status.ToString());

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
                                                   _SimulatedBackground.Data.Select(v => v.X.ToString(CultureInfo.InvariantCulture) +
                                                                                         "|" +
                                                                                         v.Y.ToString(CultureInfo.InvariantCulture))));
                    Writer.WriteEndElement();
                }

                if (SimulatedScale != null)
                {
                    Writer.WriteStartElement("SimulatedScale");
                    Writer.WriteString(string.Join(";",
                                                   _SimulatedScale.Data.Select(v => v.X.ToString(CultureInfo.InvariantCulture) +
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

                Writer.WriteStartElement("GridCTFPhase");
                GridCTFPhase.Save(Writer);
                Writer.WriteEndElement();

                Writer.WriteStartElement("GridMovementX");
                GridMovementX.Save(Writer);
                Writer.WriteEndElement();

                Writer.WriteStartElement("GridMovementY");
                GridMovementY.Save(Writer);
                Writer.WriteEndElement();

                Writer.WriteStartElement("GridLocalMovementX");
                GridLocalX.Save(Writer);
                Writer.WriteEndElement();

                Writer.WriteStartElement("GridLocalMovementY");
                GridLocalY.Save(Writer);
                Writer.WriteEndElement();

                foreach (var grid in PyramidShiftX)
                {
                    Writer.WriteStartElement("PyramidShiftX");
                    grid.Save(Writer);
                    Writer.WriteEndElement();
                }

                foreach (var grid in PyramidShiftY)
                {
                    Writer.WriteStartElement("PyramidShiftY");
                    grid.Save(Writer);
                    Writer.WriteEndElement();
                }

                Writer.WriteEndElement();
                Writer.WriteEndDocument();
            }
        }

        public virtual void ProcessCTF(MapHeader originalHeader, Image originalStack, bool doastigmatism, decimal scaleFactor)
        {
            if (!Directory.Exists(PowerSpectrumDir))
                Directory.CreateDirectory(PowerSpectrumDir);

            //CTF = new CTF();
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
            GridCTFPhase = new CubicGrid(new int3(1, 1, CTFGridZ));

            bool CTFSpace = CTFGridX * CTFGridY > 1;
            bool CTFTime = CTFGridZ > 1;
            int3 CTFSpectraGrid = new int3(CTFSpace ? DimsPositionGrid.X : 1,
                                           CTFSpace ? DimsPositionGrid.Y : 1,
                                           CTFTime ? CTFGridZ : 1);

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
                              CTFSpectraGrid,
                              CTFSpectra.GetDevice(Intent.Write),
                              CTFMean.GetDevice(Intent.Write));
            originalStack.FreeDevice(); // Won't need it in this method anymore.

            #endregion

            // Populate address arrays for later.

            #region Init addresses

            {
                float2[] CoordsData = new float2[CTFCoordsCart.ElementsSliceComplex];

                Helper.ForEachElementFT(DimsRegion, (x, y, xx, yy, r, a) => CoordsData[y * (DimsRegion.X / 2 + 1) + x] = new float2(r, a));
                CTFCoordsCart.UpdateHostWithComplex(new[] { CoordsData });

                CoordsData = new float2[NFreq * DimsRegion.X];
                Helper.ForEachElement(CTFCoordsPolarTrimmed.DimsSlice, (x, y) =>
                {
                    float Angle = ((float)y / DimsRegion.X + 0.5f) * (float)Math.PI;
                    float Ny = 1f / DimsRegion.X;
                    CoordsData[y * NFreq + x] = new float2((x + MinFreqInclusive) * Ny, Angle);
                });
                CTFCoordsPolarTrimmed.UpdateHostWithComplex(new[] { CoordsData });
            }

            #endregion

            // Retrieve average 1D spectrum from CTFMean (not corrected for astigmatism yet).

            #region Initial 1D spectrum

            {
                Image CTFAverage1D = new Image(IntPtr.Zero, new int3(DimsRegion.X / 2, 1, 1));

                GPU.CTFMakeAverage(CTFMean.GetDevice(Intent.Read),
                                   CTFCoordsCart.GetDevice(Intent.Read),
                                   (uint)CTFMean.ElementsSliceReal,
                                   (uint)DimsRegion.X,
                                   new[] { new CTF().ToStruct() },
                                   new CTF().ToStruct(),
                                   0,
                                   (uint)DimsRegion.X / 2,
                                   null,
                                   1,
                                   CTFAverage1D.GetDevice(Intent.Write));

                //CTFAverage1D.WriteMRC("CTFAverage1D.mrc");

                float[] CTFAverage1DData = CTFAverage1D.GetHost(Intent.Read)[0];
                float2[] ForPS1D = new float2[DimsRegion.X / 2];
                for (int i = 0; i < ForPS1D.Length; i++)
                    ForPS1D[i] = new float2((float)i / DimsRegion.X, (float)Math.Round(CTFAverage1DData[i], 4));
                _PS1D = ForPS1D;

                CTFAverage1D.Dispose();
            }

            #endregion

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
                MathHelper.UnNaN(ForPS1D);

                _PS1D = ForPS1D;

                CTFMeanCorrected.Dispose();
                CTFAverage1D.Dispose();
            };

            #endregion

            // Fit background to currently best average (not corrected for astigmatism yet).
            {
                float2[] ForPS1D = PS1D.Skip(MinFreqInclusive).Take(Math.Max(2, NFreq / 2)).ToArray();

                int NumNodes = Math.Max(3, (int)((MainWindow.Options.CTFRangeMax - MainWindow.Options.CTFRangeMin) * 5M));
                _SimulatedBackground = Cubic1D.Fit(ForPS1D, NumNodes); // This won't fit falloff and scale, because approx function is 0

                float[] CurrentBackground = _SimulatedBackground.Interp(PS1D.Select(p => p.X).ToArray()).Skip(MinFreqInclusive).Take(NFreq / 2).ToArray();
                float[] Subtracted1D = new float[ForPS1D.Length];
                for (int i = 0; i < ForPS1D.Length; i++)
                    Subtracted1D[i] = ForPS1D[i].Y - CurrentBackground[i];
                MathHelper.NormalizeInPlace(Subtracted1D);

                float ZMin = (float)MainWindow.Options.CTFZMin;
                float ZMax = (float)MainWindow.Options.CTFZMax;
                float ZStep = (ZMax - ZMin) / 100f;

                float BestZ = 0, BestPhase = 0, BestScore = -999;
                for (float z = ZMin; z <= ZMax + 1e-5f; z += ZStep)
                {
                    for (float p = 0; p <= (MainWindow.Options.CTFDoPhase ? 1f : 0f); p += 0.01f)
                    {
                        CTF CurrentParams = new CTF
                        {
                            PixelSize = (MainWindow.Options.CTFPixelMin + MainWindow.Options.CTFPixelMax) * 0.5M,

                            Defocus = (decimal)z,
                            PhaseShift = (decimal)p,

                            Cs = MainWindow.Options.CTFCs,
                            Voltage = MainWindow.Options.CTFVoltage,
                            Amplitude = MainWindow.Options.CTFAmplitude
                        };
                        float[] SimulatedCTF = CurrentParams.Get1D(PS1D.Length, true).Skip(MinFreqInclusive).Take(Math.Max(2, NFreq / 2)).ToArray();
                        MathHelper.NormalizeInPlace(SimulatedCTF);
                        float Score = MathHelper.CrossCorrelate(Subtracted1D, SimulatedCTF);
                        if (Score > BestScore)
                        {
                            BestScore = Score;
                            BestZ = z;
                            BestPhase = p;
                        }
                    }
                }

                CTF = new CTF
                {
                    PixelSize = (MainWindow.Options.CTFPixelMin + MainWindow.Options.CTFPixelMax) * 0.5M,

                    Defocus = (decimal)BestZ,
                    PhaseShift = (decimal)BestPhase,

                    Cs = MainWindow.Options.CTFCs,
                    Voltage = MainWindow.Options.CTFVoltage,
                    Amplitude = MainWindow.Options.CTFAmplitude
                };

                UpdateRotationalAverage(true); // This doesn't have a nice background yet.
                UpdateBackgroundFit(); // Now get a reasonably nice background.
            }

            

            // Fit defocus, (phase shift), (astigmatism) to average background-subtracted spectrum, 
            // which is in polar coords at this point (for equal weighting of all frequencies).

            #region Grid search

            {
                Image CTFMeanPolarTrimmed = CTFMean.AsPolar((uint)MinFreqInclusive, (uint)(MinFreqInclusive + NFreq / 1));

                // Subtract current background.
                Image CurrentBackground = new Image(_SimulatedBackground.Interp(PS1D.Select(p => p.X).ToArray()).Skip(MinFreqInclusive).Take(NFreq / 1).ToArray());
                CTFMeanPolarTrimmed.SubtractFromLines(CurrentBackground);
                CurrentBackground.Dispose();

                /*Image WaterMask = new Image(new int3(NFreq, 1, 1));
                float[] WaterData = WaterMask.GetHost(Intent.Write)[0];
                for (int i = 0; i < NFreq; i++)
                {
                    float f = (i + MinFreqInclusive) / (float)DimsRegion.X * 2f;
                    WaterData[i] = f > 0.2f && f < 0.6f ? 0f : 1f;
                }
                //CTFMeanPolarTrimmed.MultiplyLines(WaterMask);
                WaterMask.Dispose();*/

                // Normalize for CC (not strictly needed, but it's converted for fp16 later, so let's be on the safe side of the fp16 range.
                GPU.Normalize(CTFMeanPolarTrimmed.GetDevice(Intent.Read), CTFMeanPolarTrimmed.GetDevice(Intent.Write), (uint)CTFMeanPolarTrimmed.ElementsReal, 1);
                //CTFMeanPolarTrimmed.WriteMRC("ctfmeanpolartrimmed.mrc");

                CTF StartParams = new CTF
                {
                    PixelSize = (MainWindow.Options.CTFPixelMin + MainWindow.Options.CTFPixelMax) * 0.5M,
                    PixelSizeDelta = Math.Abs(MainWindow.Options.CTFPixelMax - MainWindow.Options.CTFPixelMin),
                    PixelSizeAngle = MainWindow.Options.CTFPixelAngle,

                    Defocus = CTF.Defocus,// (MainWindow.Options.CTFZMin + MainWindow.Options.CTFZMax) * 0.5M,
                    DefocusDelta = doastigmatism ? 0 : MainWindow.Options.CTFAstigmatism,
                    DefocusAngle = doastigmatism ? 0 : MainWindow.Options.CTFAstigmatismAngle,

                    Cs = MainWindow.Options.CTFCs,
                    Voltage = MainWindow.Options.CTFVoltage,
                    Amplitude = MainWindow.Options.CTFAmplitude
                };

                CTFFitStruct FitParams = new CTFFitStruct
                {
                    //Pixelsize = new float3(-0.02e-10f, 0.02e-10f, 0.01e-10f),
                    //Pixeldelta = new float3(0.0f, 0.02e-10f, 0.01e-10f),
                    //Pixelangle = new float3(0, 2 * (float)Math.PI, 1 * (float)Math.PI / 18),

                    //Defocus = new float3((float)(MainWindow.Options.CTFZMin - StartParams.Defocus) * 1e-6f,
                    //                     (float)(MainWindow.Options.CTFZMax - StartParams.Defocus) * 1e-6f,
                    //                     0.025e-6f),
                    Defocus = new float3(-0.4e-6f,
                                         0.4e-6f,
                                         0.025e-6f),

                    Defocusdelta = doastigmatism ? new float3(0, 0.8e-6f, 0.02e-6f) : new float3(0, 0, 0),
                    Astigmatismangle = doastigmatism ? new float3(0, 2 * (float)Math.PI, 1 * (float)Math.PI / 18) : new float3(0, 0, 0),
                    Phaseshift = MainWindow.Options.CTFDoPhase ? new float3(0, (float)Math.PI, 0.025f * (float)Math.PI) : new float3(0, 0, 0)
                };

                CTFStruct ResultStruct = GPU.CTFFitMean(CTFMeanPolarTrimmed.GetDevice(Intent.Read),
                                                        CTFCoordsPolarTrimmed.GetDevice(Intent.Read),
                                                        CTFMeanPolarTrimmed.DimsSlice,
                                                        StartParams.ToStruct(),
                                                        FitParams,
                                                        doastigmatism);
                CTF.FromStruct(ResultStruct);
                CTF.Defocus = Math.Max(CTF.Defocus, MainWindow.Options.CTFZMin);

                CTFMeanPolarTrimmed.Dispose();

                UpdateRotationalAverage(true); // This doesn't have a nice background yet.
                UpdateBackgroundFit(); // Now get a reasonably nice background.

                UpdateRotationalAverage(true); // This time, with the nice background.
                UpdateBackgroundFit(); // Make the background even nicer!
            }

            #endregion

            /*for (int i = 0; i < PS1D.Length; i++)
                PS1D[i].Y -= SimulatedBackground.Interp(PS1D[i].X);
            SimulatedBackground = new Cubic1D(SimulatedBackground.Data.Select(v => new float2(v.X, 0f)).ToArray());
            OnPropertyChanged("PS1D");

            CTFSpectra.Dispose();
            CTFMean.Dispose();
            CTFCoordsCart.Dispose();
            CTFCoordsPolarTrimmed.Dispose();

            Simulated1D = GetSimulated1D();
            CTFQuality = GetCTFQuality();

            return;*/

            // Do BFGS optimization of defocus, astigmatism and phase shift,
            // using 2D simulation for comparison

            #region BFGS

            bool[] CTFSpectraConsider = new bool[CTFSpectraGrid.Elements()];
            for (int i = 0; i < CTFSpectraConsider.Length; i++)
                CTFSpectraConsider[i] = true;
            int NCTFSpectraConsider = CTFSpectraConsider.Length;

            GridCTF = new CubicGrid(GridCTF.Dimensions, (float)CTF.Defocus, (float)CTF.Defocus, Dimension.X);
            GridCTFPhase = new CubicGrid(GridCTFPhase.Dimensions, (float)CTF.PhaseShift, (float)CTF.PhaseShift, Dimension.X);

            for (int preciseFit = 2; preciseFit < 3; preciseFit++)
            {
                NFreq = (MaxFreqExclusive - MinFreqInclusive) * (preciseFit + 1) / 3;
                //if (preciseFit >= 2)
                //    NFreq = MaxFreqExclusive - MinFreqInclusive;

                Image CTFSpectraPolarTrimmed = CTFSpectra.AsPolar((uint)MinFreqInclusive, (uint)(MinFreqInclusive + NFreq));
                CTFSpectra.FreeDevice(); // This will only be needed again for the final PS1D.

                #region Create background and scale

                float[] CurrentScale = _SimulatedScale.Interp(PS1D.Select(p => p.X).ToArray());

                Image CTFSpectraScale = new Image(new int3(NFreq, DimsRegion.X, 1));
                float[] CTFSpectraScaleData = CTFSpectraScale.GetHost(Intent.Write)[0];

                // Trim polar to relevant frequencies, and populate coordinates.
                Parallel.For(0, DimsRegion.X, y =>
                {
                    float Angle = ((float)y / DimsRegion.X + 0.5f) * (float)Math.PI;
                    for (int x = 0; x < NFreq; x++)
                        CTFSpectraScaleData[y * NFreq + x] = CurrentScale[x + MinFreqInclusive];
                });
                //CTFSpectraScale.WriteMRC("ctfspectrascale.mrc");

                // Background is just 1 line since we're in polar.
                Image CurrentBackground = new Image(_SimulatedBackground.Interp(PS1D.Select(p => p.X).ToArray()).Skip(MinFreqInclusive).Take(NFreq).ToArray());

                #endregion

                CTFSpectraPolarTrimmed.SubtractFromLines(CurrentBackground);
                CurrentBackground.Dispose();

                // Normalize background-subtracted spectra.
                GPU.Normalize(CTFSpectraPolarTrimmed.GetDevice(Intent.Read),
                              CTFSpectraPolarTrimmed.GetDevice(Intent.Write),
                              (uint)CTFSpectraPolarTrimmed.ElementsSliceReal,
                              (uint)CTFSpectraGrid.Elements());
                //CTFSpectraPolarTrimmed.WriteMRC("ctfspectrapolartrimmed.mrc");

                #region Convert to fp16

                Image CTFSpectraPolarTrimmedHalf = CTFSpectraPolarTrimmed.AsHalf();
                CTFSpectraPolarTrimmed.Dispose();

                Image CTFSpectraScaleHalf = CTFSpectraScale.AsHalf();
                CTFSpectraScale.Dispose();
                Image CTFCoordsPolarTrimmedHalf = CTFCoordsPolarTrimmed.AsHalf();

                #endregion

                // Wiggle weights show how the defocus on the spectra grid is altered 
                // by changes in individual anchor points of the spline grid.
                // They are used later to compute the dScore/dDefocus values for each spectrum 
                // only once, and derive the values for each anchor point from them.
                float[][] WiggleWeights = GridCTF.GetWiggleWeights(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 1f / (CTFGridZ + 1)));
                float[][] WiggleWeightsPhase = GridCTFPhase.GetWiggleWeights(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 1f / (CTFGridZ + 1)));

                // Helper method for getting CTFStructs for the entire spectra grid.
                Func<double[], CTF, float[], float[], CTFStruct[]> EvalGetCTF = (input, ctf, defocusValues, phaseValues) =>
                {
                    decimal AlteredDelta = (decimal)input[input.Length - 2];
                    decimal AlteredAngle = (decimal)(input[input.Length - 1] * 20 / (Math.PI / 180));

                    CTF Local = ctf.GetCopy();
                    Local.DefocusDelta = AlteredDelta;
                    Local.DefocusAngle = AlteredAngle;

                    CTFStruct LocalStruct = Local.ToStruct();
                    CTFStruct[] LocalParams = new CTFStruct[defocusValues.Length];
                    for (int i = 0; i < LocalParams.Length; i++)
                    {
                        LocalParams[i] = LocalStruct;
                        LocalParams[i].Defocus = defocusValues[i] * -1e-6f;
                        LocalParams[i].PhaseShift = phaseValues[i] * (float)Math.PI;
                    }

                    return LocalParams;
                };

                // Simulate with adjusted CTF, compare to originals

                #region Eval and Gradient methods

                float BorderZ = 0.5f / CTFGridZ;

                Func<double[], double> Eval = input =>
                {
                    CubicGrid Altered = new CubicGrid(GridCTF.Dimensions, input.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v).ToArray());
                    float[] DefocusValues = Altered.GetInterpolatedNative(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, BorderZ));
                    CubicGrid AlteredPhase = new CubicGrid(GridCTFPhase.Dimensions, input.Skip((int)GridCTF.Dimensions.Elements()).Take((int)GridCTFPhase.Dimensions.Elements()).Select(v => (float)v).ToArray());
                    float[] PhaseValues = AlteredPhase.GetInterpolatedNative(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, BorderZ));

                    CTFStruct[] LocalParams = EvalGetCTF(input, CTF, DefocusValues, PhaseValues);

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

                    if (float.IsNaN(Score) || float.IsInfinity(Score))
                        throw new Exception("Bad score.");

                    return (1.0 - Score) * 1000.0;
                };

                Func<double[], double[]> Gradient = input =>
                {
                    const float Step = 0.005f;
                    double[] Result = new double[input.Length];

                    // In 0D grid case, just get gradient for all 4 parameters.
                    // In 1+D grid case, do simple gradient for astigmatism and phase...
                    int StartComponent = input.Length - 2;
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

                    float[] ResultPlus = new float[CTFSpectraGrid.Elements()];
                    float[] ResultMinus = new float[CTFSpectraGrid.Elements()];

                    // ..., take shortcut for defoci...
                    {
                        CubicGrid AlteredPhase = new CubicGrid(GridCTFPhase.Dimensions, input.Skip((int)GridCTF.Dimensions.Elements()).Take((int)GridCTFPhase.Dimensions.Elements()).Select(v => (float)v).ToArray());
                        float[] PhaseValues = AlteredPhase.GetInterpolatedNative(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, BorderZ));

                        {
                            CubicGrid AlteredPlus = new CubicGrid(GridCTF.Dimensions, input.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v + Step).ToArray());
                            float[] DefocusValues = AlteredPlus.GetInterpolatedNative(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, BorderZ));

                            CTFStruct[] LocalParams = EvalGetCTF(input, CTF, DefocusValues, PhaseValues);

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
                            float[] DefocusValues = AlteredMinus.GetInterpolatedNative(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, BorderZ));

                            CTFStruct[] LocalParams = EvalGetCTF(input, CTF, DefocusValues, PhaseValues);

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
                        Parallel.For(0, GridCTF.Dimensions.Elements(), i => Result[i] = MathHelper.ReduceWeighted(LocalGradients, WiggleWeights[i]) / LocalGradients.Length / (2f * Step) * 1000f);
                    }

                    // ..., and take shortcut for phases.
                    if (MainWindow.Options.CTFDoPhase)
                    {
                        CubicGrid AlteredPlus = new CubicGrid(GridCTF.Dimensions, input.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v).ToArray());
                        float[] DefocusValues = AlteredPlus.GetInterpolatedNative(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, BorderZ));

                        {
                            CubicGrid AlteredPhasePlus = new CubicGrid(GridCTFPhase.Dimensions, input.Skip((int)GridCTF.Dimensions.Elements()).Take((int)GridCTFPhase.Dimensions.Elements()).Select(v => (float)v + Step).ToArray());
                            float[] PhaseValues = AlteredPhasePlus.GetInterpolatedNative(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, BorderZ));
                            CTFStruct[] LocalParams = EvalGetCTF(input, CTF, DefocusValues, PhaseValues);

                            GPU.CTFCompareToSim(CTFSpectraPolarTrimmedHalf.GetDevice(Intent.Read),
                                                CTFCoordsPolarTrimmedHalf.GetDevice(Intent.Read),
                                                CTFSpectraScaleHalf.GetDevice(Intent.Read),
                                                (uint)CTFSpectraPolarTrimmedHalf.ElementsSliceReal,
                                                LocalParams,
                                                ResultPlus,
                                                (uint)LocalParams.Length);
                        }
                        {
                            CubicGrid AlteredPhaseMinus = new CubicGrid(GridCTFPhase.Dimensions, input.Skip((int)GridCTF.Dimensions.Elements()).Take((int)GridCTFPhase.Dimensions.Elements()).Select(v => (float)v - Step).ToArray());
                            float[] PhaseValues = AlteredPhaseMinus.GetInterpolatedNative(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, BorderZ));
                            CTFStruct[] LocalParams = EvalGetCTF(input, CTF, DefocusValues, PhaseValues);

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
                        Parallel.For(0, GridCTFPhase.Dimensions.Elements(), i => Result[i + GridCTF.Dimensions.Elements()] = MathHelper.ReduceWeighted(LocalGradients, WiggleWeightsPhase[i]) / LocalGradients.Length / (2f * Step) * 1000f);
                    }

                    foreach (var i in Result)
                        if (double.IsNaN(i) || double.IsInfinity(i))
                            throw new Exception("Bad score.");

                    return Result;
                };

                #endregion

                #region Minimize first time with potential outpiers

                double[] StartParams = new double[GridCTF.Dimensions.Elements() + GridCTFPhase.Dimensions.Elements() + 2];
                for (int i = 0; i < GridCTF.Dimensions.Elements(); i++)
                    StartParams[i] = GridCTF.FlatValues[i];
                for (int i = 0; i < GridCTFPhase.Dimensions.Elements(); i++)
                    StartParams[i + GridCTF.Dimensions.Elements()] = GridCTFPhase.FlatValues[i];
                StartParams[StartParams.Length - 2] = (double)CTF.DefocusDelta;
                StartParams[StartParams.Length - 1] = (double)CTF.DefocusAngle / 20 * (Math.PI / 180);

                // Compute correlation for individual spectra, and throw away those that are >.75 sigma worse than mean.

                #region Discard outliers

                if (CTFSpace || CTFTime)
                {
                    CubicGrid Altered = new CubicGrid(GridCTF.Dimensions, StartParams.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v).ToArray());
                    float[] DefocusValues = Altered.GetInterpolatedNative(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, BorderZ));
                    CubicGrid AlteredPhase = new CubicGrid(GridCTFPhase.Dimensions, StartParams.Skip((int)GridCTF.Dimensions.Elements()).Take((int)GridCTFPhase.Dimensions.Elements()).Select(v => (float)v).ToArray());
                    float[] PhaseValues = AlteredPhase.GetInterpolatedNative(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, BorderZ));

                    CTFStruct[] LocalParams = EvalGetCTF(StartParams, CTF, DefocusValues, PhaseValues);

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
                        //if (Result[i] > MeanResult - StdResult * 1.5f)
                        CTFSpectraConsider[i] = true;
                        /*else
                        {
                            CTFSpectraConsider[i] = false;
                            for (int j = 0; j < WiggleWeights.Length; j++)
                                // Make sure the spectrum's gradient doesn't affect the overall gradient.
                                WiggleWeights[j][i] = 0;
                        }*/
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
                CTF.DefocusDelta = (decimal)Optimizer.Solution[StartParams.Length - 2];
                CTF.DefocusAngle = (decimal)(Optimizer.Solution[StartParams.Length - 1] * 20 / (Math.PI / 180));
                CTF.PhaseShift = (decimal)MathHelper.Mean(Optimizer.Solution.Skip((int)GridCTF.Dimensions.Elements()).Take((int)GridCTFPhase.Dimensions.Elements()).Select(v => (float)v));

                if (CTF.DefocusDelta < 0)
                {
                    CTF.DefocusAngle += 90;
                    CTF.DefocusDelta *= -1;
                }
                CTF.DefocusAngle = ((int)CTF.DefocusAngle + 180 * 99) % 180;

                GridCTF = new CubicGrid(GridCTF.Dimensions, Optimizer.Solution.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v).ToArray());
                GridCTFPhase = new CubicGrid(GridCTFPhase.Dimensions, Optimizer.Solution.Skip((int)GridCTF.Dimensions.Elements()).Take((int)GridCTFPhase.Dimensions.Elements()).Select(v => (float)v).ToArray());

                #endregion

                // Dispose GPU resources manually because GC can't be bothered to do it in time.
                CTFSpectraPolarTrimmedHalf.Dispose();
                CTFCoordsPolarTrimmedHalf.Dispose();
                CTFSpectraScaleHalf.Dispose();

                #region Get nicer envelope fit

                if (preciseFit >= 2)
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

                        float[] DefocusValues = GridCTF.GetInterpolatedNative(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, BorderZ));
                        CTFStruct[] LocalParams = DefocusValues.Select(v =>
                        {
                            CTF Local = CTF.GetCopy();
                            Local.Defocus = (decimal)v + 0.0M;

                            return Local.ToStruct();
                        }).ToArray();

                        Image CTFAverage1D = new Image(IntPtr.Zero, new int3(DimsRegion.X / 2, 1, 1));

                        CTF CTFAug = CTF.GetCopy();
                        CTFAug.Defocus += 0.0M;
                        GPU.CTFMakeAverage(CTFSpectra.GetDevice(Intent.Read),
                                           CTFCoordsCart.GetDevice(Intent.Read),
                                           (uint)CTFSpectra.ElementsSliceReal,
                                           (uint)DimsRegion.X,
                                           LocalParams,
                                           CTFAug.ToStruct(),
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
                        MathHelper.UnNaN(ForPS1D);
                        _PS1D = ForPS1D;

                        CTFSpectraBackground.Dispose();
                        CTFAverage1D.Dispose();
                        CTFSpectra.FreeDevice();
                    }

                    CTF.Defocus = Math.Max(CTF.Defocus, MainWindow.Options.CTFZMin);
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

                PS2DTemp = null;
                OnPropertyChanged("PS2D");
            }

            #endregion

            for (int i = 0; i < PS1D.Length; i++)
                PS1D[i].Y -= SimulatedBackground.Interp(PS1D[i].X);
            SimulatedBackground = new Cubic1D(SimulatedBackground.Data.Select(v => new float2(v.X, 0f)).ToArray());
            OnPropertyChanged("PS1D");

            CTFSpectra.Dispose();
            CTFMean.Dispose();
            CTFCoordsCart.Dispose();
            CTFCoordsPolarTrimmed.Dispose();

            Simulated1D = GetSimulated1D();
            CTFQuality = GetCTFQuality();

            SaveMeta();
        }

        public void ProcessParticleCTF(MapHeader originalHeader, Image originalStack, Star stardata, Image refft, Image maskft, int dimbox, decimal scaleFactor)
        {
            //CTF.Cs = MainWindow.Options.CTFCs;

            #region Dimensions and grids

            int NFrames = originalHeader.Dimensions.Z;
            int2 DimsImage = new int2(originalHeader.Dimensions);
            int2 DimsRegion = new int2(dimbox, dimbox);

            float3[] PositionsGrid;
            float3[] PositionsExtraction;
            float3[] ParticleAngles;
            List<int> RowIndices = new List<int>();
            {
                string[] ColumnNames = stardata.GetColumn("rlnMicrographName");
                for (int i = 0; i < ColumnNames.Length; i++)
                    if (ColumnNames[i].Contains(RootName))
                        RowIndices.Add(i);

                string[] ColumnOriginX = stardata.GetColumn("rlnCoordinateX");
                string[] ColumnOriginY = stardata.GetColumn("rlnCoordinateY");
                string[] ColumnShiftX = stardata.GetColumn("rlnOriginX");
                string[] ColumnShiftY = stardata.GetColumn("rlnOriginY");
                string[] ColumnAngleRot = stardata.GetColumn("rlnAngleRot");
                string[] ColumnAngleTilt = stardata.GetColumn("rlnAngleTilt");
                string[] ColumnAnglePsi = stardata.GetColumn("rlnAnglePsi");

                PositionsGrid = new float3[RowIndices.Count];
                PositionsExtraction = new float3[RowIndices.Count];
                ParticleAngles = new float3[RowIndices.Count];

                {
                    int i = 0;
                    foreach (var nameIndex in RowIndices)
                    {
                        float OriginX = float.Parse(ColumnOriginX[nameIndex]);
                        float OriginY = float.Parse(ColumnOriginY[nameIndex]);
                        float ShiftX = float.Parse(ColumnShiftX[nameIndex]);
                        float ShiftY = float.Parse(ColumnShiftY[nameIndex]);

                        PositionsExtraction[i] = new float3(OriginX - ShiftX - dimbox / 2, OriginY - ShiftY - dimbox / 2, 0f);
                        PositionsGrid[i] = new float3((OriginX - ShiftX) / DimsImage.X, (OriginY - ShiftY) / DimsImage.Y, 0);
                        ParticleAngles[i] = new float3(float.Parse(ColumnAngleRot[nameIndex]) * Helper.ToRad,
                                                       float.Parse(ColumnAngleTilt[nameIndex]) * Helper.ToRad,
                                                       float.Parse(ColumnAnglePsi[nameIndex]) * Helper.ToRad);
                        i++;
                    }
                }
            }
            int NPositions = PositionsGrid.Length;
            if (NPositions == 0)
                return;

            int CTFGridX = MainWindow.Options.GridCTFX;
            int CTFGridY = MainWindow.Options.GridCTFY;
            int CTFGridZ = Math.Min(NFrames, MainWindow.Options.GridCTFZ);

            int FrameGroupSize = CTFGridZ > 1 ? 12 : 1;
            int NFrameGroups = CTFGridZ > 1 ? NFrames / FrameGroupSize : 1;

            GridCTF = GridCTF.Resize(new int3(CTFGridX, CTFGridY, CTFGridZ));
            GridCTFPhase = GridCTFPhase.Resize(new int3(1, 1, CTFGridZ));

            int NSpectra = NFrameGroups * NPositions;

            int MinFreqInclusive = (int)(MainWindow.Options.CTFRangeMin * DimsRegion.X / 2);
            int MaxFreqExclusive = (int)(MainWindow.Options.CTFRangeMax * DimsRegion.X / 2);
            int NFreq = MaxFreqExclusive - MinFreqInclusive;

            float PixelSize = (float)CTF.PixelSize;
            float PixelDelta = (float)CTF.PixelSizeDelta;
            float PixelAngle = (float)CTF.PixelSizeAngle * Helper.ToRad;

            #endregion

            #region Allocate GPU memory

            Image CTFSpectra = new Image(IntPtr.Zero, new int3(DimsRegion.X, DimsRegion.X, NSpectra), true, true);
            Image CTFCoordsCart = new Image(new int3(DimsRegion), true, true);
            Image ParticleRefs = refft.AsProjections(ParticleAngles, DimsRegion, MainWindow.Options.ProjectionOversample);
            /*Image ParticleRefsIFT = ParticleRefs.AsIFFT();
            ParticleRefsIFT.WriteMRC("d_particlerefs.mrc");
            ParticleRefsIFT.Dispose();*/

            #endregion

            // Extract movie regions, create individual spectra in Cartesian coordinates.

            #region Create spectra

            Image ParticleMasksFT = maskft.AsProjections(ParticleAngles, DimsRegion, MainWindow.Options.ProjectionOversample);
            Image ParticleMasks = ParticleMasksFT.AsIFFT();
            ParticleMasksFT.Dispose();

            Parallel.ForEach(ParticleMasks.GetHost(Intent.ReadWrite), slice =>
            {
                for (int i = 0; i < slice.Length; i++)
                    slice[i] = (Math.Max(2f, Math.Min(25f, slice[i])) - 2) / 23f;
            });

            int3[] PositionsExtractionPerFrame = new int3[PositionsExtraction.Length * NFrames];
            for (int z = 0; z < NFrames; z++)
            {
                for (int p = 0; p < NPositions; p++)
                {
                    float3 Coords = new float3(PositionsGrid[p].X, PositionsGrid[p].Y, z / (float)(NFrames - 1));
                    float2 Offset = GetShiftFromPyramid(Coords);

                    PositionsExtractionPerFrame[z * NPositions + p] = new int3((int)Math.Round(PositionsExtraction[p].X - Offset.X),
                                                                               (int)Math.Round(PositionsExtraction[p].Y - Offset.Y),
                                                                               0);
                }
            }

            float3[] PositionsGridPerFrame = new float3[NSpectra];
            for (int z = 0; z < NFrameGroups; z++)
            {
                for (int p = 0; p < NPositions; p++)
                {
                    float3 Coords = new float3(PositionsGrid[p].X, PositionsGrid[p].Y, (z * FrameGroupSize + FrameGroupSize / 2) / (float)(NFrames - 1));
                    PositionsGridPerFrame[z * NPositions + p] = Coords;
                }
            }

            GPU.CreateParticleSpectra(originalStack.GetDevice(Intent.Read),
                                      DimsImage,
                                      NFrames,
                                      PositionsExtractionPerFrame,
                                      NPositions,
                                      ParticleMasks.GetDevice(Intent.Read),
                                      DimsRegion,
                                      CTFGridZ > 1,
                                      FrameGroupSize,
                                      PixelSize + PixelDelta / 2f,
                                      PixelSize - PixelDelta / 2f,
                                      PixelAngle,
                                      CTFSpectra.GetDevice(Intent.Write));
            originalStack.FreeDevice(); // Won't need it in this method anymore.
            ParticleMasks.Dispose();

            /*Image CTFSpectraIFT = CTFSpectra.AsIFFT();
            CTFSpectraIFT.RemapFromFT();
            CTFSpectraIFT.WriteMRC("d_ctfspectra.mrc");
            CTFSpectraIFT.Dispose();*/

            #endregion

            // Populate address arrays for later.

            #region Init addresses

            {
                float2[] CoordsData = new float2[CTFCoordsCart.ElementsSliceComplex];

                Helper.ForEachElementFT(DimsRegion, (x, y, xx, yy, r, a) => CoordsData[y * (DimsRegion.X / 2 + 1) + x] = new float2(r / DimsRegion.X, a));
                CTFCoordsCart.UpdateHostWithComplex(new[] { CoordsData });
                CTFCoordsCart.RemapToFT();
            }

            #endregion

            // Band-pass filter reference projections
            {
                Image BandMask = new Image(new int3(DimsRegion.X, DimsRegion.Y, 1), true);
                float[] BandMaskData = BandMask.GetHost(Intent.Write)[0];

                float[] CTFCoordsData = CTFCoordsCart.GetHost(Intent.Read)[0];
                for (int i = 0; i < BandMaskData.Length; i++)
                    BandMaskData[i] = (CTFCoordsData[i * 2] >= MinFreqInclusive / (float)DimsRegion.X && CTFCoordsData[i * 2] < MaxFreqExclusive / (float)DimsRegion.X) ? 1 : 0;

                ParticleRefs.MultiplySlices(BandMask);
                BandMask.Dispose();
            }

            Image Sigma2Noise = new Image(new int3(DimsRegion), true);
            {
                int GroupNumber = int.Parse(stardata.GetRowValue(RowIndices[0], "rlnGroupNumber"));
                Star SigmaTable = new Star(MainWindow.Options.ModelStarPath, "data_model_group_" + GroupNumber);
                float[] SigmaValues = SigmaTable.GetColumn("rlnSigma2Noise").Select(v => float.Parse(v)).ToArray();

                float[] Sigma2NoiseData = Sigma2Noise.GetHost(Intent.Write)[0];
                Helper.ForEachElementFT(new int2(DimsRegion.X, DimsRegion.Y), (x, y, xx, yy, r, angle) =>
                {
                    int ir = (int)r;
                    float val = 0;
                    if (ir < SigmaValues.Length)
                    {
                        if (SigmaValues[ir] != 0f)
                            val = 1f / SigmaValues[ir];
                    }
                    Sigma2NoiseData[y * (DimsRegion.X / 2 + 1) + x] = val;
                });
                float MaxSigma = MathHelper.Max(Sigma2NoiseData);
                for (int i = 0; i < Sigma2NoiseData.Length; i++)
                    Sigma2NoiseData[i] /= MaxSigma;

                Sigma2Noise.RemapToFT();
            }
            Sigma2Noise.WriteMRC("d_sigma2noise.mrc");

            // Do BFGS optimization of defocus, astigmatism and phase shift,
            // using 2D simulation for comparison

            #region BFGS
            {
                // Wiggle weights show how the defocus on the spectra grid is altered 
                // by changes in individual anchor points of the spline grid.
                // They are used later to compute the dScore/dDefocus values for each spectrum 
                // only once, and derive the values for each anchor point from them.
                float[][] WiggleWeights = GridCTF.GetWiggleWeights(PositionsGridPerFrame);
                float[][] WiggleWeightsPhase = GridCTFPhase.GetWiggleWeights(PositionsGridPerFrame);

                // Helper method for getting CTFStructs for the entire spectra grid.
                Func<double[], CTF, float[], float[], CTFStruct[]> EvalGetCTF = (input, ctf, defocusValues, phaseValues) =>
                {
                    decimal AlteredDelta = (decimal)input[input.Length - 2];
                    decimal AlteredAngle = (decimal)(input[input.Length - 1] * 20 / (Math.PI / 180));

                    CTF Local = ctf.GetCopy();
                    Local.DefocusDelta = AlteredDelta;
                    Local.DefocusAngle = AlteredAngle;
                    Local.PixelSizeDelta = 0;

                    CTFStruct LocalStruct = Local.ToStruct();
                    CTFStruct[] LocalParams = new CTFStruct[defocusValues.Length];
                    for (int i = 0; i < LocalParams.Length; i++)
                    {
                        LocalParams[i] = LocalStruct;
                        LocalParams[i].Defocus = defocusValues[i] * -1e-6f;
                        LocalParams[i].PhaseShift = phaseValues[i] * (float)Math.PI;
                    }

                    return LocalParams;
                };

                // Simulate with adjusted CTF, compare to originals

                #region Eval and Gradient methods

                Func<double[], double> Eval = input =>
                {
                    CubicGrid Altered = new CubicGrid(GridCTF.Dimensions, input.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v).ToArray());
                    float[] DefocusValues = Altered.GetInterpolatedNative(PositionsGridPerFrame);
                    CubicGrid AlteredPhase = new CubicGrid(GridCTFPhase.Dimensions, input.Skip((int)GridCTF.Dimensions.Elements()).Take((int)GridCTFPhase.Dimensions.Elements()).Select(v => (float)v).ToArray());
                    float[] PhaseValues = AlteredPhase.GetInterpolatedNative(PositionsGridPerFrame);

                    CTFStruct[] LocalParams = EvalGetCTF(input, CTF, DefocusValues, PhaseValues);

                    float[] Result = new float[LocalParams.Length];

                    GPU.ParticleCTFCompareToSim(CTFSpectra.GetDevice(Intent.Read),
                                                CTFCoordsCart.GetDevice(Intent.Read),
                                                ParticleRefs.GetDevice(Intent.Read),
                                                Sigma2Noise.GetDevice(Intent.Read),
                                                (uint)CTFSpectra.ElementsSliceComplex,
                                                LocalParams,
                                                Result,
                                                (uint)NFrameGroups,
                                                (uint)NPositions);

                    float Score = 0;
                    for (int i = 0; i < Result.Length; i++)
                        Score += Result[i];

                    Score /= NSpectra;

                    if (float.IsNaN(Score) || float.IsInfinity(Score))
                        throw new Exception("Bad score.");

                    return Score * 1.0;
                };

                Func<double[], double[]> Gradient = input =>
                {
                    const float Step = 0.001f;
                    double[] Result = new double[input.Length];

                    // In 0D grid case, just get gradient for all 4 parameters.
                    // In 1+D grid case, do simple gradient for astigmatism and phase...
                    int StartComponent = input.Length - 2;
                    //int StartComponent = 0;
                    /*for (int i = StartComponent; i < input.Length; i++)
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
                    }*/

                    float[] ResultPlus = new float[NSpectra];
                    float[] ResultMinus = new float[NSpectra];

                    // ..., take shortcut for defoci...
                    {
                        CubicGrid AlteredPhase = new CubicGrid(GridCTFPhase.Dimensions, input.Skip((int)GridCTF.Dimensions.Elements()).Take((int)GridCTFPhase.Dimensions.Elements()).Select(v => (float)v).ToArray());
                        float[] PhaseValues = AlteredPhase.GetInterpolatedNative(PositionsGridPerFrame);

                        {
                            CubicGrid AlteredPlus = new CubicGrid(GridCTF.Dimensions, input.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v + Step).ToArray());
                            float[] DefocusValues = AlteredPlus.GetInterpolatedNative(PositionsGridPerFrame);

                            CTFStruct[] LocalParams = EvalGetCTF(input, CTF, DefocusValues, PhaseValues);

                            GPU.ParticleCTFCompareToSim(CTFSpectra.GetDevice(Intent.Read),
                                                        CTFCoordsCart.GetDevice(Intent.Read),
                                                        ParticleRefs.GetDevice(Intent.Read),
                                                        Sigma2Noise.GetDevice(Intent.Read),
                                                        (uint)CTFSpectra.ElementsSliceComplex,
                                                        LocalParams,
                                                        ResultPlus,
                                                        (uint)NFrameGroups,
                                                        (uint)NPositions);
                        }
                        {
                            CubicGrid AlteredMinus = new CubicGrid(GridCTF.Dimensions, input.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v - Step).ToArray());
                            float[] DefocusValues = AlteredMinus.GetInterpolatedNative(PositionsGridPerFrame);

                            CTFStruct[] LocalParams = EvalGetCTF(input, CTF, DefocusValues, PhaseValues);

                            GPU.ParticleCTFCompareToSim(CTFSpectra.GetDevice(Intent.Read),
                                                        CTFCoordsCart.GetDevice(Intent.Read),
                                                        ParticleRefs.GetDevice(Intent.Read),
                                                        Sigma2Noise.GetDevice(Intent.Read),
                                                        (uint)CTFSpectra.ElementsSliceComplex,
                                                        LocalParams,
                                                        ResultMinus,
                                                        (uint)NFrameGroups,
                                                        (uint)NPositions);
                        }
                        float[] LocalGradients = new float[ResultPlus.Length];
                        for (int i = 0; i < LocalGradients.Length; i++)
                            LocalGradients[i] = ResultPlus[i] - ResultMinus[i];

                        // Now compute gradients per grid anchor point using the precomputed individual gradients and wiggle factors.
                        Parallel.For(0, GridCTF.Dimensions.Elements(), i => Result[i] = MathHelper.ReduceWeighted(LocalGradients, WiggleWeights[i]) / LocalGradients.Length / (2f * Step) * 1f);
                    }

                    // ..., and take shortcut for phases.
                    if (MainWindow.Options.CTFDoPhase)
                    {
                        CubicGrid AlteredPlus = new CubicGrid(GridCTF.Dimensions, input.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v).ToArray());
                        float[] DefocusValues = AlteredPlus.GetInterpolatedNative(PositionsGridPerFrame);

                        {
                            CubicGrid AlteredPhasePlus = new CubicGrid(GridCTFPhase.Dimensions, input.Skip((int)GridCTF.Dimensions.Elements()).Take((int)GridCTFPhase.Dimensions.Elements()).Select(v => (float)v + Step).ToArray());
                            float[] PhaseValues = AlteredPhasePlus.GetInterpolatedNative(PositionsGridPerFrame);
                            CTFStruct[] LocalParams = EvalGetCTF(input, CTF, DefocusValues, PhaseValues);

                            GPU.ParticleCTFCompareToSim(CTFSpectra.GetDevice(Intent.Read),
                                                        CTFCoordsCart.GetDevice(Intent.Read),
                                                        ParticleRefs.GetDevice(Intent.Read),
                                                        Sigma2Noise.GetDevice(Intent.Read),
                                                        (uint)CTFSpectra.ElementsSliceComplex,
                                                        LocalParams,
                                                        ResultPlus,
                                                        (uint)NFrameGroups,
                                                        (uint)NPositions);
                        }
                        {
                            CubicGrid AlteredPhaseMinus = new CubicGrid(GridCTFPhase.Dimensions, input.Skip((int)GridCTF.Dimensions.Elements()).Take((int)GridCTFPhase.Dimensions.Elements()).Select(v => (float)v - Step).ToArray());
                            float[] PhaseValues = AlteredPhaseMinus.GetInterpolatedNative(PositionsGridPerFrame);
                            CTFStruct[] LocalParams = EvalGetCTF(input, CTF, DefocusValues, PhaseValues);

                            GPU.ParticleCTFCompareToSim(CTFSpectra.GetDevice(Intent.Read),
                                                        CTFCoordsCart.GetDevice(Intent.Read),
                                                        ParticleRefs.GetDevice(Intent.Read),
                                                        Sigma2Noise.GetDevice(Intent.Read),
                                                        (uint)CTFSpectra.ElementsSliceComplex,
                                                        LocalParams,
                                                        ResultMinus,
                                                        (uint)NFrameGroups,
                                                        (uint)NPositions);
                        }
                        float[] LocalGradients = new float[ResultPlus.Length];
                        for (int i = 0; i < LocalGradients.Length; i++)
                            LocalGradients[i] = ResultPlus[i] - ResultMinus[i];

                        // Now compute gradients per grid anchor point using the precomputed individual gradients and wiggle factors.
                        Parallel.For(0, GridCTFPhase.Dimensions.Elements(), i => Result[i + GridCTF.Dimensions.Elements()] = MathHelper.ReduceWeighted(LocalGradients, WiggleWeightsPhase[i]) / LocalGradients.Length / (2f * Step) * 1f);
                    }

                    foreach (var i in Result)
                        if (double.IsNaN(i) || double.IsInfinity(i))
                            throw new Exception("Bad score.");

                    return Result;
                };

                #endregion

                #region Maximize normalized cross-correlation

                double[] StartParams = new double[GridCTF.Dimensions.Elements() + GridCTFPhase.Dimensions.Elements() + 2];
                for (int i = 0; i < GridCTF.Dimensions.Elements(); i++)
                    StartParams[i] = GridCTF.FlatValues[i];
                for (int i = 0; i < GridCTFPhase.Dimensions.Elements(); i++)
                    StartParams[i + GridCTF.Dimensions.Elements()] = GridCTFPhase.FlatValues[i];
                StartParams[StartParams.Length - 2] = (double)CTF.DefocusDelta;
                StartParams[StartParams.Length - 1] = (double)CTF.DefocusAngle / 20 * (Math.PI / 180);

                BroydenFletcherGoldfarbShanno Optimizer = new BroydenFletcherGoldfarbShanno(StartParams.Length, Eval, Gradient);
                /*{
                    Past = 1,
                    Delta = 1e-6,
                    MaxLineSearch = 15,
                    Corrections = 20
                };*/

                double[] BestStart = new double[StartParams.Length];
                for (int i = 0; i < BestStart.Length; i++)
                    BestStart[i] = StartParams[i];
                double BestValue = Eval(StartParams);
                for (int o = 0; o < 1; o++)
                {
                    /*for (int step = 0; step < 150; step++)
                    {
                        float Adjustment = (step - 75) / 75f * 0.075f;
                        double[] Adjusted = new double[StartParams.Length];
                        for (int j = 0; j < Adjusted.Length; j++)
                            if (j < GridCTF.Dimensions.Elements())
                                Adjusted[j] = StartParams[j] + Adjustment;
                            else
                                Adjusted[j] = StartParams[j];

                        double NewValue = Eval(Adjusted);
                        if (NewValue > BestValue)
                        {
                            BestValue = NewValue;
                            for (int j = 0; j < GridCTF.Dimensions.Elements(); j++)
                                BestStart[j] = StartParams[j] + Adjustment;
                        }
                    }
                    for (int i = 0; i < GridCTF.Dimensions.Elements(); i++)
                        StartParams[i] = BestStart[i];*/

                    Optimizer.Maximize(StartParams);
                }

                #endregion

                #region Retrieve parameters

                decimal NewDefocus = (decimal)MathHelper.Mean(StartParams.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v));
                Debug.WriteLine(CTF.Defocus - NewDefocus);
                CTF.Defocus = (decimal)MathHelper.Mean(StartParams.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v));
                CTF.DefocusDelta = (decimal)StartParams[StartParams.Length - 2];
                CTF.DefocusAngle = (decimal)(StartParams[StartParams.Length - 1] * 20 / (Math.PI / 180));
                CTF.PhaseShift = (decimal)MathHelper.Mean(StartParams.Skip((int)GridCTF.Dimensions.Elements()).Take((int)GridCTFPhase.Dimensions.Elements()).Select(v => (float)v));

                GridCTF = new CubicGrid(GridCTF.Dimensions, StartParams.Take((int)GridCTF.Dimensions.Elements()).Select(v => (float)v).ToArray());
                GridCTFPhase = new CubicGrid(GridCTFPhase.Dimensions, StartParams.Skip((int)GridCTF.Dimensions.Elements()).Take((int)GridCTFPhase.Dimensions.Elements()).Select(v => (float)v).ToArray());

                #endregion

                Sigma2Noise.Dispose();
            }

            #endregion

            ParticleRefs.Dispose();
            //ParticleAmps.Dispose();
            CTFSpectra.Dispose();
            CTFCoordsCart.Dispose();

            Simulated1D = GetSimulated1D();
            CTFQuality = GetCTFQuality();

            SaveMeta();
        }

        public void ProcessShift(MapHeader originalHeader, Image originalStack, decimal scaleFactor)
        {
            // Deal with dimensions and grids.

            int NFrames = originalHeader.Dimensions.Z;
            int2 DimsImage = new int2(originalHeader.Dimensions);
            int2 DimsRegion = new int2(768, 768);

            float OverlapFraction = 0.0f;
            int2 DimsPositionGrid;
            int3[] PositionGrid = Helper.GetEqualGridSpacing(DimsImage, DimsRegion, OverlapFraction, out DimsPositionGrid);
            //PositionGrid = new[] { new int3(0, 0, 0) };
            //DimsPositionGrid = new int2(1, 1);
            int NPositions = PositionGrid.Length;

            int ShiftGridX = 1;
            int ShiftGridY = 1;
            int ShiftGridZ = Math.Min(NFrames, MainWindow.Options.GridMoveZ);
            GridMovementX = new CubicGrid(new int3(ShiftGridX, ShiftGridY, ShiftGridZ));
            GridMovementY = new CubicGrid(new int3(ShiftGridX, ShiftGridY, ShiftGridZ));

            int LocalGridX = Math.Min(DimsPositionGrid.X, MainWindow.Options.GridMoveX);
            int LocalGridY = Math.Min(DimsPositionGrid.Y, MainWindow.Options.GridMoveY);
            int LocalGridZ = Math.Min(2, NFrames);
            GridLocalX = new CubicGrid(new int3(LocalGridX, LocalGridY, LocalGridZ));
            GridLocalY = new CubicGrid(new int3(LocalGridX, LocalGridY, LocalGridZ));

            int3 ShiftGrid = new int3(DimsPositionGrid.X, DimsPositionGrid.Y, NFrames);

            int MinFreqInclusive = (int)(MainWindow.Options.MovementRangeMin * DimsRegion.X / 2);
            int MaxFreqExclusive = (int)(MainWindow.Options.MovementRangeMax * DimsRegion.X / 2);
            int NFreq = MaxFreqExclusive - MinFreqInclusive;

            int CentralFrame = NFrames / 2;

            int MaskExpansions = Math.Max(1, ShiftGridZ / 3);
            int[] MaskSizes = new int[MaskExpansions];

            // Allocate memory and create all prerequisites:
            int MaskLength;
            Image ShiftFactors;
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

                float Bfac = (float)MainWindow.Options.MovementBfactor * 0.25f / PixelSize / DimsRegion.X;
                float2[] BfacWeightsData = Freq.Select(v => (float)Math.Exp(v.X * Bfac)).Select(v => new float2(v, v)).ToArray();
                Image BfacWeights = new Image(Helper.ToInterleaved(BfacWeightsData), false, false, false);

                long[] RelevantMask = Positions.ToArray();
                ShiftFactors = new Image(Helper.ToInterleaved(Factors.ToArray()));
                MaskLength = RelevantMask.Length;

                // Get mask sizes for different expansion steps.
                for (int i = 0; i < MaskExpansions; i++)
                {
                    float CurrentMaxFreq = MinFreqInclusive + (MaxFreqExclusive - MinFreqInclusive) / (float)MaskExpansions * (i + 1);
                    MaskSizes[i] = Freq.Count(v => v.X * v.X < CurrentMaxFreq * CurrentMaxFreq);
                }

                Phases = new Image(IntPtr.Zero, new int3(MaskLength * 2, DimsPositionGrid.X * DimsPositionGrid.Y, NFrames), false, false, false);

                GPU.CreateShift(originalStack.GetDevice(Intent.Read),
                                new int2(originalHeader.Dimensions),
                                originalHeader.Dimensions.Z,
                                PositionGrid,
                                PositionGrid.Length,
                                DimsRegion,
                                RelevantMask,
                                (uint)MaskLength,
                                Phases.GetDevice(Intent.Write));

                Phases.MultiplyLines(BfacWeights);
                BfacWeights.Dispose();

                originalStack.FreeDevice();
                PhasesAverage = new Image(IntPtr.Zero, new int3(MaskLength, NPositions, 1), false, true, false);
                Shifts = new Image(new float[NPositions * NFrames * 2]);
            }

            #region Fit global movement

            {
                int MinXSteps = 1, MinYSteps = 1;
                int MinZSteps = Math.Min(NFrames, 3);
                int3 ExpansionGridSize = new int3(MinXSteps, MinYSteps, MinZSteps);
                float[][] WiggleWeights = new CubicGrid(ExpansionGridSize).GetWiggleWeights(ShiftGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 0f));
                double[] StartParams = new double[ExpansionGridSize.Elements() * 2];

                for (int m = 0; m < MaskExpansions; m++)
                {
                    double[] LastAverage = null;

                    Action<double[]> SetPositions = input =>
                    {
                        // Construct CubicGrids and get interpolated shift values.
                        CubicGrid AlteredGridX = new CubicGrid(ExpansionGridSize, input.Where((v, i) => i % 2 == 0).Select(v => (float)v).ToArray());
                        float[] AlteredX = AlteredGridX.GetInterpolatedNative(new int3(DimsPositionGrid.X, DimsPositionGrid.Y, NFrames),
                                                                              new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 0f));
                        CubicGrid AlteredGridY = new CubicGrid(ExpansionGridSize, input.Where((v, i) => i % 2 == 1).Select(v => (float)v).ToArray());
                        float[] AlteredY = AlteredGridY.GetInterpolatedNative(new int3(DimsPositionGrid.X, DimsPositionGrid.Y, NFrames),
                                                                              new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 0f));

                        // Let movement start at 0 in the central frame.
                        /*float2[] CenterFrameOffsets = new float2[NPositions];
                        for (int i = 0; i < NPositions; i++)
                            CenterFrameOffsets[i] = new float2(AlteredX[CentralFrame * NPositions + i], AlteredY[CentralFrame * NPositions + i]);*/

                        // Finally, set the shift values in the device array.
                        float[] ShiftData = Shifts.GetHost(Intent.Write)[0];
                        Parallel.For(0, AlteredX.Length, i =>
                        {
                            ShiftData[i * 2] = AlteredX[i];// - CenterFrameOffsets[i % NPositions].X;
                            ShiftData[i * 2 + 1] = AlteredY[i];// - CenterFrameOffsets[i % NPositions].Y;
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
                                                (uint)MaskLength,
                                                (uint)MaskSizes[m],
                                                Shifts.GetDevice(Intent.Read),
                                                (uint)NPositions,
                                                (uint)NFrames);

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
                            Diff[i] = Diff[i];// * 100f;

                        return Diff.Sum();
                    };

                    Func<double[], double[]> Grad = input =>
                    {
                        DoAverage(input);

                        float[] GradX = new float[NPositions * NFrames], GradY = new float[NPositions * NFrames];

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

                        //for (int i = 0; i < Diff.Length; i++)
                            //Diff[i] = Diff[i] * 100f;

                        for (int i = 0; i < GradX.Length; i++)
                        {
                            GradX[i] = Diff[i * 2];
                            GradY[i] = Diff[i * 2 + 1];
                        }

                        double[] Result = new double[input.Length];
                        Parallel.For(0, input.Length / 2, i =>
                        {
                            Result[i * 2] = MathHelper.ReduceWeighted(GradX, WiggleWeights[i]);
                            Result[i * 2 + 1] = MathHelper.ReduceWeighted(GradY, WiggleWeights[i]);
                        });
                        return Result;
                    };

                    /*Func<double[], double[]> Grad = input =>
                    {
                        DoAverage(input);

                        float[] GradX = new float[NPositions * NFrames], GradY = new float[NPositions * NFrames];
                        float Step = 0.002f;

                        {
                            double[] InputXP = new double[input.Length];
                            for (int i = 0; i < input.Length; i++)
                                if (i % 2 == 0)
                                    InputXP[i] = input[i] + Step;
                                else
                                    InputXP[i] = input[i];
                            SetPositions(InputXP);

                            float[] DiffXP = new float[NPositions * NFrames];
                            GPU.ShiftGetDiff(Phases.GetDevice(Intent.Read),
                                             PhasesAverage.GetDevice(Intent.Read),
                                             ShiftFactors.GetDevice(Intent.Read),
                                             (uint)MaskLength,
                                             (uint)MaskSizes[m],
                                             Shifts.GetDevice(Intent.Read),
                                             DiffXP,
                                             (uint)NPositions,
                                             (uint)NFrames);


                            double[] InputXM = new double[input.Length];
                            for (int i = 0; i < input.Length; i++)
                                if (i % 2 == 0)
                                    InputXM[i] = input[i] - Step;
                                else
                                    InputXM[i] = input[i];
                            SetPositions(InputXM);

                            float[] DiffXM = new float[NPositions * NFrames];
                            GPU.ShiftGetDiff(Phases.GetDevice(Intent.Read),
                                             PhasesAverage.GetDevice(Intent.Read),
                                             ShiftFactors.GetDevice(Intent.Read),
                                             (uint)MaskLength,
                                             (uint)MaskSizes[m],
                                             Shifts.GetDevice(Intent.Read),
                                             DiffXM,
                                             (uint)NPositions,
                                             (uint)NFrames);

                            for (int i = 0; i < GradX.Length; i++)
                                GradX[i] = (DiffXP[i] - DiffXM[i]) / (Step * 2);
                        }

                        {
                            double[] InputYP = new double[input.Length];
                            for (int i = 0; i < input.Length; i++)
                                if (i % 2 == 1)
                                    InputYP[i] = input[i] + Step;
                                else
                                    InputYP[i] = input[i];
                            SetPositions(InputYP);

                            float[] DiffYP = new float[NPositions * NFrames];
                            GPU.ShiftGetDiff(Phases.GetDevice(Intent.Read),
                                             PhasesAverage.GetDevice(Intent.Read),
                                             ShiftFactors.GetDevice(Intent.Read),
                                             (uint)MaskLength,
                                             (uint)MaskSizes[m],
                                             Shifts.GetDevice(Intent.Read),
                                             DiffYP,
                                             (uint)NPositions,
                                             (uint)NFrames);


                            double[] InputYM = new double[input.Length];
                            for (int i = 0; i < input.Length; i++)
                                if (i % 2 == 1)
                                    InputYM[i] = input[i] - Step;
                                else
                                    InputYM[i] = input[i];
                            SetPositions(InputYM);

                            float[] DiffYM = new float[NPositions * NFrames];
                            GPU.ShiftGetDiff(Phases.GetDevice(Intent.Read),
                                             PhasesAverage.GetDevice(Intent.Read),
                                             ShiftFactors.GetDevice(Intent.Read),
                                             (uint)MaskLength,
                                             (uint)MaskSizes[m],
                                             Shifts.GetDevice(Intent.Read),
                                             DiffYM,
                                             (uint)NPositions,
                                             (uint)NFrames);

                            for (int i = 0; i < GradY.Length; i++)
                                GradY[i] = (DiffYP[i] - DiffYM[i]) / (Step * 2);
                        }

                        double[] Result = new double[input.Length];
                        Parallel.For(0, input.Length / 2, i =>
                        {
                            Result[i * 2] = MathHelper.ReduceWeighted(GradX, WiggleWeights[i]);
                            Result[i * 2 + 1] = MathHelper.ReduceWeighted(GradY, WiggleWeights[i]);
                        });
                        return Result;
                    };*/

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
                                                     (int)Math.Round((float)(ShiftGridZ - MinZSteps) / (MaskExpansions - 1) * (m + 1) + MinZSteps));
                        WiggleWeights = new CubicGrid(ExpansionGridSize).GetWiggleWeights(ShiftGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 0f));

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
            }

            #endregion

            // Center the global shifts
            /*{
                float2[] AverageShifts = new float2[ShiftGridZ];
                for (int i = 0; i < AverageShifts.Length; i++)
                    AverageShifts[i] = new float2(MathHelper.Mean(GridMovementX.GetSliceXY(i)),
                                                  MathHelper.Mean(GridMovementY.GetSliceXY(i)));
                float2 CenterShift = MathHelper.Mean(AverageShifts);

                GridMovementX = new CubicGrid(GridMovementX.Dimensions, GridMovementX.FlatValues.Select(v => v - CenterShift.X).ToArray());
                GridMovementY = new CubicGrid(GridMovementY.Dimensions, GridMovementY.FlatValues.Select(v => v - CenterShift.Y).ToArray());
            }*/

            #region Fit local movement

            /*{
                int MinXSteps = LocalGridX, MinYSteps = LocalGridY;
                int MinZSteps = LocalGridZ;
                int3 ExpansionGridSize = new int3(MinXSteps, MinYSteps, MinZSteps);
                float[][] WiggleWeights = new CubicGrid(ExpansionGridSize).GetWiggleWeights(ShiftGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 0f));
                double[] StartParams = new double[ExpansionGridSize.Elements() * 2];

                for (int m = MaskExpansions - 1; m < MaskExpansions; m++)
                {
                    double[] LastAverage = null;

                    Action<double[]> SetPositions = input =>
                    {
                        // Construct CubicGrids and get interpolated shift values.
                        float[] GlobalX = GridMovementX.GetInterpolatedNative(new int3(DimsPositionGrid.X, DimsPositionGrid.Y, NFrames),
                                                                              new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 0f));
                        CubicGrid AlteredGridX = new CubicGrid(ExpansionGridSize, input.Where((v, i) => i % 2 == 0).Select(v => (float)v).ToArray());
                        float[] AlteredX = AlteredGridX.GetInterpolatedNative(new int3(DimsPositionGrid.X, DimsPositionGrid.Y, NFrames),
                                                                              new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 0f));
                        AlteredX = MathHelper.Plus(GlobalX, AlteredX);

                        float[] GlobalY = GridMovementY.GetInterpolatedNative(new int3(DimsPositionGrid.X, DimsPositionGrid.Y, NFrames),
                                                                              new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 0f));
                        CubicGrid AlteredGridY = new CubicGrid(ExpansionGridSize, input.Where((v, i) => i % 2 == 1).Select(v => (float)v).ToArray());
                        float[] AlteredY = AlteredGridY.GetInterpolatedNative(new int3(DimsPositionGrid.X, DimsPositionGrid.Y, NFrames),
                                                                              new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 0f));
                        AlteredY = MathHelper.Plus(GlobalY, AlteredY);

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
                                                (uint)MaskLength,
                                                (uint)MaskSizes[m],
                                                Shifts.GetDevice(Intent.Read),
                                                (uint)NPositions,
                                                (uint)NFrames);

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
                            Diff[i] = Diff[i] * 100f;

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
                            Diff[i] = Diff[i] * 100f;

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
                    GridLocalX = new CubicGrid(ExpansionGridSize, Optimizer.Solution.Where((v, i) => i % 2 == 0).Select(v => (float)v).ToArray());
                    GridLocalY = new CubicGrid(ExpansionGridSize, Optimizer.Solution.Where((v, i) => i % 2 == 1).Select(v => (float)v).ToArray());

                    if (m < MaskExpansions - 1)
                    {
                        // Refine sampling.
                        ExpansionGridSize = new int3((int)Math.Round((float)(LocalGridX - MinXSteps) / (MaskExpansions - 1) * (m + 1) + MinXSteps),
                                                     (int)Math.Round((float)(LocalGridY - MinYSteps) / (MaskExpansions - 1) * (m + 1) + MinYSteps),
                                                     (int)Math.Round((float)(LocalGridZ - MinZSteps) / (MaskExpansions - 1) * (m + 1) + MinZSteps));
                        WiggleWeights = new CubicGrid(ExpansionGridSize).GetWiggleWeights(ShiftGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 0f));

                        // Resize the grids to account for finer sampling.
                        GridLocalX = GridLocalX.Resize(ExpansionGridSize);
                        GridLocalY = GridLocalY.Resize(ExpansionGridSize);

                        // Construct start parameters for next optimization iteration.
                        StartParams = new double[ExpansionGridSize.Elements() * 2];
                        for (int i = 0; i < ExpansionGridSize.Elements(); i++)
                        {
                            StartParams[i * 2] = GridLocalX.FlatValues[i];
                            StartParams[i * 2 + 1] = GridLocalY.FlatValues[i];
                        }
                    }
                }
            }*/

            #endregion

            ShiftFactors.Dispose();
            Phases.Dispose();
            PhasesAverage.Dispose();
            Shifts.Dispose();

            // Center the local shifts
            /*{
                float2[] AverageShifts = new float2[LocalGridZ];
                for (int i = 0; i < AverageShifts.Length; i++)
                    AverageShifts[i] = new float2(MathHelper.Mean(GridLocalX.GetSliceXY(i)),
                                                  MathHelper.Mean(GridLocalY.GetSliceXY(i)));
                float2 CenterShift = MathHelper.Mean(AverageShifts);

                GridLocalX = new CubicGrid(GridLocalX.Dimensions, GridLocalX.FlatValues.Select(v => v - CenterShift.X).ToArray());
                GridLocalY = new CubicGrid(GridLocalY.Dimensions, GridLocalY.FlatValues.Select(v => v - CenterShift.Y).ToArray());
            }*/

            SaveMeta();
        }

        public void ProcessParticleShift(MapHeader originalHeader, Image originalStack, Star stardata, Image refft, Image maskft, int dimbox, decimal scaleFactor)
        {
            // Deal with dimensions and grids.

            int NFrames = originalHeader.Dimensions.Z;
            int2 DimsImage = new int2(originalHeader.Dimensions);
            int2 DimsRegion = new int2(dimbox, dimbox);

            decimal SubdivisionRatio = 4M;
            List<int3> PyramidSizes = new List<int3>();
            PyramidSizes.Add(new int3(MainWindow.Options.GridMoveX, MainWindow.Options.GridMoveX, Math.Min(NFrames, MainWindow.Options.GridMoveZ)));
            while (true)
            {
                int3 Previous = PyramidSizes.Last();
                int NewZ = Math.Min((int)Math.Round(Previous.Z / SubdivisionRatio), Previous.Z - 1);
                if (NewZ < 2)
                    break;

                PyramidSizes.Add(new int3(Previous.X * 2, Previous.Y * 2, NewZ));
            }

            PyramidShiftX.Clear();
            PyramidShiftY.Clear();

            float3[] PositionsGrid, PositionsGridPerFrame;
            float2[] PositionsExtraction, PositionsShift;
            float3[] ParticleAngles;
            List<int> RowIndices = new List<int>();
            {
                string[] ColumnNames = stardata.GetColumn("rlnMicrographName");
                for (int i = 0; i < ColumnNames.Length; i++)
                    if (ColumnNames[i].Contains(RootName))
                        RowIndices.Add(i);

                string[] ColumnOriginX = stardata.GetColumn("rlnCoordinateX");
                string[] ColumnOriginY = stardata.GetColumn("rlnCoordinateY");
                string[] ColumnShiftX = stardata.GetColumn("rlnOriginX");
                string[] ColumnShiftY = stardata.GetColumn("rlnOriginY");
                string[] ColumnAngleRot = stardata.GetColumn("rlnAngleRot");
                string[] ColumnAngleTilt = stardata.GetColumn("rlnAngleTilt");
                string[] ColumnAnglePsi = stardata.GetColumn("rlnAnglePsi");

                PositionsGrid = new float3[RowIndices.Count];
                PositionsGridPerFrame = new float3[RowIndices.Count * NFrames];
                PositionsExtraction = new float2[RowIndices.Count];
                PositionsShift = new float2[RowIndices.Count * NFrames];
                ParticleAngles = new float3[RowIndices.Count];

                {
                    int i = 0;
                    foreach (var nameIndex in RowIndices)
                    {
                        float OriginX = float.Parse(ColumnOriginX[nameIndex]);
                        float OriginY = float.Parse(ColumnOriginY[nameIndex]);
                        float ShiftX = float.Parse(ColumnShiftX[nameIndex]);
                        float ShiftY = float.Parse(ColumnShiftY[nameIndex]);

                        PositionsExtraction[i] = new float2(OriginX - ShiftX, OriginY - ShiftY);
                        PositionsGrid[i] = new float3((OriginX - ShiftX) / DimsImage.X, (OriginY - ShiftY) / DimsImage.Y, 0.5f);
                        for (int z = 0; z < NFrames; z++)
                        {
                            PositionsGridPerFrame[z * RowIndices.Count + i] = new float3(PositionsGrid[i].X,
                                                                                          PositionsGrid[i].Y,
                                                                                          (float)z / (NFrames - 1));

                            PositionsShift[z * RowIndices.Count + i] = GetShiftFromPyramid(PositionsGridPerFrame[z * RowIndices.Count + i]);
                        }
                        ParticleAngles[i] = new float3(float.Parse(ColumnAngleRot[nameIndex]) * Helper.ToRad,
                                                       float.Parse(ColumnAngleTilt[nameIndex]) * Helper.ToRad,
                                                       float.Parse(ColumnAnglePsi[nameIndex]) * Helper.ToRad);

                        i++;
                    }
                }
            }
            int NPositions = PositionsGrid.Length;
            if (NPositions == 0)
                return;

            int MinFreqInclusive = (int)(MainWindow.Options.MovementRangeMin * DimsRegion.X / 2);
            int MaxFreqExclusive = (int)(MainWindow.Options.MovementRangeMax * DimsRegion.X / 2);
            int NFreq = MaxFreqExclusive - MinFreqInclusive;

            int CentralFrame = NFrames / 2;

            int MaskExpansions = 4; // Math.Max(1, PyramidSizes[0].Z / 3);
            int[] MaskSizes = new int[MaskExpansions];

            // Allocate memory and create all prerequisites:
            int MaskLength;
            Image ShiftFactors;
            Image Phases;
            Image Projections;
            Image Shifts;
            Image InvSigma;
            {
                List<long> Positions = new List<long>();
                List<float2> Factors = new List<float2>();
                List<float2> Freq = new List<float2>();
                int Min2 = MinFreqInclusive * MinFreqInclusive;
                int Max2 = MaxFreqExclusive * MaxFreqExclusive;

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

                // Addresses for CTF simulation
                Image CTFCoordsCart = new Image(new int3(DimsRegion), true, true);
                {
                    float2[] CoordsData = new float2[CTFCoordsCart.ElementsSliceComplex];

                    Helper.ForEachElementFT(DimsRegion, (x, y, xx, yy, r, a) => CoordsData[y * (DimsRegion.X / 2 + 1) + x] = new float2(r / DimsRegion.X, a));
                    CTFCoordsCart.UpdateHostWithComplex(new[] { CoordsData });
                    CTFCoordsCart.RemapToFT();
                }
                float[] ValuesDefocus = GridCTF.GetInterpolatedNative(PositionsGrid);
                CTFStruct[] PositionsCTF = ValuesDefocus.Select(v =>
                {
                    CTF Altered = CTF.GetCopy();
                    Altered.PixelSizeDelta = 0;
                    Altered.Defocus = (decimal)v;
                    //Altered.Bfactor = -MainWindow.Options.MovementBfactor;
                    return Altered.ToStruct();
                }).ToArray();

                // Sort everyone with ascending distance from center.
                List<KeyValuePair<float, int>> FreqIndices = Freq.Select((v, i) => new KeyValuePair<float, int>(v.X, i)).ToList();
                FreqIndices.Sort((a, b) => a.Key.CompareTo(b.Key));
                int[] SortedIndices = FreqIndices.Select(v => v.Value).ToArray();

                Helper.Reorder(Positions, SortedIndices);
                Helper.Reorder(Factors, SortedIndices);
                Helper.Reorder(Freq, SortedIndices);

                long[] RelevantMask = Positions.ToArray();
                ShiftFactors = new Image(Helper.ToInterleaved(Factors.ToArray()));
                MaskLength = RelevantMask.Length;

                // Get mask sizes for different expansion steps.
                for (int i = 0; i < MaskExpansions; i++)
                {
                    float CurrentMaxFreq = MinFreqInclusive + (MaxFreqExclusive - MinFreqInclusive) / (float)MaskExpansions * (i + 1);
                    MaskSizes[i] = Freq.Count(v => v.X * v.X < CurrentMaxFreq * CurrentMaxFreq);
                }

                Phases = new Image(IntPtr.Zero, new int3(MaskLength, NPositions, NFrames), false, true, false);
                Projections = new Image(IntPtr.Zero, new int3(MaskLength, NPositions, NFrames), false, true, false);
                InvSigma = new Image(IntPtr.Zero, new int3(MaskLength, 1, 1));

                Image ParticleMasksFT = maskft.AsProjections(ParticleAngles, DimsRegion, MainWindow.Options.ProjectionOversample);
                Image ParticleMasks = ParticleMasksFT.AsIFFT();
                ParticleMasksFT.Dispose();
                ParticleMasks.RemapFromFT();

                Parallel.ForEach(ParticleMasks.GetHost(Intent.ReadWrite), slice =>
                {
                    for (int i = 0; i < slice.Length; i++)
                        slice[i] = (Math.Max(2f, Math.Min(25f, slice[i])) - 2) / 23f;
                });

                Image ProjectionsSparse = refft.AsProjections(ParticleAngles, DimsRegion, MainWindow.Options.ProjectionOversample);

                Image InvSigmaSparse = new Image(new int3(DimsRegion), true);
                {
                    int GroupNumber = int.Parse(stardata.GetRowValue(RowIndices[0], "rlnGroupNumber"));
                    //Star SigmaTable = new Star("D:\\rado27\\RefineWarppolish\\run1_model.star", "data_model_group_" + GroupNumber);
                    Star SigmaTable = new Star(MainWindow.Options.ModelStarPath, "data_model_group_" + GroupNumber);
                    float[] SigmaValues = SigmaTable.GetColumn("rlnSigma2Noise").Select(v => float.Parse(v)).ToArray();

                    float[] Sigma2NoiseData = InvSigmaSparse.GetHost(Intent.Write)[0];
                    Helper.ForEachElementFT(new int2(DimsRegion.X, DimsRegion.Y), (x, y, xx, yy, r, angle) =>
                    {
                        int ir = (int)r;
                        float val = 0;
                        if (ir < SigmaValues.Length)
                        {
                            if (SigmaValues[ir] != 0f)
                                val = 1f / SigmaValues[ir];
                        }
                        Sigma2NoiseData[y * (DimsRegion.X / 2 + 1) + x] = val;
                    });
                    float MaxSigma = MathHelper.Max(Sigma2NoiseData);
                    for (int i = 0; i < Sigma2NoiseData.Length; i++)
                        Sigma2NoiseData[i] /= MaxSigma;

                    InvSigmaSparse.RemapToFT();
                }
                //InvSigmaSparse.WriteMRC("d_sigma2noise.mrc");

                float PixelSize = (float)CTF.PixelSize;
                float PixelDelta = (float)CTF.PixelSizeDelta;
                float PixelAngle = (float)CTF.PixelSizeAngle * Helper.ToRad;

                GPU.CreateParticleShift(originalStack.GetDevice(Intent.Read),
                                        DimsImage,
                                        NFrames,
                                        Helper.ToInterleaved(PositionsExtraction),
                                        Helper.ToInterleaved(PositionsShift),
                                        NPositions,
                                        DimsRegion,
                                        RelevantMask,
                                        (uint)RelevantMask.Length,
                                        ParticleMasks.GetDevice(Intent.Read),
                                        ProjectionsSparse.GetDevice(Intent.Read),
                                        PositionsCTF,
                                        CTFCoordsCart.GetDevice(Intent.Read),
                                        InvSigmaSparse.GetDevice(Intent.Read),
                                        PixelSize + PixelDelta / 2,
                                        PixelSize - PixelDelta / 2,
                                        PixelAngle,
                                        Phases.GetDevice(Intent.Write),
                                        Projections.GetDevice(Intent.Write),
                                        InvSigma.GetDevice(Intent.Write));

                InvSigmaSparse.Dispose();
                ParticleMasks.Dispose();
                ProjectionsSparse.Dispose();
                CTFCoordsCart.Dispose();
                originalStack.FreeDevice();
                Shifts = new Image(new float[NPositions * NFrames * 2]);
            }

            #region Fit movement

            {

                int NPyramidPoints = 0;
                float[][][] WiggleWeights = new float[PyramidSizes.Count][][];
                for (int p = 0; p < PyramidSizes.Count; p++)
                {
                    CubicGrid WiggleGrid = new CubicGrid(PyramidSizes[p]);
                    NPyramidPoints += (int)PyramidSizes[p].Elements();

                    WiggleWeights[p] = WiggleGrid.GetWiggleWeights(PositionsGridPerFrame);
                }

                double[] StartParams = new double[NPyramidPoints * 2];

                for (int m = 3; m < MaskExpansions; m++)
                {
                    for (int currentGrid = 0; currentGrid < PyramidSizes.Count; currentGrid++)
                    {
                        Action<double[]> SetPositions = input =>
                        {
                            // Construct CubicGrids and get interpolated shift values.
                            float[] AlteredX = new float[PositionsGridPerFrame.Length];
                            float[] AlteredY = new float[PositionsGridPerFrame.Length];

                            int Offset = 0;
                            foreach (var size in PyramidSizes)
                            {
                                int Elements = (int)size.Elements();
                                CubicGrid GridX = new CubicGrid(size, input.Skip(Offset).Take(Elements).Select(v => (float)v).ToArray());
                                AlteredX = MathHelper.Plus(AlteredX, GridX.GetInterpolatedNative(PositionsGridPerFrame));

                                CubicGrid GridY = new CubicGrid(size, input.Skip(NPyramidPoints + Offset).Take(Elements).Select(v => (float)v).ToArray());
                                AlteredY = MathHelper.Plus(AlteredY, GridY.GetInterpolatedNative(PositionsGridPerFrame));

                                Offset += Elements;
                            }

                            // Finally, set the shift values in the device array.
                            float[] ShiftData = Shifts.GetHost(Intent.Write)[0];
                            for (int i = 0; i < PositionsGridPerFrame.Length; i++)
                            {
                                ShiftData[i * 2] = AlteredX[i];
                                ShiftData[i * 2 + 1] = AlteredY[i];
                            }
                        };

                        Func<double[], double> Eval = input =>
                        {
                            SetPositions(input);

                            float[] Diff = new float[NPositions * NFrames];
                            GPU.ParticleShiftGetDiff(Phases.GetDevice(Intent.Read),
                                                     Projections.GetDevice(Intent.Read),
                                                     ShiftFactors.GetDevice(Intent.Read),
                                                     InvSigma.GetDevice(Intent.Read),
                                                     (uint)MaskLength,
                                                     (uint)MaskSizes[m],
                                                     Shifts.GetDevice(Intent.Read),
                                                     Diff,
                                                     (uint)NPositions,
                                                     (uint)NFrames);

                            //for (int i = 0; i < Diff.Length; i++)
                            //Diff[i] = Diff[i] * 100f;

                            double Score = Diff.Sum();
                            //Debug.WriteLine(Score);
                            return Score;
                        };

                        Func<double[], double[]> Grad = input =>
                        {
                            SetPositions(input);

                            float[] Diff = new float[NPositions * NFrames * 2];
                            GPU.ParticleShiftGetGrad(Phases.GetDevice(Intent.Read),
                                                     Projections.GetDevice(Intent.Read),
                                                     ShiftFactors.GetDevice(Intent.Read),
                                                     InvSigma.GetDevice(Intent.Read),
                                                     (uint)MaskLength,
                                                     (uint)MaskSizes[m],
                                                     Shifts.GetDevice(Intent.Read),
                                                     Diff,
                                                     (uint)NPositions,
                                                     (uint)NFrames);

                            //for (int i = 0; i < Diff.Length; i++)
                                //Diff[i] = Diff[i] * 100f;

                            float[] DiffX = new float[NPositions * NFrames], DiffY = new float[NPositions * NFrames];
                            for (int i = 0; i < DiffX.Length; i++)
                            {
                                DiffX[i] = Diff[i * 2];
                                DiffY[i] = Diff[i * 2 + 1];
                            }

                            double[] Result = new double[input.Length];
                            int Offset = 0;
                            for (int p = 0; p < PyramidSizes.Count; p++)
                            {
                                //if (p == currentGrid)
                                    Parallel.For(0, (int)PyramidSizes[p].Elements(), i =>
                                    {
                                        Result[Offset + i] = MathHelper.ReduceWeighted(DiffX, WiggleWeights[p][i]);
                                        Result[NPyramidPoints + Offset + i] = MathHelper.ReduceWeighted(DiffY, WiggleWeights[p][i]);
                                    });

                                Offset += (int)PyramidSizes[p].Elements();
                            }
                            return Result;
                        };

                        BroydenFletcherGoldfarbShanno Optimizer = new BroydenFletcherGoldfarbShanno(StartParams.Length, Eval, Grad);
                        //Optimizer.Corrections = 20;
                        Optimizer.Minimize(StartParams);
                    }

                    {
                        PyramidShiftX.Clear();
                        PyramidShiftY.Clear();
                        int Offset = 0;
                        foreach (var size in PyramidSizes)
                        {
                            int Elements = (int)size.Elements();
                            CubicGrid GridX = new CubicGrid(size, StartParams.Skip(Offset).Take(Elements).Select(v => (float)v).ToArray());
                            PyramidShiftX.Add(GridX);

                            CubicGrid GridY = new CubicGrid(size, StartParams.Skip(NPyramidPoints + Offset).Take(Elements).Select(v => (float)v).ToArray());
                            PyramidShiftY.Add(GridY);

                            Offset += Elements;
                        }
                    }
                }
            }

            #endregion

            ShiftFactors.Dispose();
            Phases.Dispose();
            Projections.Dispose();
            Shifts.Dispose();
            InvSigma.Dispose();

            SaveMeta();
        }

        private float2 GetShiftFromPyramid(float3 coords)
        {
            float2 Result = new float2(0, 0);

            Result.X = GridMovementX.GetInterpolated(coords);
            Result.Y = GridMovementY.GetInterpolated(coords);

            for (int i = 0; i < PyramidShiftX.Count; i++)
            //for (int i = 0; i < 0; i++)
            {
                Result.X += PyramidShiftX[i].GetInterpolated(coords);
                Result.Y += PyramidShiftY[i].GetInterpolated(coords);
            }

            return Result;
        }

        public void CreateCorrected(MapHeader originalHeader, Image originalStack)
        {
            if (!Directory.Exists(AverageDir))
                Directory.CreateDirectory(AverageDir);
            if (!Directory.Exists(CTFDir))
                Directory.CreateDirectory(CTFDir);

            if (MainWindow.Options.PostStack && !Directory.Exists(ShiftedStackDir))
                Directory.CreateDirectory(ShiftedStackDir);

            int3 Dims = originalStack.Dims;

            Image ShiftedStack = null;
            if (MainWindow.Options.PostStack)
                ShiftedStack = new Image(Dims);

            float PixelSize = (float)(MainWindow.Options.CTFPixelMin + MainWindow.Options.CTFPixelMax) * 0.5f;
            float PixelDelta = (float)(MainWindow.Options.CTFPixelMax - MainWindow.Options.CTFPixelMin) * 0.5f;
            float PixelAngle = (float)MainWindow.Options.CTFPixelAngle / (float)(180.0 / Math.PI);
            Image CTFCoords;
            {
                float2[] CTFCoordsData = new float2[Dims.ElementsSlice()];
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
            Weights.Fill(1e-6f);

            float StepZ = 1f / Math.Max(Dims.Z - 1, 1);

            for (int nframe = 0; nframe < Dims.Z; nframe++)
            {

                Image PS = new Image(Dims.Slice(), true);
                PS.Fill(1f);

                // Apply motion blur filter.
                /*{
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
                }*/

                // Apply CTF.
                /*if (CTF != null)
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

                    CTFImage.Abs();
                    PS.Multiply(CTFImage);
                    //CTFImage.WriteMRC("ctf.mrc");
                    CTFImage.Dispose();
                }*/

                // Apply dose weighting.
                /*{
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
                }*/

                Image Frame = new Image(originalStack.GetHost(Intent.Read)[nframe], Dims.Slice());
                Frame.ShiftSlicesMassive(new[]
                {
                    new float3(CollapsedMovementX.GetInterpolated(new float3(0.5f, 0.5f, nframe * StepZ)),
                               CollapsedMovementY.GetInterpolated(new float3(0.5f, 0.5f, nframe * StepZ)),
                               0f)
                });
                if (MainWindow.Options.PostStack)
                    ShiftedStack.GetHost(Intent.Write)[nframe] = Frame.GetHost(Intent.Read)[0];

                Image FrameFT = Frame.AsFFT();
                Frame.Dispose();

                //Image PSSign = new Image(PS.GetDevice(Intent.Read), Dims.Slice(), true);
                //Image PSSign = new Image(Dims.Slice(), true);
                //PSSign.Fill(1f);
                //PSSign.Sign();

                // Do phase flipping before averaging.
                //FrameFT.Multiply(PSSign);
                //PS.Multiply(PSSign);
                //PSSign.Dispose();

                //FrameFT.Multiply(PS);
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

            //AverageFT.Divide(Weights);
            //AverageFT.WriteMRC("averageft.mrc");
            //Weights.WriteMRC("weights.mrc");
            AveragePS.Divide(Weights);
            Weights.Dispose();

            Image Average = AverageFT.AsIFFT();
            AverageFT.Dispose();

            MapHeader Header = originalHeader;
            Header.Dimensions = Dims.Slice();

            Average.WriteMRC(AveragePath);
            Average.Dispose();

            AveragePS.WriteMRC(CTFPath);
            AveragePS.Dispose();

            TempAverageImage = null;
            OnPropertyChanged("AverageImage");

            using (TextWriter Writer = File.CreateText(AverageDir + RootName + "_ctffind3.log"))
            {
                decimal Mag = (MainWindow.Options.CTFDetectorPixel * 10000M / CTF.PixelSize);

                Writer.WriteLine("CS[mm], HT[kV], AmpCnst, XMAG, DStep[um]");
                Writer.WriteLine($"{CTF.Cs} {CTF.Voltage} {CTF.Amplitude} {Mag} {MainWindow.Options.CTFDetectorPixel}");

                float BestQ = 0;
                float2[] Q = CTFQuality;
                if (Q != null)
                    foreach (var q in Q)
                    {
                        if (q.Y < 0.3f)
                            break;
                        BestQ = q.X * 2f;
                    }

                Writer.WriteLine($"{(CTF.Defocus + CTF.DefocusDelta / 2M) * 1e4M} {(CTF.Defocus - CTF.DefocusDelta / 2M) * 1e4M} {CTF.DefocusAngle} {BestQ} {CTF.PhaseShift * 180M} Final Values");
            }

            if (MainWindow.Options.PostStack)
                ShiftedStack.WriteMRC(ShiftedStackPath);
        }

        public float2[] GetMotionTrack(float2 position, int samples)
        {
            if (Dimensions.Z <= 1 || Dimensions.Z <= 1)
                return null;

            float BorderX = 0.01f, BorderY = 0.01f;
            position.X = Math.Max(BorderX, Math.Min(1f - BorderX, position.X));
            position.Y = Math.Max(BorderY, Math.Min(1f - BorderY, position.Y));
            float2[] Result = new float2[Dimensions.Z * samples];

            float StepZ = 1f / Math.Max(Dimensions.Z * samples - 1, 1);
            for (int z = 0; z < Dimensions.Z * samples; z++)
                Result[z] = GetShiftFromPyramid(new float3(position.X, position.Y, z * StepZ));

            return Result;
        }

        public void UpdateStarDefocus(Star table, string[] columnNames, string[] columnCoordsX, string[] columnCoordsY)
        {
            List<int> NameIndices = new List<int>();
            string InvariantRoot = RootName;
            for (int i = 0; i < columnNames.Length; i++)
                if (columnNames[i].Contains(InvariantRoot))
                    NameIndices.Add(i);

            //if (NameIndices.Count == 0)
                //Debug.WriteLine(RootName + ": " + NameIndices.Count);
                
            foreach (var nameIndex in NameIndices)
            {
                float CoordX = float.Parse(columnCoordsX[nameIndex], CultureInfo.InvariantCulture);
                float CoordY = float.Parse(columnCoordsY[nameIndex], CultureInfo.InvariantCulture);

                float LocalDefocus = GridCTF.GetInterpolated(new float3(CoordX / (Dimensions.X * 0.5f), CoordY / (Dimensions.Y * 0.5f), 4f / 37f));
                table.SetRowValue(nameIndex, "rlnDefocusU", ((LocalDefocus + (float)CTF.DefocusDelta / 2f) * 1e4f).ToString(CultureInfo.InvariantCulture));
                table.SetRowValue(nameIndex, "rlnDefocusV", ((LocalDefocus - (float)CTF.DefocusDelta / 2f) * 1e4f).ToString(CultureInfo.InvariantCulture));
                table.SetRowValue(nameIndex, "rlnDefocusAngle", ((float)CTF.DefocusAngle).ToString(CultureInfo.InvariantCulture));

                // Go from pixel size to magnification
                float Mag = (float)(MainWindow.Options.CTFDetectorPixel * 10000M / (CTF.PixelSize / 1.00M));
                table.SetRowValue(nameIndex, "rlnMagnification", Mag.ToString(CultureInfo.InvariantCulture));

                //table.SetRowValue(nameIndex, "rlnSphericalAberration", ((float)CTF.Cs).ToString(CultureInfo.InvariantCulture));
                //float LocalPhaseShift = GridCTFPhase.GetInterpolated(new float3(0.5f, 0.5f, 4f / 37f));
                //table.SetRowValue(nameIndex, "rlnPhaseShift", (LocalPhaseShift * 180f).ToString(CultureInfo.InvariantCulture));
            }
        }

        public void ExportParticles(Star tableIn, Star tableOut, MapHeader originalHeader, Image originalStack, int size, float particleradius, decimal scaleFactor)
        {
            if (!tableIn.HasColumn("rlnAutopickFigureOfMerit"))
                tableIn.AddColumn("rlnAutopickFigureOfMerit");

            List<int> RowIndices = new List<int>();
            string[] ColumnMicrographName = tableIn.GetColumn("rlnMicrographName");
            for (int i = 0; i < ColumnMicrographName.Length; i++)
                if (ColumnMicrographName[i].Contains(RootName))
                    RowIndices.Add(i);

            if (RowIndices.Count == 0)
                return;

            if (!Directory.Exists(ParticlesDir))
                Directory.CreateDirectory(ParticlesDir);
            if (!Directory.Exists(ParticleCTFDir))
                Directory.CreateDirectory(ParticleCTFDir);

            int3 Dims = originalHeader.Dimensions;
            int3 DimsRegion = new int3(size, size, 1);
            int3 DimsPadded = new int3(size * 2, size * 2, 1);
            int NParticles = RowIndices.Count;

            float PixelSize = (float)CTF.PixelSize;
            float PixelDelta = (float)CTF.PixelSizeDelta;
            float PixelAngle = (float)CTF.PixelSizeAngle * Helper.ToRad;
            /*Image CTFCoords;
            Image CTFFreq;
            {
                float2[] CTFCoordsData = new float2[(DimsRegion.X / 2 + 1) * DimsRegion.Y];
                float[] CTFFreqData = new float[(DimsRegion.X / 2 + 1) * DimsRegion.Y];
                for (int y = 0; y < DimsRegion.Y; y++)
                    for (int x = 0; x < DimsRegion.X / 2 + 1; x++)
                    {
                        int xx = x;
                        int yy = y < DimsRegion.Y / 2 + 1 ? y : y - DimsRegion.Y;

                        float xs = xx / (float)DimsRegion.X;
                        float ys = yy / (float)DimsRegion.Y;
                        float r = (float)Math.Sqrt(xs * xs + ys * ys);
                        float angle = (float)(Math.Atan2(yy, xx));
                        float CurrentPixelSize = PixelSize + PixelDelta * (float)Math.Cos(2f * (angle - PixelAngle));

                        CTFCoordsData[y * (DimsRegion.X / 2 + 1) + x] = new float2(r / DimsRegion.X, angle);
                        CTFFreqData[y * (DimsRegion.X / 2 + 1) + x] = r / CurrentPixelSize;
                    }

                CTFCoords = new Image(CTFCoordsData, DimsRegion.Slice(), true);
                CTFFreq = new Image(CTFFreqData, DimsRegion.Slice(), true);
            }*/

            string[] ColumnPosX = tableIn.GetColumn("rlnCoordinateX");
            string[] ColumnPosY = tableIn.GetColumn("rlnCoordinateY");
            string[] ColumnOriginX = tableIn.GetColumn("rlnOriginX");
            string[] ColumnOriginY = tableIn.GetColumn("rlnOriginY");
            int3[] Origins = new int3[NParticles];
            float3[] ResidualShifts = new float3[NParticles];

            for (int i = 0; i < NParticles; i++)
            {
                float2 Pos = new float2(float.Parse(ColumnPosX[RowIndices[i]], CultureInfo.InvariantCulture),
                                        float.Parse(ColumnPosY[RowIndices[i]], CultureInfo.InvariantCulture)) * 1.00f;
                float2 Shift = new float2(float.Parse(ColumnOriginX[RowIndices[i]], CultureInfo.InvariantCulture),
                                          float.Parse(ColumnOriginY[RowIndices[i]], CultureInfo.InvariantCulture)) * 1.00f;

                Origins[i] = new int3((int)(Pos.X - Shift.X),
                                      (int)(Pos.Y - Shift.Y),
                                      0);
                ResidualShifts[i] = new float3(-MathHelper.ResidualFraction(Pos.X - Shift.X),
                                               -MathHelper.ResidualFraction(Pos.Y - Shift.Y),
                                               0f);

                tableIn.SetRowValue(RowIndices[i], "rlnCoordinateX", Origins[i].X.ToString());
                tableIn.SetRowValue(RowIndices[i], "rlnCoordinateY", Origins[i].Y.ToString());
                tableIn.SetRowValue(RowIndices[i], "rlnOriginX", "0.0");
                tableIn.SetRowValue(RowIndices[i], "rlnOriginY", "0.0");
            }

            Image AverageFT = new Image(new int3(DimsRegion.X, DimsRegion.Y, NParticles), true, true);
            Image AveragePS = new Image(new int3(DimsRegion.X, DimsRegion.Y, NParticles), true);
            Image Weights = new Image(new int3(DimsRegion.X, DimsRegion.Y, NParticles), true);
            Weights.Fill(1e-6f);
            Image FrameParticles = new Image(IntPtr.Zero, new int3(DimsPadded.X, DimsPadded.Y, NParticles));

            float StepZ = 1f / Math.Max(Dims.Z - 1, 1);
            for (int z = 0; z < Dims.Z; z++)
            {
                float CoordZ = z * StepZ;

                if (originalStack != null)
                    GPU.Extract(originalStack.GetDeviceSlice(z, Intent.Read),
                                FrameParticles.GetDevice(Intent.Write),
                                Dims.Slice(),
                                DimsPadded,
                                Helper.ToInterleaved(Origins.Select(v => new int3(v.X - DimsPadded.X / 2, v.Y - DimsPadded.Y / 2, 0)).ToArray()),
                                (uint)NParticles);

                // Shift particles
                {
                    float3[] Shifts = new float3[NParticles];

                    for (int i = 0; i < NParticles; i++)
                    {
                        float3 Coords = new float3((float)Origins[i].X / Dims.X, (float)Origins[i].Y / Dims.Y, CoordZ);
                        Shifts[i] = ResidualShifts[i] + new float3(GetShiftFromPyramid(Coords)) * 1.00f;
                    }
                    FrameParticles.ShiftSlices(Shifts);
                }

                Image FrameParticlesCropped = FrameParticles.AsPadded(new int2(DimsRegion));
                Image FrameParticlesFT = FrameParticlesCropped.AsFFT();
                FrameParticlesCropped.Dispose();

                //Image PS = new Image(new int3(DimsRegion.X, DimsRegion.Y, NParticles), true);
                //PS.Fill(1f);

                // Apply motion blur filter.

                #region Motion blur weighting

                /*{
                    const int Samples = 11;
                    float StartZ = (z - 0.5f) * StepZ;
                    float StopZ = (z + 0.5f) * StepZ;

                    float2[] Shifts = new float2[Samples * NParticles];
                    for (int p = 0; p < NParticles; p++)
                    {
                        float NormX = (float)Origins[p].X / Dims.X;
                        float NormY = (float)Origins[p].Y / Dims.Y;

                        for (int zz = 0; zz < Samples; zz++)
                        {
                            float zp = StartZ + (StopZ - StartZ) / (Samples - 1) * zz;
                            float3 Coords = new float3(NormX, NormY, zp);
                            Shifts[p * Samples + zz] = GetShiftFromPyramid(Coords);
                        }
                    }

                    Image MotionFilter = new Image(IntPtr.Zero, new int3(DimsRegion.X, DimsRegion.Y, NParticles), true);
                    GPU.CreateMotionBlur(MotionFilter.GetDevice(Intent.Write),
                                         DimsRegion,
                                         Helper.ToInterleaved(Shifts.Select(v => new float3(v.X, v.Y, 0)).ToArray()),
                                         Samples,
                                         (uint)NParticles);
                    PS.Multiply(MotionFilter);
                    //MotionFilter.WriteMRC("motion.mrc");
                    MotionFilter.Dispose();
                }*/

                #endregion

                // Apply CTF.

                #region CTF weighting

                /*if (CTF != null)
                {
                    CTFStruct[] Structs = new CTFStruct[NParticles];
                    for (int p = 0; p < NParticles; p++)
                    {
                        CTF Altered = CTF.GetCopy();
                        Altered.Defocus = (decimal)GridCTF.GetInterpolated(new float3(Origins[p].X / Dims.X,
                                                                                      Origins[p].Y / Dims.Y,
                                                                                      z * StepZ));

                        Structs[p] = Altered.ToStruct();
                    }

                    Image CTFImage = new Image(IntPtr.Zero, new int3(DimsRegion.X, DimsRegion.Y, NParticles), true);
                    GPU.CreateCTF(CTFImage.GetDevice(Intent.Write),
                                  CTFCoords.GetDevice(Intent.Read),
                                  (uint)CTFCoords.ElementsSliceComplex,
                                  Structs,
                                  false,
                                  (uint)NParticles);

                    //CTFImage.Abs();
                    PS.Multiply(CTFImage);
                    //CTFImage.WriteMRC("ctf.mrc");
                    CTFImage.Dispose();
                }*/

                #endregion

                // Apply dose.

                #region Dose weighting
                /*{
                    float3 NikoConst = new float3(0.245f, -1.665f, 2.81f);

                    // Niko's formula expects e-/A2/frame, we've got e-/px/frame -- convert!
                    float FrameDose = (float)MainWindow.Options.CorrectDosePerFrame * (z + 0.5f) / (PixelSize * PixelSize);

                    Image DoseImage = new Image(IntPtr.Zero, DimsRegion, true);
                    GPU.DoseWeighting(CTFFreq.GetDevice(Intent.Read),
                                      DoseImage.GetDevice(Intent.Write),
                                      (uint)DoseImage.ElementsSliceComplex,
                                      new[] { FrameDose },
                                      NikoConst,
                                      1);
                    PS.MultiplySlices(DoseImage);
                    //DoseImage.WriteMRC("dose.mrc");
                    DoseImage.Dispose();
                }*/
                #endregion

                //Image PSAbs = new Image(PS.GetDevice(Intent.Read), new int3(DimsRegion.X, DimsRegion.Y, NParticles), true);
                //PSAbs.Abs();

                //FrameParticlesFT.Multiply(PS);
                AverageFT.Add(FrameParticlesFT);

                //Weights.Add(PSAbs);

                //PS.Multiply(PS);
                //AveragePS.Add(PS);

                //PS.Dispose();
                FrameParticlesFT.Dispose();
                //PSAbs.Dispose();
            }
            FrameParticles.Dispose();
            //CTFCoords.Dispose();

            //AverageFT.Divide(Weights);
            //AveragePS.Divide(Weights);
            //AverageFT.Multiply(AveragePS);
            Weights.Dispose();

            Image AverageParticlesUncorrected = AverageFT.AsIFFT();
            AverageFT.Dispose();

            Image AverageParticles = AverageParticlesUncorrected.AsAnisotropyCorrected(new int2(DimsRegion),
                                                                                       (float)(CTF.PixelSize + CTF.PixelSizeDelta / 2M),
                                                                                       (float)(CTF.PixelSize - CTF.PixelSizeDelta / 2M),
                                                                                       (float)CTF.PixelSizeAngle * Helper.ToRad,
                                                                                       8);
            AverageParticlesUncorrected.Dispose();

            GPU.NormParticles(AverageParticles.GetDevice(Intent.Read),
                              AverageParticles.GetDevice(Intent.Write),
                              DimsRegion,
                              (uint)(particleradius / (PixelSize / 1.00f)),
                              true,
                              (uint)NParticles);

            HeaderMRC ParticlesHeader = new HeaderMRC
            {
                Pixelsize = new float3(PixelSize, PixelSize, PixelSize)
            };

            AverageParticles.WriteMRC(ParticlesPath, ParticlesHeader);
            AverageParticles.Dispose();

            //AveragePS.WriteMRC(ParticleCTFPath, ParticlesHeader);
            AveragePS.Dispose();

            float[] DistanceWeights = new float[NParticles];
            for (int p1 = 0; p1 < NParticles - 1; p1++)
            {
                float2 Pos1 = new float2(Origins[p1].X, Origins[p1].Y);

                for (int p2 = p1 + 1; p2 < NParticles; p2++)
                {
                    float2 Pos2 = new float2(Origins[p2].X, Origins[p2].Y);
                    float2 Diff = Pos2 - Pos1;
                    float Dist = Diff.X * Diff.X + Diff.Y * Diff.Y;
                    Dist = 1f / Dist;

                    DistanceWeights[p1] += Dist;
                    DistanceWeights[p2] += Dist;
                }
            }

            for (int i = 0; i < NParticles; i++)
            {
                string ParticlePath = (i + 1).ToString("D6") + "@particles/" + RootName + "_particles.mrcs";
                tableIn.SetRowValue(RowIndices[i], "rlnImageName", ParticlePath);

                //string ParticleCTFsPath = (i + 1).ToString("D6") + "@particlectf/" + RootName + "_particlectf.mrcs";
                //tableIn.SetRowValue(RowIndices[i], "rlnCtfImage", ParticleCTFsPath);

                tableIn.SetRowValue(RowIndices[i], "rlnAutopickFigureOfMerit", DistanceWeights[i].ToString(CultureInfo.InvariantCulture));
            }
        }

        public void ExportParticlesMovie(Star tableIn, Star tableOut, MapHeader originalHeader, Image originalStack, int size, float particleradius, decimal scaleFactor)
        {
            int CurrentDevice = GPU.GetDevice();

            #region Make sure directories exist.
            lock (tableIn)
            {
                if (!Directory.Exists(ParticleMoviesDir))
                    Directory.CreateDirectory(ParticleMoviesDir);
                if (!Directory.Exists(ParticleCTFMoviesDir))
                    Directory.CreateDirectory(ParticleCTFMoviesDir);
            }
            #endregion

            #region Get row indices for all, and individual halves

            List<int> RowIndices = new List<int>();
            string[] ColumnMicrographName = tableIn.GetColumn("rlnMicrographName");
            for (int i = 0; i < ColumnMicrographName.Length; i++)
                if (ColumnMicrographName[i].Contains(RootName))
                    RowIndices.Add(i);

            //RowIndices = RowIndices.Take(13).ToList();

            List<int> RowIndices1 = new List<int>();
            List<int> RowIndices2 = new List<int>();
            for (int i = 0; i < RowIndices.Count; i++)
                if (tableIn.GetRowValue(RowIndices[i], "rlnRandomSubset") == "1")
                    RowIndices1.Add(RowIndices[i]);
                else
                    RowIndices2.Add(RowIndices[i]);

            #endregion

            if (RowIndices.Count == 0)
                return;

            #region Auxiliary variables

            List<int> TableOutIndices = new List<int>();

            int3 Dims = originalHeader.Dimensions;
            Dims.Z = 36;
            int3 DimsRegion = new int3(size, size, 1);
            int3 DimsPadded = new int3(size * 2, size * 2, 1);
            int NParticles = RowIndices.Count;
            int NParticles1 = RowIndices1.Count;
            int NParticles2 = RowIndices2.Count;

            float PixelSize = (float)CTF.PixelSize / 1.00f;
            float PixelDelta = (float)CTF.PixelSizeDelta / 1.00f;
            float PixelAngle = (float)CTF.PixelSizeAngle * Helper.ToRad;

            #endregion

            #region Prepare initial coordinates and shifts

            string[] ColumnPosX = tableIn.GetColumn("rlnCoordinateX");
            string[] ColumnPosY = tableIn.GetColumn("rlnCoordinateY");
            string[] ColumnOriginX = tableIn.GetColumn("rlnOriginX");
            string[] ColumnOriginY = tableIn.GetColumn("rlnOriginY");

            int3[] Origins1 = new int3[NParticles1];
            int3[] Origins2 = new int3[NParticles2];
            float3[] ResidualShifts1 = new float3[NParticles1];
            float3[] ResidualShifts2 = new float3[NParticles2];

            lock (tableIn)  // Writing to the table, better be on the safe side
            {
                // Half1: Add translational shifts to coordinates, sans the fractional part
                for (int i = 0; i < NParticles1; i++)
                {
                    float2 Pos = new float2(float.Parse(ColumnPosX[RowIndices1[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosY[RowIndices1[i]], CultureInfo.InvariantCulture)) * 1.00f;
                    float2 Shift = new float2(float.Parse(ColumnOriginX[RowIndices1[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginY[RowIndices1[i]], CultureInfo.InvariantCulture)) * 1.00f;

                    Origins1[i] = new int3((int)(Pos.X - Shift.X),
                                           (int)(Pos.Y - Shift.Y),
                                           0);
                    ResidualShifts1[i] = new float3(-MathHelper.ResidualFraction(Pos.X - Shift.X),
                                                    -MathHelper.ResidualFraction(Pos.Y - Shift.Y),
                                                    0f);

                    tableIn.SetRowValue(RowIndices1[i], "rlnCoordinateX", Origins1[i].X.ToString());
                    tableIn.SetRowValue(RowIndices1[i], "rlnCoordinateY", Origins1[i].Y.ToString());
                    tableIn.SetRowValue(RowIndices1[i], "rlnOriginX", "0.0");
                    tableIn.SetRowValue(RowIndices1[i], "rlnOriginY", "0.0");
                }

                // Half2: Add translational shifts to coordinates, sans the fractional part
                for (int i = 0; i < NParticles2; i++)
                {
                    float2 Pos = new float2(float.Parse(ColumnPosX[RowIndices2[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosY[RowIndices2[i]], CultureInfo.InvariantCulture)) * 1.00f;
                    float2 Shift = new float2(float.Parse(ColumnOriginX[RowIndices2[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginY[RowIndices2[i]], CultureInfo.InvariantCulture)) * 1.00f;

                    Origins2[i] = new int3((int)(Pos.X - Shift.X),
                                           (int)(Pos.Y - Shift.Y),
                                           0);
                    ResidualShifts2[i] = new float3(-MathHelper.ResidualFraction(Pos.X - Shift.X),
                                                    -MathHelper.ResidualFraction(Pos.Y - Shift.Y),
                                                    0f);

                    tableIn.SetRowValue(RowIndices2[i], "rlnCoordinateX", Origins2[i].X.ToString());
                    tableIn.SetRowValue(RowIndices2[i], "rlnCoordinateY", Origins2[i].Y.ToString());
                    tableIn.SetRowValue(RowIndices2[i], "rlnOriginX", "0.0");
                    tableIn.SetRowValue(RowIndices2[i], "rlnOriginY", "0.0");
                }
            }

            #endregion

            #region Allocate memory for particle and PS stacks

            Image ParticleStackAll = new Image(new int3(DimsRegion.X, DimsRegion.Y, NParticles * Dims.Z));
            Image ParticleStack1 = new Image(new int3(DimsRegion.X, DimsRegion.Y, NParticles1 * Dims.Z));
            Image ParticleStack2 = new Image(new int3(DimsRegion.X, DimsRegion.Y, NParticles2 * Dims.Z));
            Image PSStackAll = new Image(new int3(DimsRegion.X, DimsRegion.Y, NParticles * Dims.Z), true);
            Image PSStack1 = new Image(new int3(DimsRegion.X, DimsRegion.Y, NParticles1 * Dims.Z), true);
            Image PSStack2 = new Image(new int3(DimsRegion.X, DimsRegion.Y, NParticles2 * Dims.Z), true);

            Image FrameParticles1 = new Image(IntPtr.Zero, new int3(DimsPadded.X, DimsPadded.Y, NParticles1));
            Image FrameParticles2 = new Image(IntPtr.Zero, new int3(DimsPadded.X, DimsPadded.Y, NParticles2));

            float[][] ParticleStackData = ParticleStackAll.GetHost(Intent.Write);
            float[][] ParticleStackData1 = ParticleStack1.GetHost(Intent.Write);
            float[][] ParticleStackData2 = ParticleStack2.GetHost(Intent.Write);
            float[][] PSStackData = PSStackAll.GetHost(Intent.Write);
            float[][] PSStackData1 = PSStack1.GetHost(Intent.Write);
            float[][] PSStackData2 = PSStack2.GetHost(Intent.Write);

            #endregion

            #region Create rows in outTable

            lock (tableOut)  // Creating rows in outTable, this absolutely needs to be staged sequentially
            {
                for (int z = 0; z < Dims.Z; z++)
                {
                    for (int i = 0; i < NParticles; i++)
                    {
                        int Index = i < NParticles1 ? RowIndices1[i] : RowIndices2[i - NParticles1];

                        string OriParticlePath = (i + 1).ToString("D6") + "@particles/" + RootName + "_particles.mrcs";
                        string ParticleName = (z * NParticles + i + 1).ToString("D6") + "@particlemovies/" + RootName + "_particles.mrcs";
                        string ParticleCTFName = (z * NParticles + i + 1).ToString("D6") + "@particlectfmovies/" + RootName + "_particlectf.mrcs";

                        List<string> NewRow = tableIn.GetRow(Index).Select(v => v).ToList(); // Get copy of original row.
                        NewRow[tableOut.GetColumnIndex("rlnOriginalParticleName")] = OriParticlePath;
                        NewRow[tableOut.GetColumnIndex("rlnAngleRotPrior")] = tableIn.GetRowValue(Index, "rlnAngleRot");
                        NewRow[tableOut.GetColumnIndex("rlnAngleTiltPrior")] = tableIn.GetRowValue(Index, "rlnAngleTilt");
                        NewRow[tableOut.GetColumnIndex("rlnAnglePsiPrior")] = tableIn.GetRowValue(Index, "rlnAnglePsi");
                        NewRow[tableOut.GetColumnIndex("rlnOriginXPrior")] = "0.0";
                        NewRow[tableOut.GetColumnIndex("rlnOriginYPrior")] = "0.0";

                        NewRow[tableOut.GetColumnIndex("rlnImageName")] = ParticleName;
                        NewRow[tableOut.GetColumnIndex("rlnCtfImage")] = ParticleCTFName;
                        NewRow[tableOut.GetColumnIndex("rlnMicrographName")] = (z + 1).ToString("D6") + "@stack/" + RootName + "_movie.mrcs";

                        TableOutIndices.Add(tableOut.RowCount);
                        tableOut.AddRow(NewRow);
                    }
                }
            }

            #endregion

            #region For every frame, extract particles from each half; shift, correct, and norm them

            float StepZ = 1f / Math.Max(Dims.Z - 1, 1);
            for (int z = 0; z < Dims.Z; z++)
            {
                float CoordZ = z * StepZ;

                #region Extract, correct, and norm particles

                #region Half 1
                {
                    if (originalStack != null)
                        GPU.Extract(originalStack.GetDeviceSlice(z, Intent.Read),
                                    FrameParticles1.GetDevice(Intent.Write),
                                    Dims.Slice(),
                                    DimsPadded,
                                    Helper.ToInterleaved(Origins1.Select(v => new int3(v.X - DimsPadded.X / 2, v.Y - DimsPadded.Y / 2, 0)).ToArray()),
                                    (uint)NParticles1);

                    // Shift particles
                    {
                        float3[] Shifts = new float3[NParticles1];

                        for (int i = 0; i < NParticles1; i++)
                        {
                            float3 Coords = new float3((float)Origins1[i].X / Dims.X, (float)Origins1[i].Y / Dims.Y, CoordZ);
                            Shifts[i] = ResidualShifts1[i] + new float3(GetShiftFromPyramid(Coords)) * 1.00f;
                        }
                        FrameParticles1.ShiftSlices(Shifts);
                    }

                    Image FrameParticlesCropped = FrameParticles1.AsPadded(new int2(DimsRegion));
                    Image FrameParticlesCorrected = FrameParticlesCropped.AsAnisotropyCorrected(new int2(DimsRegion),
                                                                                                PixelSize + PixelDelta / 2f,
                                                                                                PixelSize - PixelDelta / 2f,
                                                                                                PixelAngle,
                                                                                                6);
                    FrameParticlesCropped.Dispose();

                    GPU.NormParticles(FrameParticlesCorrected.GetDevice(Intent.Read),
                                      FrameParticlesCorrected.GetDevice(Intent.Write),
                                      DimsRegion,
                                      (uint)(particleradius / PixelSize),
                                      true,
                                      (uint)NParticles1);

                    float[][] FrameParticlesCorrectedData = FrameParticlesCorrected.GetHost(Intent.Read);
                    for (int n = 0; n < NParticles1; n++)
                    {
                        ParticleStackData[z * NParticles + n] = FrameParticlesCorrectedData[n];
                        ParticleStackData1[z * NParticles1 + n] = FrameParticlesCorrectedData[n];
                    }

                    //FrameParticlesCorrected.WriteMRC("intermediate_particles1.mrc");

                    FrameParticlesCorrected.Dispose();
                }
                #endregion

                #region Half 2
                {
                    if (originalStack != null)
                        GPU.Extract(originalStack.GetDeviceSlice(z, Intent.Read),
                                    FrameParticles2.GetDevice(Intent.Write),
                                    Dims.Slice(),
                                    DimsPadded,
                                    Helper.ToInterleaved(Origins2.Select(v => new int3(v.X - DimsPadded.X / 2, v.Y - DimsPadded.Y / 2, 0)).ToArray()),
                                    (uint)NParticles2);

                    // Shift particles
                    {
                        float3[] Shifts = new float3[NParticles2];

                        for (int i = 0; i < NParticles2; i++)
                        {
                            float3 Coords = new float3((float)Origins2[i].X / Dims.X, (float)Origins2[i].Y / Dims.Y, CoordZ);
                            Shifts[i] = ResidualShifts2[i] + new float3(GetShiftFromPyramid(Coords)) * 1.00f;
                        }
                        FrameParticles2.ShiftSlices(Shifts);
                    }

                    Image FrameParticlesCropped = FrameParticles2.AsPadded(new int2(DimsRegion));
                    Image FrameParticlesCorrected = FrameParticlesCropped.AsAnisotropyCorrected(new int2(DimsRegion),
                                                                                                PixelSize + PixelDelta / 2f,
                                                                                                PixelSize - PixelDelta / 2f,
                                                                                                PixelAngle,
                                                                                                6);
                    FrameParticlesCropped.Dispose();

                    GPU.NormParticles(FrameParticlesCorrected.GetDevice(Intent.Read),
                                      FrameParticlesCorrected.GetDevice(Intent.Write),
                                      DimsRegion,
                                      (uint)(particleradius / PixelSize),
                                      true,
                                      (uint)NParticles2);

                    float[][] FrameParticlesCorrectedData = FrameParticlesCorrected.GetHost(Intent.Read);
                    for (int n = 0; n < NParticles2; n++)
                    {
                        ParticleStackData[z * NParticles + NParticles1 + n] = FrameParticlesCorrectedData[n];
                        ParticleStackData2[z * NParticles2 + n] = FrameParticlesCorrectedData[n];
                    }

                    //FrameParticlesCorrected.WriteMRC("intermediate_particles2.mrc");

                    FrameParticlesCorrected.Dispose();
                }
                #endregion

                #endregion
                
                #region PS Half 1
                {
                    Image PS = new Image(new int3(DimsRegion.X, DimsRegion.Y, NParticles1), true);
                    PS.Fill(1f);

                    // Apply motion blur filter.

                    #region Motion blur weighting

                    {
                        const int Samples = 11;
                        float StartZ = (z - 0.5f) * StepZ;
                        float StopZ = (z + 0.5f) * StepZ;

                        float2[] Shifts = new float2[Samples * NParticles1];
                        for (int p = 0; p < NParticles1; p++)
                        {
                            float NormX = (float)Origins1[p].X / Dims.X;
                            float NormY = (float)Origins1[p].Y / Dims.Y;

                            for (int zz = 0; zz < Samples; zz++)
                            {
                                float zp = StartZ + (StopZ - StartZ) / (Samples - 1) * zz;
                                float3 Coords = new float3(NormX, NormY, zp);
                                Shifts[p * Samples + zz] = GetShiftFromPyramid(Coords) * 1.00f;
                            }
                        }

                        Image MotionFilter = new Image(IntPtr.Zero, new int3(DimsRegion.X, DimsRegion.Y, NParticles1), true);
                        GPU.CreateMotionBlur(MotionFilter.GetDevice(Intent.Write),
                                             DimsRegion,
                                             Helper.ToInterleaved(Shifts.Select(v => new float3(v.X, v.Y, 0)).ToArray()),
                                             Samples,
                                             (uint)NParticles1);
                        PS.Multiply(MotionFilter);
                        //MotionFilter.WriteMRC("motion.mrc");
                        MotionFilter.Dispose();
                    }

                    #endregion

                    float[][] PSData = PS.GetHost(Intent.Read);
                    for (int n = 0; n < NParticles1; n++)
                        PSStackData[z * NParticles + n] = PSData[n];

                    //PS.WriteMRC("intermediate_ps1.mrc");

                    PS.Dispose();
                }
                #endregion

                #region PS Half 2
                {
                    Image PS = new Image(new int3(DimsRegion.X, DimsRegion.Y, NParticles2), true);
                    PS.Fill(1f);

                    // Apply motion blur filter.

                    #region Motion blur weighting

                    {
                        const int Samples = 11;
                        float StartZ = (z - 0.5f) * StepZ;
                        float StopZ = (z + 0.5f) * StepZ;

                        float2[] Shifts = new float2[Samples * NParticles2];
                        for (int p = 0; p < NParticles2; p++)
                        {
                            float NormX = (float)Origins2[p].X / Dims.X;
                            float NormY = (float)Origins2[p].Y / Dims.Y;

                            for (int zz = 0; zz < Samples; zz++)
                            {
                                float zp = StartZ + (StopZ - StartZ) / (Samples - 1) * zz;
                                float3 Coords = new float3(NormX, NormY, zp);
                                Shifts[p * Samples + zz] = GetShiftFromPyramid(Coords) * 1.00f;
                            }
                        }

                        Image MotionFilter = new Image(IntPtr.Zero, new int3(DimsRegion.X, DimsRegion.Y, NParticles2), true);
                        GPU.CreateMotionBlur(MotionFilter.GetDevice(Intent.Write),
                                             DimsRegion,
                                             Helper.ToInterleaved(Shifts.Select(v => new float3(v.X, v.Y, 0)).ToArray()),
                                             Samples,
                                             (uint)NParticles2);
                        PS.Multiply(MotionFilter);
                        //MotionFilter.WriteMRC("motion.mrc");
                        MotionFilter.Dispose();
                    }

                    #endregion

                    float[][] PSData = PS.GetHost(Intent.Read);
                    for (int n = 0; n < NParticles2; n++)
                        PSStackData[z * NParticles + NParticles1 + n] = PSData[n];

                    //PS.WriteMRC("intermediate_ps2.mrc");

                    PS.Dispose();
                }
                #endregion
            }
            FrameParticles1.Dispose();
            FrameParticles2.Dispose();
            originalStack.FreeDevice();

            #endregion

            HeaderMRC ParticlesHeader = new HeaderMRC
            {
                Pixelsize = new float3(PixelSize, PixelSize, PixelSize)
            };

            // Do translation and rotation BFGS per particle
            {
                float MaxHigh = 2.6f;

                CubicGrid GridX = new CubicGrid(new int3(NParticles1, 1, 2));
                CubicGrid GridY = new CubicGrid(new int3(NParticles1, 1, 2));
                CubicGrid GridRot = new CubicGrid(new int3(NParticles1, 1, 2));
                CubicGrid GridTilt = new CubicGrid(new int3(NParticles1, 1, 2));
                CubicGrid GridPsi = new CubicGrid(new int3(NParticles1, 1, 2));

                int2 DimsCropped = new int2(DimsRegion / (MaxHigh / PixelSize / 2f)) / 2 * 2;

                #region Get coordinates for CTF and Fourier-space shifts
                Image CTFCoords;
                Image ShiftFactors;
                {
                    float2[] CTFCoordsData = new float2[(DimsCropped.X / 2 + 1) * DimsCropped.Y];
                    float2[] ShiftFactorsData = new float2[(DimsCropped.X / 2 + 1) * DimsCropped.Y];
                    for (int y = 0; y < DimsCropped.Y; y++)
                        for (int x = 0; x < DimsCropped.X / 2 + 1; x++)
                        {
                            int xx = x;
                            int yy = y < DimsCropped.Y / 2 + 1 ? y : y - DimsCropped.Y;

                            float xs = xx / (float)DimsRegion.X;
                            float ys = yy / (float)DimsRegion.Y;
                            float r = (float)Math.Sqrt(xs * xs + ys * ys);
                            float angle = (float)(Math.Atan2(yy, xx));

                            CTFCoordsData[y * (DimsCropped.X / 2 + 1) + x] = new float2(r / PixelSize, angle);
                            ShiftFactorsData[y * (DimsCropped.X / 2 + 1) + x] = new float2((float)-xx / DimsRegion.X * 2f * (float)Math.PI,
                                                                                          (float)-yy / DimsRegion.X * 2f * (float)Math.PI);
                        }

                    CTFCoords = new Image(CTFCoordsData, new int3(DimsCropped), true);
                    ShiftFactors = new Image(ShiftFactorsData, new int3(DimsCropped), true);
                }
                #endregion

                #region Get inverse sigma2 spectrum for this micrograph from Relion's model.star
                Image Sigma2Noise = new Image(new int3(DimsCropped), true);
                {
                    int GroupNumber = int.Parse(tableIn.GetRowValue(RowIndices[0], "rlnGroupNumber"));
                    //Star SigmaTable = new Star("D:\\rado27\\Refine3D\\run1_ct5_it009_half1_model.star", "data_model_group_" + GroupNumber);
                    Star SigmaTable = new Star(MainWindow.Options.ModelStarPath, "data_model_group_" + GroupNumber);
                    float[] SigmaValues = SigmaTable.GetColumn("rlnSigma2Noise").Select(v => float.Parse(v)).ToArray();

                    float[] Sigma2NoiseData = Sigma2Noise.GetHost(Intent.Write)[0];
                    Helper.ForEachElementFT(DimsCropped, (x, y, xx, yy, r, angle) =>
                    {
                        int ir = (int)r;
                        float val = 0;
                        if (ir < SigmaValues.Length && ir >= size / (50f / PixelSize) && ir < DimsCropped.X / 2)
                        {
                            if (SigmaValues[ir] != 0f)
                                val = 1f / SigmaValues[ir];
                        }
                        Sigma2NoiseData[y * (DimsCropped.X / 2 + 1) + x] = val;
                    });
                    float MaxSigma = MathHelper.Max(Sigma2NoiseData);
                    for (int i = 0; i < Sigma2NoiseData.Length; i++)
                        Sigma2NoiseData[i] /= MaxSigma;

                    Sigma2Noise.RemapToFT();
                }
                //Sigma2Noise.WriteMRC("d_sigma2noise.mrc");
                #endregion

                #region Initialize particle angles for both halves

                float3[] ParticleAngles1 = new float3[NParticles1];
                float3[] ParticleAngles2 = new float3[NParticles2];
                for (int p = 0; p < NParticles1; p++)
                    ParticleAngles1[p] = new float3(float.Parse(tableIn.GetRowValue(RowIndices1[p], "rlnAngleRot")),
                                                    float.Parse(tableIn.GetRowValue(RowIndices1[p], "rlnAngleTilt")),
                                                    float.Parse(tableIn.GetRowValue(RowIndices1[p], "rlnAnglePsi")));
                for (int p = 0; p < NParticles2; p++)
                    ParticleAngles2[p] = new float3(float.Parse(tableIn.GetRowValue(RowIndices2[p], "rlnAngleRot")),
                                                    float.Parse(tableIn.GetRowValue(RowIndices2[p], "rlnAngleTilt")),
                                                    float.Parse(tableIn.GetRowValue(RowIndices2[p], "rlnAnglePsi")));
                #endregion

                #region Prepare masks
                Image Masks1, Masks2;
                {
                    // Half 1
                    {
                        Image Volume = StageDataLoad.LoadMap(MainWindow.Options.MaskPath, new int2(1, 1), 0, typeof (float));
                        Image VolumePadded = Volume.AsPadded(Volume.Dims * MainWindow.Options.ProjectionOversample);
                        Volume.Dispose();
                        VolumePadded.RemapToFT(true);
                        Image VolMaskFT = VolumePadded.AsFFT(true);
                        VolumePadded.Dispose();

                        Image MasksFT = VolMaskFT.AsProjections(ParticleAngles1.Select(v => new float3(v.X * Helper.ToRad, v.Y * Helper.ToRad, v.Z * Helper.ToRad)).ToArray(),
                                                                new int2(DimsRegion),
                                                                MainWindow.Options.ProjectionOversample);
                        VolMaskFT.Dispose();

                        Masks1 = MasksFT.AsIFFT();
                        MasksFT.Dispose();

                        Masks1.RemapFromFT();

                        Parallel.ForEach(Masks1.GetHost(Intent.ReadWrite), slice =>
                        {
                            for (int i = 0; i < slice.Length; i++)
                                slice[i] = (Math.Max(2f, Math.Min(50f, slice[i])) - 2) / 48f;
                        });
                    }

                    // Half 2
                    {
                        Image Volume = StageDataLoad.LoadMap(MainWindow.Options.MaskPath, new int2(1, 1), 0, typeof(float));
                        Image VolumePadded = Volume.AsPadded(Volume.Dims * MainWindow.Options.ProjectionOversample);
                        Volume.Dispose();
                        VolumePadded.RemapToFT(true);
                        Image VolMaskFT = VolumePadded.AsFFT(true);
                        VolumePadded.Dispose();

                        Image MasksFT = VolMaskFT.AsProjections(ParticleAngles2.Select(v => new float3(v.X * Helper.ToRad, v.Y * Helper.ToRad, v.Z * Helper.ToRad)).ToArray(),
                                                                new int2(DimsRegion),
                                                                MainWindow.Options.ProjectionOversample);
                        VolMaskFT.Dispose();

                        Masks2 = MasksFT.AsIFFT();
                        MasksFT.Dispose();

                        Masks2.RemapFromFT();

                        Parallel.ForEach(Masks2.GetHost(Intent.ReadWrite), slice =>
                        {
                            for (int i = 0; i < slice.Length; i++)
                                slice[i] = (Math.Max(2f, Math.Min(50f, slice[i])) - 2) / 48f;
                        });
                    }
                }
                //Masks1.WriteMRC("d_masks1.mrc");
                //Masks2.WriteMRC("d_masks2.mrc");
                #endregion

                #region Load and prepare references for both halves
                Image VolRefFT1;
                {
                    Image Volume = StageDataLoad.LoadMap(MainWindow.Options.ReferencePath, new int2(1, 1), 0, typeof(float));
                    //GPU.Normalize(Volume.GetDevice(Intent.Read), Volume.GetDevice(Intent.Write), (uint)Volume.ElementsReal, 1);
                    Image VolumePadded = Volume.AsPadded(Volume.Dims * MainWindow.Options.ProjectionOversample);
                    Volume.Dispose();
                    VolumePadded.RemapToFT(true);
                    VolRefFT1 = VolumePadded.AsFFT(true);
                    VolumePadded.Dispose();
                }
                VolRefFT1.FreeDevice();

                Image VolRefFT2;
                {
                    // Can't assume there is a second half, but certainly hope so
                    string Half2Path = MainWindow.Options.ReferencePath;
                    if (Half2Path.Contains("half1"))
                        Half2Path = Half2Path.Replace("half1", "half2");

                    Image Volume = StageDataLoad.LoadMap(Half2Path, new int2(1, 1), 0, typeof(float));
                    //GPU.Normalize(Volume.GetDevice(Intent.Read), Volume.GetDevice(Intent.Write), (uint)Volume.ElementsReal, 1);
                    Image VolumePadded = Volume.AsPadded(Volume.Dims * MainWindow.Options.ProjectionOversample);
                    Volume.Dispose();
                    VolumePadded.RemapToFT(true);
                    VolRefFT2 = VolumePadded.AsFFT(true);
                    VolumePadded.Dispose();
                }
                VolRefFT2.FreeDevice();
                #endregion

                #region Prepare particles: group and resize to DimsCropped

                Image ParticleStackFT1 = new Image(IntPtr.Zero, new int3(DimsCropped.X, DimsCropped.Y, NParticles1 * Dims.Z / 3), true, true);
                {
                    GPU.CreatePolishing(ParticleStack1.GetDevice(Intent.Read),
                                        ParticleStackFT1.GetDevice(Intent.Write),
                                        Masks1.GetDevice(Intent.Read),
                                        new int2(DimsRegion),
                                        DimsCropped,
                                        NParticles1,
                                        Dims.Z);

                    ParticleStack1.FreeDevice();
                    Masks1.Dispose();

                    /*Image Amps = ParticleStackFT1.AsIFFT();
                    Amps.RemapFromFT();
                    Amps.WriteMRC("d_particlestackft1.mrc");
                    Amps.Dispose();*/
                }

                Image ParticleStackFT2 = new Image(IntPtr.Zero, new int3(DimsCropped.X, DimsCropped.Y, NParticles2 * Dims.Z / 3), true, true);
                {
                    GPU.CreatePolishing(ParticleStack2.GetDevice(Intent.Read),
                                        ParticleStackFT2.GetDevice(Intent.Write),
                                        Masks2.GetDevice(Intent.Read),
                                        new int2(DimsRegion),
                                        DimsCropped,
                                        NParticles2,
                                        Dims.Z);

                    ParticleStack1.FreeDevice();
                    Masks2.Dispose();

                    /*Image Amps = ParticleStackFT2.AsIFFT();
                    Amps.RemapFromFT();
                    Amps.WriteMRC("d_particlestackft2.mrc");
                    Amps.Dispose();*/
                }
                #endregion

                Image Projections1 = new Image(IntPtr.Zero, new int3(DimsCropped.X, DimsCropped.Y, NParticles1 * Dims.Z / 3), true, true);
                Image Projections2 = new Image(IntPtr.Zero, new int3(DimsCropped.X, DimsCropped.Y, NParticles2 * Dims.Z / 3), true, true);

                Image Shifts1 = new Image(new int3(NParticles1, Dims.Z / 3, 1), false, true);
                float3[] Angles1 = new float3[NParticles1 * Dims.Z / 3];
                CTFStruct[] CTFParams1 = new CTFStruct[NParticles1 * Dims.Z / 3];

                Image Shifts2 = new Image(new int3(NParticles2, Dims.Z / 3, 1), false, true);
                float3[] Angles2 = new float3[NParticles2 * Dims.Z / 3];
                CTFStruct[] CTFParams2 = new CTFStruct[NParticles2 * Dims.Z / 3];

                float[] BFacs =
                {
                    -3.86f,
                    0.00f,
                    -17.60f,
                    -35.24f,
                    -57.48f,
                    -93.51f,
                    -139.57f,
                    -139.16f
                };

                #region Initialize defocus and phase shift values
                float[] InitialDefoci1 = new float[NParticles1 * (Dims.Z / 3)];
                float[] InitialPhaseShifts1 = new float[NParticles1 * (Dims.Z / 3)];
                float[] InitialDefoci2 = new float[NParticles2 * (Dims.Z / 3)];
                float[] InitialPhaseShifts2 = new float[NParticles2 * (Dims.Z / 3)];
                for (int z = 0, i = 0; z < Dims.Z / 3; z++)
                {
                    for (int p = 0; p < NParticles1; p++, i++)
                    {
                        InitialDefoci1[i] = GridCTF.GetInterpolated(new float3((float)Origins1[p].X / Dims.X,
                                                                               (float)Origins1[p].Y / Dims.Y,
                                                                               (float)(z * 3 + 1) / (Dims.Z - 1)));
                        InitialPhaseShifts1[i] = GridCTFPhase.GetInterpolated(new float3((float)Origins1[p].X / Dims.X,
                                                                                         (float)Origins1[p].Y / Dims.Y,
                                                                                         (float)(z * 3 + 1) / (Dims.Z - 1)));

                        CTF Alt = CTF.GetCopy();
                        Alt.PixelSize = (decimal)PixelSize;
                        Alt.PixelSizeDelta = 0;
                        Alt.Defocus = (decimal)InitialDefoci1[i];
                        Alt.PhaseShift = (decimal)InitialPhaseShifts1[i];
                        //Alt.Bfactor = (decimal)BFacs[z];

                        CTFParams1[i] = Alt.ToStruct();
                    }
                }
                for (int z = 0, i = 0; z < Dims.Z / 3; z++)
                {
                    for (int p = 0; p < NParticles2; p++, i++)
                    {
                        InitialDefoci2[i] = GridCTF.GetInterpolated(new float3((float)Origins2[p].X / Dims.X,
                                                                               (float)Origins2[p].Y / Dims.Y,
                                                                               (float)(z * 3 + 1) / (Dims.Z - 1)));
                        InitialPhaseShifts2[i] = GridCTFPhase.GetInterpolated(new float3((float)Origins2[p].X / Dims.X,
                                                                                         (float)Origins2[p].Y / Dims.Y,
                                                                                         (float)(z * 3 + 1) / (Dims.Z - 1)));

                        CTF Alt = CTF.GetCopy();
                        Alt.PixelSize = (decimal)PixelSize;
                        Alt.PixelSizeDelta = 0;
                        Alt.Defocus = (decimal)InitialDefoci2[i];
                        Alt.PhaseShift = (decimal)InitialPhaseShifts2[i];
                        //Alt.Bfactor = (decimal)BFacs[z];

                        CTFParams2[i] = Alt.ToStruct();
                    }
                }
                #endregion

                #region SetPositions lambda
                Action<double[]> SetPositions = input =>
                {
                    float BorderZ = 0.5f / (Dims.Z / 3);

                    GridX = new CubicGrid(new int3(NParticles, 1, 2), input.Take(NParticles * 2).Select(v => (float)v).ToArray());
                    GridY = new CubicGrid(new int3(NParticles, 1, 2), input.Skip(NParticles * 2 * 1).Take(NParticles * 2).Select(v => (float)v).ToArray());

                    float[] AlteredX = GridX.GetInterpolatedNative(new int3(NParticles, 1, Dims.Z / 3), new float3(0, 0, BorderZ));
                    float[] AlteredY = GridY.GetInterpolatedNative(new int3(NParticles, 1, Dims.Z / 3), new float3(0, 0, BorderZ));

                    GridRot = new CubicGrid(new int3(NParticles, 1, 2), input.Skip(NParticles * 2 * 2).Take(NParticles * 2).Select(v => (float)v).ToArray());
                    GridTilt = new CubicGrid(new int3(NParticles, 1, 2), input.Skip(NParticles * 2 * 3).Take(NParticles * 2).Select(v => (float)v).ToArray());
                    GridPsi = new CubicGrid(new int3(NParticles, 1, 2), input.Skip(NParticles * 2 * 4).Take(NParticles * 2).Select(v => (float)v).ToArray());

                    float[] AlteredRot = GridRot.GetInterpolatedNative(new int3(NParticles, 1, Dims.Z / 3), new float3(0, 0, BorderZ));
                    float[] AlteredTilt = GridTilt.GetInterpolatedNative(new int3(NParticles, 1, Dims.Z / 3), new float3(0, 0, BorderZ));
                    float[] AlteredPsi = GridPsi.GetInterpolatedNative(new int3(NParticles, 1, Dims.Z / 3), new float3(0, 0, BorderZ));

                    float[] ShiftData1 = Shifts1.GetHost(Intent.Write)[0];
                    float[] ShiftData2 = Shifts2.GetHost(Intent.Write)[0];

                    for (int z = 0; z < Dims.Z / 3; z++)
                    {
                        // Half 1
                        for (int p = 0; p < NParticles1; p++)
                        {
                            int i1 = z * NParticles1 + p;
                            int i = z * NParticles + p;
                            ShiftData1[i1 * 2] = AlteredX[i];
                            ShiftData1[i1 * 2 + 1] = AlteredY[i];

                            Angles1[i1] = new float3(AlteredRot[i] * 1f * Helper.ToRad, AlteredTilt[i] * 1f * Helper.ToRad, AlteredPsi[i] * 1f * Helper.ToRad);
                        }

                        // Half 2
                        for (int p = 0; p < NParticles2; p++)
                        {
                            int i2 = z * NParticles2 + p;
                            int i = z * NParticles + NParticles1 + p;
                            ShiftData2[i2 * 2] = AlteredX[i];
                            ShiftData2[i2 * 2 + 1] = AlteredY[i];

                            Angles2[i2] = new float3(AlteredRot[i] * 1f * Helper.ToRad, AlteredTilt[i] * 1f * Helper.ToRad, AlteredPsi[i] * 1f * Helper.ToRad);
                        }
                    }
                };
                #endregion

                #region EvalIndividuals lambda
                Func<double[], bool, double[]> EvalIndividuals = (input, redoProj) =>
                {
                    SetPositions(input);

                    if (redoProj)
                    {
                        GPU.ProjectForward(VolRefFT1.GetDevice(Intent.Read),
                                           Projections1.GetDevice(Intent.Write),
                                           VolRefFT1.Dims,
                                           DimsCropped,
                                           Helper.ToInterleaved(Angles1),
                                           MainWindow.Options.ProjectionOversample,
                                           (uint)(NParticles1 * Dims.Z / 3));

                        GPU.ProjectForward(VolRefFT2.GetDevice(Intent.Read),
                                           Projections2.GetDevice(Intent.Write),
                                           VolRefFT2.Dims,
                                           DimsCropped,
                                           Helper.ToInterleaved(Angles2),
                                           MainWindow.Options.ProjectionOversample,
                                           (uint)(NParticles2 * Dims.Z / 3));
                    }

                    /*{
                        Image ProjectionsAmps = Projections1.AsIFFT();
                        ProjectionsAmps.RemapFromFT();
                        ProjectionsAmps.WriteMRC("d_projectionsamps1.mrc");
                        ProjectionsAmps.Dispose();
                    }
                    {
                        Image ProjectionsAmps = Projections2.AsIFFT();
                        ProjectionsAmps.RemapFromFT();
                        ProjectionsAmps.WriteMRC("d_projectionsamps2.mrc");
                        ProjectionsAmps.Dispose();
                    }*/

                    float[] Diff1 = new float[NParticles1];
                    float[] DiffAll1 = new float[NParticles1 * (Dims.Z / 3)];
                    GPU.PolishingGetDiff(ParticleStackFT1.GetDevice(Intent.Read),
                                         Projections1.GetDevice(Intent.Read),
                                         ShiftFactors.GetDevice(Intent.Read),
                                         CTFCoords.GetDevice(Intent.Read),
                                         CTFParams1,
                                         Sigma2Noise.GetDevice(Intent.Read),
                                         DimsCropped,
                                         Shifts1.GetDevice(Intent.Read),
                                         Diff1,
                                         DiffAll1,
                                         (uint)NParticles1,
                                         (uint)Dims.Z / 3);

                    float[] Diff2 = new float[NParticles2];
                    float[] DiffAll2 = new float[NParticles2 * (Dims.Z / 3)];
                    GPU.PolishingGetDiff(ParticleStackFT2.GetDevice(Intent.Read),
                                         Projections2.GetDevice(Intent.Read),
                                         ShiftFactors.GetDevice(Intent.Read),
                                         CTFCoords.GetDevice(Intent.Read),
                                         CTFParams2,
                                         Sigma2Noise.GetDevice(Intent.Read),
                                         DimsCropped,
                                         Shifts2.GetDevice(Intent.Read),
                                         Diff2,
                                         DiffAll2,
                                         (uint)NParticles2,
                                         (uint)Dims.Z / 3);

                    double[] DiffBoth = new double[NParticles];
                    for (int p = 0; p < NParticles1; p++)
                        DiffBoth[p] = Diff1[p];
                    for (int p = 0; p < NParticles2; p++)
                        DiffBoth[NParticles1 + p] = Diff2[p];

                    return DiffBoth;
                };
                #endregion

                Func<double[], double> Eval = input =>
                {
                    float Result = MathHelper.Mean(EvalIndividuals(input, true).Select(v => (float)v)) * NParticles;
                    Debug.WriteLine(Result);
                    return Result;
                };

                Func<double[], double[]> Grad = input =>
                {
                    SetPositions(input);

                    GPU.ProjectForward(VolRefFT1.GetDevice(Intent.Read),
                                       Projections1.GetDevice(Intent.Write),
                                       VolRefFT1.Dims,
                                       DimsCropped,
                                       Helper.ToInterleaved(Angles1),
                                       MainWindow.Options.ProjectionOversample,
                                       (uint)(NParticles1 * Dims.Z / 3));

                    GPU.ProjectForward(VolRefFT2.GetDevice(Intent.Read),
                                       Projections2.GetDevice(Intent.Write),
                                       VolRefFT2.Dims,
                                       DimsCropped,
                                       Helper.ToInterleaved(Angles2),
                                       MainWindow.Options.ProjectionOversample,
                                       (uint)(NParticles2 * Dims.Z / 3));

                    double[] Result = new double[input.Length];

                    double Step = 0.1;
                    int NVariables = 10;    // (Shift + Euler) * 2
                    for (int v = 0; v < NVariables; v++)
                    {
                        double[] InputPlus = new double[input.Length];
                        for (int i = 0; i < input.Length; i++)
                        {
                            int iv = i / NParticles;

                            if (iv == v)
                                InputPlus[i] = input[i] + Step;
                            else
                                InputPlus[i] = input[i];
                        }
                        double[] ScorePlus = EvalIndividuals(InputPlus, v >= 4);

                        double[] InputMinus = new double[input.Length];
                        for (int i = 0; i < input.Length; i++)
                        {
                            int iv = i / NParticles;

                            if (iv == v)
                                InputMinus[i] = input[i] - Step;
                            else
                                InputMinus[i] = input[i];
                        }
                        double[] ScoreMinus = EvalIndividuals(InputMinus, v >= 4);

                        for (int i = 0; i < NParticles; i++)
                            Result[v * NParticles + i] = (ScorePlus[i] - ScoreMinus[i]) / (Step * 2.0);
                    }

                    return Result;
                };

                double[] StartParams = new double[NParticles * 2 * 5];
                
                for (int i = 0; i < NParticles * 2; i++)
                {
                    int p = i % NParticles;
                    StartParams[NParticles * 2 * 0 + i] = 0;
                    StartParams[NParticles * 2 * 1 + i] = 0;

                    if (p < NParticles1)
                    {
                        StartParams[NParticles * 2 * 2 + i] = ParticleAngles1[p].X / 1.0;
                        StartParams[NParticles * 2 * 3 + i] = ParticleAngles1[p].Y / 1.0;
                        StartParams[NParticles * 2 * 4 + i] = ParticleAngles1[p].Z / 1.0;
                    }
                    else
                    {
                        p -= NParticles1;
                        StartParams[NParticles * 2 * 2 + i] = ParticleAngles2[p].X / 1.0;
                        StartParams[NParticles * 2 * 3 + i] = ParticleAngles2[p].Y / 1.0;
                        StartParams[NParticles * 2 * 4 + i] = ParticleAngles2[p].Z / 1.0;
                    }
                }

                BroydenFletcherGoldfarbShanno Optimizer = new BroydenFletcherGoldfarbShanno(StartParams.Length, Eval, Grad);
                Optimizer.Epsilon = 3e-7;
                
                Optimizer.Maximize(StartParams);

                #region Calculate particle quality for high frequencies
                float[] ParticleQuality = new float[NParticles * (Dims.Z / 3)];
                {
                    Sigma2Noise.Dispose();
                    Sigma2Noise = new Image(new int3(DimsCropped), true);
                    {
                        int GroupNumber = int.Parse(tableIn.GetRowValue(RowIndices[0], "rlnGroupNumber"));
                        //Star SigmaTable = new Star("D:\\rado27\\Refine3D\\run1_ct5_it009_half1_model.star", "data_model_group_" + GroupNumber);
                        Star SigmaTable = new Star(MainWindow.Options.ModelStarPath, "data_model_group_" + GroupNumber);
                        float[] SigmaValues = SigmaTable.GetColumn("rlnSigma2Noise").Select(v => float.Parse(v)).ToArray();

                        float[] Sigma2NoiseData = Sigma2Noise.GetHost(Intent.Write)[0];
                        Helper.ForEachElementFT(DimsCropped, (x, y, xx, yy, r, angle) =>
                        {
                            int ir = (int)r;
                            float val = 0;
                            if (ir < SigmaValues.Length && ir >= size / (4.0f / PixelSize) && ir < DimsCropped.X / 2)
                            {
                                if (SigmaValues[ir] != 0f)
                                    val = 1f / SigmaValues[ir] / (ir * 3.14f);
                            }
                            Sigma2NoiseData[y * (DimsCropped.X / 2 + 1) + x] = val;
                        });
                        float MaxSigma = MathHelper.Max(Sigma2NoiseData);
                        for (int i = 0; i < Sigma2NoiseData.Length; i++)
                            Sigma2NoiseData[i] /= MaxSigma;

                        Sigma2Noise.RemapToFT();
                    }
                    //Sigma2Noise.WriteMRC("d_sigma2noiseScore.mrc");

                    SetPositions(StartParams);

                    GPU.ProjectForward(VolRefFT1.GetDevice(Intent.Read),
                                       Projections1.GetDevice(Intent.Write),
                                       VolRefFT1.Dims,
                                       DimsCropped,
                                       Helper.ToInterleaved(Angles1),
                                       MainWindow.Options.ProjectionOversample,
                                       (uint)(NParticles1 * Dims.Z / 3));

                    GPU.ProjectForward(VolRefFT2.GetDevice(Intent.Read),
                                       Projections2.GetDevice(Intent.Write),
                                       VolRefFT2.Dims,
                                       DimsCropped,
                                       Helper.ToInterleaved(Angles2),
                                       MainWindow.Options.ProjectionOversample,
                                       (uint)(NParticles2 * Dims.Z / 3));

                    float[] Diff1 = new float[NParticles1];
                    float[] ParticleQuality1 = new float[NParticles1 * (Dims.Z / 3)];
                    GPU.PolishingGetDiff(ParticleStackFT1.GetDevice(Intent.Read),
                                         Projections1.GetDevice(Intent.Read),
                                         ShiftFactors.GetDevice(Intent.Read),
                                         CTFCoords.GetDevice(Intent.Read),
                                         CTFParams1,
                                         Sigma2Noise.GetDevice(Intent.Read),
                                         DimsCropped,
                                         Shifts1.GetDevice(Intent.Read),
                                         Diff1,
                                         ParticleQuality1,
                                         (uint)NParticles1,
                                         (uint)Dims.Z / 3);

                    float[] Diff2 = new float[NParticles2];
                    float[] ParticleQuality2 = new float[NParticles2 * (Dims.Z / 3)];
                    GPU.PolishingGetDiff(ParticleStackFT2.GetDevice(Intent.Read),
                                         Projections2.GetDevice(Intent.Read),
                                         ShiftFactors.GetDevice(Intent.Read),
                                         CTFCoords.GetDevice(Intent.Read),
                                         CTFParams2,
                                         Sigma2Noise.GetDevice(Intent.Read),
                                         DimsCropped,
                                         Shifts2.GetDevice(Intent.Read),
                                         Diff2,
                                         ParticleQuality2,
                                         (uint)NParticles2,
                                         (uint)Dims.Z / 3);

                    for (int z = 0; z < Dims.Z / 3; z++)
                    {
                        for (int p = 0; p < NParticles1; p++)
                            ParticleQuality[z * NParticles + p] = ParticleQuality1[z * NParticles1 + p];

                        for (int p = 0; p < NParticles2; p++)
                            ParticleQuality[z * NParticles + NParticles1 + p] = ParticleQuality2[z * NParticles2 + p];
                    }
                }
                #endregion

                lock (tableOut)     // Only changing cell values, but better be safe in case table implementation changes later
                {
                    GridX = new CubicGrid(new int3(NParticles, 1, 2), Optimizer.Solution.Take(NParticles * 2).Select(v => (float)v).ToArray());
                    GridY = new CubicGrid(new int3(NParticles, 1, 2), Optimizer.Solution.Skip(NParticles * 2 * 1).Take(NParticles * 2).Select(v => (float)v).ToArray());
                    float[] AlteredX = GridX.GetInterpolated(new int3(NParticles, 1, Dims.Z), new float3(0, 0, 0));
                    float[] AlteredY = GridY.GetInterpolated(new int3(NParticles, 1, Dims.Z), new float3(0, 0, 0));

                    GridRot = new CubicGrid(new int3(NParticles, 1, 2), Optimizer.Solution.Skip(NParticles * 2 * 2).Take(NParticles * 2).Select(v => (float)v).ToArray());
                    GridTilt = new CubicGrid(new int3(NParticles, 1, 2), Optimizer.Solution.Skip(NParticles * 2 * 3).Take(NParticles * 2).Select(v => (float)v).ToArray());
                    GridPsi = new CubicGrid(new int3(NParticles, 1, 2), Optimizer.Solution.Skip(NParticles * 2 * 4).Take(NParticles * 2).Select(v => (float)v).ToArray());
                    float[] AlteredRot = GridRot.GetInterpolated(new int3(NParticles, 1, Dims.Z), new float3(0, 0, 0));
                    float[] AlteredTilt = GridTilt.GetInterpolated(new int3(NParticles, 1, Dims.Z), new float3(0, 0, 0));
                    float[] AlteredPsi = GridPsi.GetInterpolated(new int3(NParticles, 1, Dims.Z), new float3(0, 0, 0));
                    
                    for (int i = 0; i < TableOutIndices.Count; i++)
                    {
                        int p = i % NParticles;
                        int z = i / NParticles;
                        float Defocus = 0, PhaseShift = 0;

                        if (p < NParticles1)
                        {
                            Defocus = GridCTF.GetInterpolated(new float3((float)Origins1[p].X / Dims.X,
                                                                         (float)Origins1[p].Y / Dims.Y,
                                                                         (float)z / (Dims.Z - 1)));
                            PhaseShift = GridCTFPhase.GetInterpolated(new float3((float)Origins1[p].X / Dims.X,
                                                                                 (float)Origins1[p].Y / Dims.Y,
                                                                                 (float)z / (Dims.Z - 1)));
                        }
                        else
                        {
                            p -= NParticles1;
                            Defocus = GridCTF.GetInterpolated(new float3((float)Origins2[p].X / Dims.X,
                                                                         (float)Origins2[p].Y / Dims.Y,
                                                                         (float)z / (Dims.Z - 1)));
                            PhaseShift = GridCTFPhase.GetInterpolated(new float3((float)Origins2[p].X / Dims.X,
                                                                                 (float)Origins2[p].Y / Dims.Y,
                                                                                 (float)z / (Dims.Z - 1)));
                        }

                        tableOut.SetRowValue(TableOutIndices[i], "rlnOriginX", AlteredX[i].ToString(CultureInfo.InvariantCulture));
                        tableOut.SetRowValue(TableOutIndices[i], "rlnOriginY", AlteredY[i].ToString(CultureInfo.InvariantCulture));
                        tableOut.SetRowValue(TableOutIndices[i], "rlnAngleRot", (-AlteredRot[i]).ToString(CultureInfo.InvariantCulture));
                        tableOut.SetRowValue(TableOutIndices[i], "rlnAngleTilt", (-AlteredTilt[i]).ToString(CultureInfo.InvariantCulture));
                        tableOut.SetRowValue(TableOutIndices[i], "rlnAnglePsi", (-AlteredPsi[i]).ToString(CultureInfo.InvariantCulture));
                        tableOut.SetRowValue(TableOutIndices[i], "rlnDefocusU", ((Defocus + (float)CTF.DefocusDelta / 2f) * 1e4f).ToString(CultureInfo.InvariantCulture));
                        tableOut.SetRowValue(TableOutIndices[i], "rlnDefocusV", ((Defocus - (float)CTF.DefocusDelta / 2f) * 1e4f).ToString(CultureInfo.InvariantCulture));
                        tableOut.SetRowValue(TableOutIndices[i], "rlnPhaseShift", (PhaseShift * 180f).ToString(CultureInfo.InvariantCulture));
                        tableOut.SetRowValue(TableOutIndices[i], "rlnCtfFigureOfMerit", (ParticleQuality[(z / 3) * NParticles + (i % NParticles)]).ToString(CultureInfo.InvariantCulture));

                        tableOut.SetRowValue(TableOutIndices[i], "rlnMagnification", ((float)MainWindow.Options.CTFDetectorPixel * 10000f / PixelSize).ToString());
                    }
                }

                VolRefFT1.Dispose();
                VolRefFT2.Dispose();
                Projections1.Dispose();
                Projections2.Dispose();
                Sigma2Noise.Dispose();
                ParticleStackFT1.Dispose();
                ParticleStackFT2.Dispose();
                Shifts1.Dispose();
                Shifts2.Dispose();
                CTFCoords.Dispose();
                ShiftFactors.Dispose();

                ParticleStack1.Dispose();
                ParticleStack2.Dispose();
                PSStack1.Dispose();
                PSStack2.Dispose();
            }
            
            // Write movies to disk asynchronously, so the next micrograph can load.
            Thread SaveThread = new Thread(() =>
            {
                GPU.SetDevice(CurrentDevice);   // It's a separate thread, make sure it's using the same device

                ParticleStackAll.WriteMRC(ParticleMoviesPath, ParticlesHeader);
                //ParticleStackAll.WriteMRC("D:\\gala\\particlemovies\\" + RootName + "_particles.mrcs", ParticlesHeader);
                ParticleStackAll.Dispose();

                PSStackAll.WriteMRC(ParticleCTFMoviesPath);
                //PSStackAll.WriteMRC("D:\\rado27\\particlectfmovies\\" + RootName + "_particlectf.mrcs");
                PSStackAll.Dispose();
            });
            SaveThread.Start();
        }

        public void ExportParticlesMovieOld(Star table, int size)
        {
            List<int> RowIndices = new List<int>();
            string[] ColumnMicrographName = table.GetColumn("rlnMicrographName");
            for (int i = 0; i < ColumnMicrographName.Length; i++)
                if (ColumnMicrographName[i].Contains(RootName))
                    RowIndices.Add(i);

            if (RowIndices.Count == 0)
                return;

            if (!Directory.Exists(ParticlesDir))
                Directory.CreateDirectory(ParticlesDir);
            if (!Directory.Exists(ParticleCTFDir))
                Directory.CreateDirectory(ParticleCTFDir);

            MapHeader OriginalHeader = MapHeader.ReadFromFile(Path,
                                                              new int2(MainWindow.Options.InputDatWidth, MainWindow.Options.InputDatHeight),
                                                              MainWindow.Options.InputDatOffset,
                                                              ImageFormatsHelper.StringToType(MainWindow.Options.InputDatType));
            /*Image OriginalStack = StageDataLoad.LoadMap(Path,
                                                        new int2(MainWindow.Options.InputDatWidth, MainWindow.Options.InputDatHeight),
                                                        MainWindow.Options.InputDatOffset,
                                                        ImageFormatsHelper.StringToType(MainWindow.Options.InputDatType));*/

            //OriginalStack.Xray(20f);

            int3 Dims = OriginalHeader.Dimensions;
            int3 DimsRegion = new int3(size, size, 1);
            int NParticles = RowIndices.Count / Dimensions.Z;

            float PixelSize = (float)(MainWindow.Options.CTFPixelMin + MainWindow.Options.CTFPixelMax) * 0.5f;
            float PixelDelta = (float)(MainWindow.Options.CTFPixelMax - MainWindow.Options.CTFPixelMin) * 0.5f;
            float PixelAngle = (float)MainWindow.Options.CTFPixelAngle / (float)(180.0 / Math.PI);
            Image CTFCoords;
            {
                float2[] CTFCoordsData = new float2[DimsRegion.ElementsSlice()];
                //Helper.ForEachElementFT(new int2(DimsRegion), (x, y, xx, yy) =>
                for (int y = 0; y < DimsRegion.Y; y++)
                    for (int x = 0; x < DimsRegion.X / 2 + 1; x++)
                    {
                        int xx = x;
                        int yy = y < DimsRegion.Y / 2 + 1 ? y : y - DimsRegion.Y;

                        float xs = xx / (float)DimsRegion.X;
                        float ys = yy / (float)DimsRegion.Y;
                        float r = (float)Math.Sqrt(xs * xs + ys * ys);
                        float angle = (float)(Math.Atan2(yy, xx));
                        float CurrentPixelSize = PixelSize + PixelDelta * (float)Math.Cos(2f * (angle - PixelAngle));

                        CTFCoordsData[y * (DimsRegion.X / 2 + 1) + x] = new float2(r / CurrentPixelSize, angle);
                    } //);

                CTFCoords = new Image(CTFCoordsData, DimsRegion.Slice(), true);
                //CTFCoords.RemapToFT();
            }
            Image CTFFreq = CTFCoords.AsReal();

            Image CTFStack = new Image(new int3(size, size, NParticles * Dimensions.Z), true);
            int CTFStackIndex = 0;

            string[] ColumnPosX = table.GetColumn("rlnCoordinateX");
            string[] ColumnPosY = table.GetColumn("rlnCoordinateY");
            int3[] Origins = new int3[NParticles];
            for (int i = 0; i < NParticles; i++)
                Origins[i] = new int3((int)double.Parse(ColumnPosX[RowIndices[i]]) - DimsRegion.X * 2 / 2,
                                      (int)double.Parse(ColumnPosY[RowIndices[i]]) - DimsRegion.Y * 2 / 2,
                                      0);

            int IndexOffset = RowIndices[0];

            string[] ColumnOriginX = table.GetColumn("rlnOriginX");
            string[] ColumnOriginY = table.GetColumn("rlnOriginY");
            string[] ColumnPriorX = table.GetColumn("rlnOriginXPrior");
            string[] ColumnPriorY = table.GetColumn("rlnOriginYPrior");
            float2[] ShiftPriors = new float2[NParticles];
            for (int i = 0; i < NParticles; i++)
                ShiftPriors[i] = new float2(float.Parse(ColumnPriorX[IndexOffset + i]),
                                            float.Parse(ColumnPriorY[IndexOffset + i]));

            float2[][] ParticleTracks = new float2[NParticles][];
            for (int i = 0; i < NParticles; i++)
            {
                ParticleTracks[i] = new float2[Dimensions.Z];
                for (int z = 0; z < Dimensions.Z; z++)
                    ParticleTracks[i][z] = new float2(float.Parse(ColumnOriginX[IndexOffset + z * NParticles + i]) - ShiftPriors[i].X,
                                                      float.Parse(ColumnOriginY[IndexOffset + z * NParticles + i]) - ShiftPriors[i].Y);
            }

            Image AverageFT = new Image(new int3(DimsRegion.X, DimsRegion.Y, NParticles), true, true);
            Image AveragePS = new Image(new int3(DimsRegion.X, DimsRegion.Y, NParticles), true);
            Image Weights = new Image(new int3(DimsRegion.X, DimsRegion.Y, NParticles), true);
            Weights.Fill(1e-6f);
            Image FrameParticles = new Image(IntPtr.Zero, new int3(DimsRegion.X * 2, DimsRegion.Y * 2, NParticles));

            float StepZ = 1f / Math.Max(Dims.Z - 1, 1);
            for (int z = 0; z < Dims.Z; z++)
            {
                float CoordZ = z * StepZ;

                /*GPU.Extract(OriginalStack.GetDeviceSlice(z, Intent.Read),
                            FrameParticles.GetDevice(Intent.Write),
                            Dims.Slice(),
                            new int3(DimsRegion.X * 2, DimsRegion.Y * 2, 1),
                            Helper.ToInterleaved(Origins),
                            (uint)NParticles);*/

                // Shift particles
                {
                    float3[] Shifts = new float3[NParticles];

                    for (int i = 0; i < NParticles; i++)
                    {
                        float NormX = Math.Max(0.15f, Math.Min((float)Origins[i].X / Dims.X, 0.85f));
                        float NormY = Math.Max(0.15f, Math.Min((float)Origins[i].Y / Dims.Y, 0.85f));
                        float3 Coords = new float3(NormX, NormY, CoordZ);
                        Shifts[i] = new float3(GridMovementX.GetInterpolated(Coords) + ParticleTracks[i][z].X,
                                               GridMovementY.GetInterpolated(Coords) + ParticleTracks[i][z].Y,
                                               0f);
                    }
                    FrameParticles.ShiftSlices(Shifts);
                }

                Image FrameParticlesCropped = FrameParticles.AsPadded(new int2(DimsRegion));
                Image FrameParticlesFT = FrameParticlesCropped.AsFFT();
                FrameParticlesCropped.Dispose();

                Image PS = new Image(new int3(DimsRegion.X, DimsRegion.Y, NParticles), true);
                PS.Fill(1f);

                // Apply motion blur filter.
                {
                    const int Samples = 11;
                    float StartZ = (z - 0.75f) * StepZ;
                    float StopZ = (z + 0.75f) * StepZ;

                    float2[] Shifts = new float2[Samples * NParticles];
                    for (int p = 0; p < NParticles; p++)
                    {
                        float NormX = Math.Max(0.15f, Math.Min((float)Origins[p].X / Dims.X, 0.85f));
                        float NormY = Math.Max(0.15f, Math.Min((float)Origins[p].Y / Dims.Y, 0.85f));

                        for (int zz = 0; zz < Samples; zz++)
                        {
                            float zp = StartZ + (StopZ - StartZ) / (Samples - 1) * zz;
                            float3 Coords = new float3(NormX, NormY, zp);
                            Shifts[p * Samples + zz] = new float2(GridMovementX.GetInterpolated(Coords),
                                                                  GridMovementY.GetInterpolated(Coords));
                        }
                    }

                    Image MotionFilter = new Image(IntPtr.Zero, new int3(DimsRegion.X, DimsRegion.Y, NParticles), true);
                    GPU.CreateMotionBlur(MotionFilter.GetDevice(Intent.Write),
                                         DimsRegion,
                                         Helper.ToInterleaved(Shifts.Select(v => new float3(v.X, v.Y, 0)).ToArray()),
                                         Samples,
                                         (uint)NParticles);
                    PS.Multiply(MotionFilter);
                    //MotionFilter.WriteMRC("motion.mrc");
                    MotionFilter.Dispose();
                }

                // Apply CTF.
                if (CTF != null)
                {
                    CTFStruct[] Structs = new CTFStruct[NParticles];
                    for (int p = 0; p < NParticles; p++)
                    {
                        CTF Altered = CTF.GetCopy();
                        Altered.Defocus = (decimal)GridCTF.GetInterpolated(new float3(float.Parse(ColumnPosX[RowIndices[p]]) / Dims.X,
                                                                                      float.Parse(ColumnPosY[RowIndices[p]]) / Dims.Y,
                                                                                      z * StepZ));

                        Structs[p] = Altered.ToStruct();
                    }

                    Image CTFImage = new Image(IntPtr.Zero, new int3(DimsRegion.X, DimsRegion.Y, NParticles), true);
                    GPU.CreateCTF(CTFImage.GetDevice(Intent.Write),
                                  CTFCoords.GetDevice(Intent.Read),
                                  (uint)CTFCoords.ElementsSliceComplex,
                                  Structs,
                                  false,
                                  (uint)NParticles);

                    //CTFImage.Abs();
                    PS.Multiply(CTFImage);
                    //CTFImage.WriteMRC("ctf.mrc");
                    CTFImage.Dispose();
                }

                // Apply dose weighting.
                /*{
                    float3 NikoConst = new float3(0.245f, -1.665f, 2.81f);

                    // Niko's formula expects e-/A2/frame, we've got e-/px/frame -- convert!
                    float FrameDose = (float)MainWindow.Options.CorrectDosePerFrame * (z + 0.5f) / (PixelSize * PixelSize);

                    Image DoseImage = new Image(IntPtr.Zero, DimsRegion, true);
                    GPU.DoseWeighting(CTFFreq.GetDevice(Intent.Read),
                                      DoseImage.GetDevice(Intent.Write),
                                      (uint)DoseImage.ElementsSliceComplex,
                                      new[] { FrameDose },
                                      NikoConst,
                                      1);
                    PS.MultiplySlices(DoseImage);
                    //DoseImage.WriteMRC("dose.mrc");
                    DoseImage.Dispose();
                }*/

                // Copy custom CTF into the CTF stack
                GPU.CopyDeviceToDevice(PS.GetDevice(Intent.Read),
                                       new IntPtr((long)CTFStack.GetDevice(Intent.Write) + CTFStack.ElementsSliceReal * NParticles * z * sizeof (float)),
                                       CTFStack.ElementsSliceReal * NParticles);

                Image PSAbs = new Image(PS.GetDevice(Intent.Read), new int3(DimsRegion.X, DimsRegion.Y, NParticles), true);
                PSAbs.Abs();

                FrameParticlesFT.Multiply(PSAbs);
                AverageFT.Add(FrameParticlesFT);

                Weights.Add(PSAbs);

                PS.Multiply(PSAbs);
                AveragePS.Add(PS);

                PS.Dispose();
                FrameParticlesFT.Dispose();
                PSAbs.Dispose();

                // Write paths to custom CTFs into the .star
                for (int i = 0; i < NParticles; i++)
                {
                    string ParticleCTFPath = (CTFStackIndex + 1).ToString("D6") + "@particlectf/" + RootName + ".mrcs";
                    table.SetRowValue(RowIndices[NParticles * z + i], "rlnCtfImage", ParticleCTFPath);
                    CTFStackIndex++;
                }
            }
            FrameParticles.Dispose();
            CTFCoords.Dispose();
            CTFFreq.Dispose();

            AverageFT.Divide(Weights);
            AveragePS.Divide(Weights);
            Weights.Dispose();

            Image AverageParticles = AverageFT.AsIFFT();
            AverageFT.Dispose();

            GPU.NormParticles(AverageParticles.GetDevice(Intent.Read), AverageParticles.GetDevice(Intent.Write), DimsRegion, (uint)(90f / PixelSize), true, (uint)NParticles);

            HeaderMRC ParticlesHeader = new HeaderMRC
            {
                Pixelsize = new float3(PixelSize, PixelSize, PixelSize)
            };

            AverageParticles.WriteMRC(ParticlesPath, ParticlesHeader);
            AverageParticles.Dispose();

            //OriginalStack.Dispose();

            CTFStack.WriteMRC(DirectoryName + "particlectf/" + RootName + ".mrcs");
            CTFStack.Dispose();

            /*for (int i = 0; i < NParticles; i++)
            {
                string ParticlePath = (i + 1).ToString("D6") + "@particles/" + RootName + "_particles.mrcs";
                table.SetRowValue(RowIndices[i], "rlnImageName", ParticlePath);
            }*/

            AveragePS.Dispose();

            //table.RemoveRows(RowIndices.Skip(NParticles).ToArray());
        }

        public void PerformComparison(MapHeader originalHeader, Star stardata, Image refft, Image maskft, decimal scaleFactor)
        {
            int NFrames = originalHeader.Dimensions.Z;
            int2 DimsImage = new int2(originalHeader.Dimensions);

            float3[] PositionsGrid;
            float3[] PositionsShift;
            float3[] ParticleAngles;
            List<int> RowIndices = new List<int>();
            {
                string[] ColumnNames = stardata.GetColumn("rlnMicrographName");
                for (int i = 0; i < ColumnNames.Length; i++)
                    if (ColumnNames[i].Contains(RootName))
                        RowIndices.Add(i);

                string[] ColumnOriginX = stardata.GetColumn("rlnCoordinateX");
                string[] ColumnOriginY = stardata.GetColumn("rlnCoordinateY");
                string[] ColumnShiftX = stardata.GetColumn("rlnOriginX");
                string[] ColumnShiftY = stardata.GetColumn("rlnOriginY");
                string[] ColumnAngleRot = stardata.GetColumn("rlnAngleRot");
                string[] ColumnAngleTilt = stardata.GetColumn("rlnAngleTilt");
                string[] ColumnAnglePsi = stardata.GetColumn("rlnAnglePsi");

                PositionsGrid = new float3[RowIndices.Count];
                PositionsShift = new float3[RowIndices.Count];
                ParticleAngles = new float3[RowIndices.Count];

                {
                    int i = 0;
                    foreach (var nameIndex in RowIndices)
                    {
                        float OriginX = float.Parse(ColumnOriginX[nameIndex]);
                        float OriginY = float.Parse(ColumnOriginY[nameIndex]);
                        float ShiftX = float.Parse(ColumnShiftX[nameIndex]);
                        float ShiftY = float.Parse(ColumnShiftY[nameIndex]);
                        
                        PositionsGrid[i] = new float3((OriginX - ShiftX) / DimsImage.X, (OriginY - ShiftY) / DimsImage.Y, 0);
                        PositionsShift[i] = new float3(ShiftX, ShiftY, 0f);
                        ParticleAngles[i] = new float3(-float.Parse(ColumnAngleRot[nameIndex]) * Helper.ToRad,
                                                       -float.Parse(ColumnAngleTilt[nameIndex]) * Helper.ToRad,
                                                       -float.Parse(ColumnAnglePsi[nameIndex]) * Helper.ToRad);

                        i++;
                    }
                }
            }
            int NPositions = PositionsGrid.Length;
            if (NPositions == 0)
                return;

            Image Particles = StageDataLoad.LoadMap(ParticlesPath, new int2(1, 1), 0, typeof (float));
            int2 DimsRegion = new int2(Particles.Dims.X, Particles.Dims.X);

            Particles.ShiftSlices(PositionsShift);

            int MinFreqInclusive = (int)(MainWindow.Options.MovementRangeMin * DimsRegion.X / 2);
            int MaxFreqExclusive = (int)(MainWindow.Options.MovementRangeMax * DimsRegion.X / 2);
            int NFreq = MaxFreqExclusive - MinFreqInclusive;

            Image ParticleMasksFT = maskft.AsProjections(ParticleAngles, DimsRegion, MainWindow.Options.ProjectionOversample);
            Image ParticleMasks = ParticleMasksFT.AsIFFT();
            ParticleMasksFT.Dispose();
            ParticleMasks.RemapFromFT();

            Parallel.ForEach(ParticleMasks.GetHost(Intent.ReadWrite), slice =>
            {
                for (int i = 0; i < slice.Length; i++)
                    slice[i] = (Math.Max(2f, Math.Min(25f, slice[i])) - 2) / 23f;
            });

            Image ProjectionsFT = refft.AsProjections(ParticleAngles, DimsRegion, MainWindow.Options.ProjectionOversample);
            Image Projections = ProjectionsFT.AsIFFT();
            ProjectionsFT.Dispose();

            // Addresses for CTF simulation
            Image CTFCoordsCart = new Image(new int3(DimsRegion), true, true);
            {
                float2[] CoordsData = new float2[CTFCoordsCart.ElementsSliceComplex];

                Helper.ForEachElementFT(DimsRegion, (x, y, xx, yy, r, a) => CoordsData[y * (DimsRegion.X / 2 + 1) + x] = new float2(r / DimsRegion.X, a));
                CTFCoordsCart.UpdateHostWithComplex(new[] { CoordsData });
                CTFCoordsCart.RemapToFT();
            }
            float[] ValuesDefocus = GridCTF.GetInterpolatedNative(PositionsGrid);
            CTFStruct[] PositionsCTF = ValuesDefocus.Select(v =>
            {
                CTF Altered = CTF.GetCopy();
                Altered.Defocus = (decimal)v;
                //Altered.Bfactor = -MainWindow.Options.MovementBfactor;
                return Altered.ToStruct();
            }).ToArray();

            Image Scores = new Image(IntPtr.Zero, new int3(NPositions, 1, 1));

            GPU.CompareParticles(Particles.GetDevice(Intent.Read),
                                 ParticleMasks.GetDevice(Intent.Read),
                                 Projections.GetDevice(Intent.Read),
                                 DimsRegion,
                                 CTFCoordsCart.GetDevice(Intent.Read),
                                 PositionsCTF,
                                 MinFreqInclusive,
                                 MaxFreqExclusive,
                                 Scores.GetDevice(Intent.Write),
                                 (uint)NPositions);

            float[] ScoresData = Scores.GetHost(Intent.Read)[0];
            for (int p = 0; p < NPositions; p++)
            {
                stardata.SetRowValue(RowIndices[p], "rlnCtfFigureOfMerit", ScoresData[p].ToString(CultureInfo.InvariantCulture));
            }

            Scores.Dispose();
            Projections.Dispose();
            ParticleMasks.Dispose();
            CTFCoordsCart.Dispose();
            Particles.Dispose();
        }
    }

    public enum ProcessingStatus
    {
        Unprocessed,
        Outdated,
        Processed,
        Skip
    }
}
