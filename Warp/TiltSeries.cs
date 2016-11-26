using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;
using Accord.Math.Optimization;
using Warp.Headers;
using Warp.Stages;
using Warp.Tools;

namespace Warp
{
    public class TiltSeries : Movie
    {
        public string CorrelationDir => DirectoryName + "correlation\\";

        public string ReconstructionDir => DirectoryName + "reconstruction\\";

        public string WeightOptimizationDir => DirectoryName + "weightoptimization\\";

        public float GlobalWeight = 1;
        public float GlobalBfactor = 0;

        private CubicGrid _GridAngleWeights = new CubicGrid(new int3(1, 1, 1), 1, 1, Dimension.X);
        public CubicGrid GridAngleWeights
        {
            get { return _GridAngleWeights; }
            set { if (value != _GridAngleWeights) { _GridAngleWeights = value; OnPropertyChanged(); } }
        }

        private CubicGrid _GridDoseBfacs = new CubicGrid(new int3(1, 1, 1));
        public CubicGrid GridDoseBfacs
        {
            get { return _GridDoseBfacs; }
            set { if (value != _GridDoseBfacs) { _GridDoseBfacs = value; OnPropertyChanged(); } }
        }

        private ObservableCollection<float2[]> _TiltPS1D = new ObservableCollection<float2[]>();
        public ObservableCollection<float2[]> TiltPS1D
        {
            get { return _TiltPS1D; }
            set { if (value != _TiltPS1D) { _TiltPS1D = value; OnPropertyChanged(); } }
        }

        private ObservableCollection<Cubic1D> _TiltSimulatedBackground = new ObservableCollection<Cubic1D>();
        public ObservableCollection<Cubic1D> TiltSimulatedBackground
        {
            get { return _TiltSimulatedBackground; }
            set { if (value != _TiltSimulatedBackground) { _TiltSimulatedBackground = value; OnPropertyChanged(); } }
        }

        private ObservableCollection<Cubic1D> _TiltSimulatedScale = new ObservableCollection<Cubic1D>();
        public ObservableCollection<Cubic1D> TiltSimulatedScale
        {
            get { return _TiltSimulatedScale; }
            set { if (value != _TiltSimulatedScale) { _TiltSimulatedScale = value; OnPropertyChanged(); } }
        }

        private CubicGrid _GridCTFDefocusDelta = new CubicGrid(new int3(1, 1, 1));
        public CubicGrid GridCTFDefocusDelta
        {
            get { return _GridCTFDefocusDelta; }
            set { if (value != _GridCTFDefocusDelta) { _GridCTFDefocusDelta = value; OnPropertyChanged(); } }
        }

        private CubicGrid _GridCTFDefocusAngle = new CubicGrid(new int3(1, 1, 1));
        public CubicGrid GridCTFDefocusAngle
        {
            get { return _GridCTFDefocusAngle; }
            set { if (value != _GridCTFDefocusAngle) { _GridCTFDefocusAngle = value; OnPropertyChanged(); } }
        }

        private CubicGrid _GridLocalZ = new CubicGrid(new int3(1, 1, 1));
        public CubicGrid GridLocalZ
        {
            get { return _GridLocalZ; }
            set { if (value != _GridLocalZ) { _GridLocalZ = value; OnPropertyChanged(); } }
        }

        private CubicGrid _GridAngleX = new CubicGrid(new int3(1, 1, 1));
        public CubicGrid GridAngleX
        {
            get { return _GridAngleX; }
            set { if (value != _GridAngleX) { _GridAngleX = value; OnPropertyChanged(); } }
        }

        private CubicGrid _GridAngleY = new CubicGrid(new int3(1, 1, 1));
        public CubicGrid GridAngleY
        {
            get { return _GridAngleY; }
            set { if (value != _GridAngleY) { _GridAngleY = value; OnPropertyChanged(); } }
        }

        private CubicGrid _GridAngleZ = new CubicGrid(new int3(1, 1, 1));
        public CubicGrid GridAngleZ
        {
            get { return _GridAngleZ; }
            set { if (value != _GridAngleZ) { _GridAngleZ = value; OnPropertyChanged(); } }
        }

        public int3 VolumeDimensions;

        public float[] Angles = { 0 };
        public float[] Dose = { 0 };

        private bool _AreAnglesInverted = false;
        public bool AreAnglesInverted
        {
            get { return _AreAnglesInverted; }
            set { if (value != _AreAnglesInverted) { _AreAnglesInverted = value; OnPropertyChanged(); } }
        }

        public float[] AnglesCorrect => Angles.Select(v => AreAnglesInverted ? v : v).ToArray();

        public int[] IndicesSortedAngle
        {
            get
            {
                if (Angles == null)
                    return null;

                List<int> Sorted = new List<int>(Angles.Length);
                for (int i = 0; i < Angles.Length; i++)
                    Sorted.Add(i);

                Sorted.Sort((a, b) => Angles[a].CompareTo(Angles[b]));

                return Sorted.ToArray();
            }
        }

        public int[] IndicesSortedAbsoluteAngle
        {
            get
            {
                if (Angles == null)
                    return null;

                List<int> Sorted = new List<int>(Angles.Length);
                for (int i = 0; i < Angles.Length; i++)
                    Sorted.Add(i);

                Sorted.Sort((a, b) => Math.Abs(Angles[a]).CompareTo(Math.Abs(Angles[b])));

                return Sorted.ToArray();
            }
        }

        private int[] _IndicesSortedDose;
        public int[] IndicesSortedDose
        {
            get
            {
                if (Dose == null)
                    return null;

                if (_IndicesSortedDose == null)
                {
                    List<int> Sorted = new List<int>(Dose.Length);
                    for (int i = 0; i < Dose.Length; i++)
                        Sorted.Add(i);

                    Sorted.Sort((a, b) => Dose[a].CompareTo(Dose[b]));

                    _IndicesSortedDose = Sorted.ToArray();
                }

                return _IndicesSortedDose;
            }
        }

        public int NUniqueTilts
        {
            get
            {
                HashSet<float> UniqueAngles = new HashSet<float>();
                foreach (var angle in Angles)
                    if (!UniqueAngles.Contains(angle))
                        UniqueAngles.Add(angle);

                return UniqueAngles.Count;
            }
        }

        public int NTilts => Angles.Length;

        public float MinTilt => MathHelper.Min(Angles);
        public float MaxTilt => MathHelper.Max(Angles);

        public TiltSeries(string path) : base(path)
        {
            if (Angles.Length < Dimensions.Z)   // In case angles and dose haven't been read and stored in .xml yet.
            {
                if (File.Exists(DirectoryName + RootName + ".star"))
                {
                    Star Table = new Star(DirectoryName + RootName + ".star");

                    if (!Table.HasColumn("wrpDose") || !Table.HasColumn("wrpAngleTilt"))
                        throw new Exception("STAR file has no wrpDose or wrpTilt column.");

                    List<float> TempAngles = new List<float>();
                    List<float> TempDose = new List<float>();

                    for (int i = 0; i < Table.RowCount; i++)
                    {
                        TempAngles.Add(float.Parse(Table.GetRowValue(i, "wrpAngleTilt")));
                        TempDose.Add(float.Parse(Table.GetRowValue(i, "wrpDose")));
                    }

                    if (TempAngles.Count == 0)
                        throw new Exception("Metadata must contain 2 values per tilt: angle, dose.");
                    if (TempAngles.Count != Dimensions.Z)
                        throw new Exception("Metadata must contain one line for each image in tilt series.");

                    Angles = TempAngles.ToArray();
                    Dose = TempDose.ToArray();
                }
                else
                {
                    HeaderMRC Header = new HeaderMRC(new BinaryReader(File.OpenRead(Path)));
                    if (Header.ImodTilt != null)
                    {
                        Angles = Header.ImodTilt;
                        Dose = new float[Angles.Length];
                    }
                    else
                        throw new Exception("A .meta file with angles and accumulated dose, or at least tilt angle data in the .ali's header are needed to work with tilt series.");
                }
            }
        }

        public override void ProcessCTF(MapHeader originalHeader, Image originalStack, bool doastigmatism, decimal scaleFactor)
        {
            if (!Directory.Exists(PowerSpectrumDir))
                Directory.CreateDirectory(PowerSpectrumDir);

            AreAnglesInverted = false;
            float LastFittedAngle = 9999f;
            float AverageDose = Dose[IndicesSortedDose.Last()] / NTilts;
            List<int> ProcessedIndices = new List<int>();

            CTF[] FitCTF = new CTF[NTilts];
            CubicGrid[] FitGrids = new CubicGrid[NTilts];
            float2[][] FitPS1D = new float2[NTilts][];
            Cubic1D[] FitBackground = new Cubic1D[NTilts];
            Cubic1D[] FitScale = new Cubic1D[NTilts];
            Image[] FitPS2D = new Image[NTilts];

            float[][] StackData = originalStack.GetHost(Intent.Read);

            #region Get astigmatism from lower tilts

            List<float> AstigmatismDeltas = new List<float>();
            List<float> AstigmatismAngles = new List<float>();

            for (int i = 0; i < Math.Min(NTilts, 6); i++)
            {
                int AngleID = IndicesSortedAbsoluteAngle[i];
                Image UncroppedAngleImage = new Image(StackData[AngleID], originalStack.Dims.Slice());
                Image AngleImage = UncroppedAngleImage.AsPadded(new int2(3500, 3500));
                UncroppedAngleImage.Dispose();

                int BestPrevious = -1;
                if (Math.Abs(LastFittedAngle - Angles[AngleID]) <= 5.1f)
                    BestPrevious = IndicesSortedAbsoluteAngle[i - 1];
                else if (ProcessedIndices.Count > 0)
                {
                    List<int> SortedProcessed = new List<int>(ProcessedIndices);
                    SortedProcessed.Sort((a, b) => Math.Abs(Angles[AngleID] - Angles[a]).CompareTo(Math.Abs(Angles[AngleID] - Angles[b])));
                    if (Math.Abs(Dose[SortedProcessed.First()] - Dose[AngleID]) < AverageDose * 5f)
                        BestPrevious = SortedProcessed.First();
                }

                CTF ThisCTF;
                CubicGrid ThisGrid;
                float2[] ThisPS1D;
                Cubic1D ThisBackground, ThisScale;
                Image ThisPS2D;

                CTF PrevCTF = BestPrevious >= 0 ? FitCTF[BestPrevious] : null;
                CubicGrid PrevGrid = BestPrevious >= 0 ? FitGrids[BestPrevious] : null;
                Cubic1D PrevBackground = BestPrevious >= 0 ? FitBackground[BestPrevious] : null;
                Cubic1D PrevScale = BestPrevious >= 0 ? FitScale[BestPrevious] : null;

                ProcessCTFOneAngle(AngleImage,
                                   Angles[AngleID],
                                   BestPrevious < 0,
                                   false,
                                   new float2(0, 0), 
                                   PrevCTF,
                                   PrevGrid,
                                   PrevBackground,
                                   PrevScale,
                                   out ThisCTF,
                                   out ThisGrid,
                                   out ThisPS1D,
                                   out ThisBackground,
                                   out ThisScale,
                                   out ThisPS2D);
                AngleImage.Dispose();

                FitCTF[AngleID] = ThisCTF;
                FitGrids[AngleID] = ThisGrid;
                FitPS1D[AngleID] = ThisPS1D;
                FitBackground[AngleID] = ThisBackground;
                FitScale[AngleID] = ThisScale;
                FitPS2D[AngleID] = ThisPS2D;

                LastFittedAngle = Angles[AngleID];
                ProcessedIndices.Add(AngleID);

                AstigmatismDeltas.Add((float)ThisCTF.DefocusDelta);
                AstigmatismAngles.Add((float)ThisCTF.DefocusAngle);
            }

            ProcessedIndices.Clear();
            LastFittedAngle = 9999;
            int[] GoodIndices = MathHelper.WithinNStdFromMedianIndices(AstigmatismDeltas.ToArray(), 1f);
            float MeanAstigmatismDelta = MathHelper.Mean(GoodIndices.Select(i => AstigmatismDeltas[i]));
            float2 MeanAstigmatismVector = MathHelper.Mean(GoodIndices.Select(i => new float2((float)Math.Cos(AstigmatismAngles[i] * Helper.ToRad),
                                                                                              (float)Math.Sin(AstigmatismAngles[i] * Helper.ToRad))));
            float MeanAstigmatismAngle = (float)Math.Atan2(MeanAstigmatismVector.Y, MeanAstigmatismVector.X) * Helper.ToDeg;

            #endregion

            #region Fit every tilt

            for (int i = 0; i < NTilts; i++)
            {
                int AngleID = IndicesSortedDose[i];
                Image UncroppedAngleImage = new Image(StackData[AngleID], originalStack.Dims.Slice());
                Image AngleImage = UncroppedAngleImage.AsPadded(new int2(3500, 3500));
                UncroppedAngleImage.Dispose();

                int BestPrevious = -1;
                if (Math.Abs(LastFittedAngle - Angles[AngleID]) <= 5.1f)
                    BestPrevious = IndicesSortedDose[i - 1];
                else if (ProcessedIndices.Count > 0)
                {
                    List<int> SortedProcessed = new List<int>(ProcessedIndices);
                    SortedProcessed.Sort((a, b) => Math.Abs(Angles[AngleID] - Angles[a]).CompareTo(Math.Abs(Angles[AngleID] - Angles[b])));
                    if (Math.Abs(Dose[SortedProcessed.First()] - Dose[AngleID]) < AverageDose * 5f)
                        BestPrevious = SortedProcessed.First();
                }

                CTF ThisCTF;
                CubicGrid ThisGrid;
                float2[] ThisPS1D;
                Cubic1D ThisBackground, ThisScale;
                Image ThisPS2D;

                CTF PrevCTF = BestPrevious >= 0 ? FitCTF[BestPrevious] : null;
                CubicGrid PrevGrid = BestPrevious >= 0 ? FitGrids[BestPrevious] : null;
                Cubic1D PrevBackground = BestPrevious >= 0 ? FitBackground[BestPrevious] : null;
                Cubic1D PrevScale = BestPrevious >= 0 ? FitScale[BestPrevious] : null;

                ProcessCTFOneAngle(AngleImage,
                                   Angles[AngleID],
                                   BestPrevious < 0,
                                   true,
                                   new float2(MeanAstigmatismDelta, MeanAstigmatismAngle), 
                                   PrevCTF,
                                   PrevGrid,
                                   PrevBackground,
                                   PrevScale,
                                   out ThisCTF,
                                   out ThisGrid,
                                   out ThisPS1D,
                                   out ThisBackground,
                                   out ThisScale,
                                   out ThisPS2D);
                AngleImage.Dispose();

                FitCTF[AngleID] = ThisCTF;
                FitGrids[AngleID] = ThisGrid;
                FitPS1D[AngleID] = ThisPS1D;
                FitBackground[AngleID] = ThisBackground;
                FitScale[AngleID] = ThisScale;
                FitPS2D[AngleID] = ThisPS2D;

                LastFittedAngle = Angles[AngleID];
                ProcessedIndices.Add(AngleID);
            }

            #endregion

            CTF = FitCTF[IndicesSortedDose[0]];

            #region Determine if angles are inverted compared to actual defocus

            {
                float[] UnbiasedAngles = FitGrids.Select(g =>
                {
                    float X1 = (g.FlatValues[0] + g.FlatValues[2]) * 0.5f;
                    float X2 = (g.FlatValues[1] + g.FlatValues[3]) * 0.5f;
                    float Delta = (X2 - X1) * 10000;
                    float Distance = (float)MainWindow.Options.BinnedPixelSize * 3000;// originalHeader.Dimensions.X;
                    return (float)Math.Atan2(Delta, Distance) * Helper.ToDeg;
                }).ToArray();

                float Unbiased1 = 0, Unbiased2 = 0, Original1 = 0, Original2 = 0;
                for (int i = 0; i < NTilts; i++)
                {
                    int ii = IndicesSortedAngle[i];
                    if (i < NTilts / 2)
                    {
                        Unbiased1 += UnbiasedAngles[ii];
                        Original1 += Angles[ii];
                    }
                    else
                    {
                        Unbiased2 += UnbiasedAngles[ii];
                        Original2 += Angles[ii];
                    }
                }

                if ((Unbiased1 > Unbiased2) != (Original1 > Original2))
                    AreAnglesInverted = true;
            }

            #endregion

            // Create grids for fitted CTF params
            {
                float[] DefocusValues = new float[NTilts];
                float[] DeltaValues = new float[NTilts];
                float[] AngleValues = new float[NTilts];
                for (int i = 0; i < NTilts; i++)
                {
                    DefocusValues[i] = (float)FitCTF[i].Defocus;
                    DeltaValues[i] = (float)FitCTF[i].DefocusDelta;
                    AngleValues[i] = (float)FitCTF[i].DefocusAngle;
                }

                GridCTF = new CubicGrid(new int3(1, 1, NTilts), DefocusValues);
                GridCTFDefocusDelta = new CubicGrid(new int3(1, 1, NTilts), DeltaValues);
                GridCTFDefocusAngle = new CubicGrid(new int3(1, 1, NTilts), AngleValues);
            }

            // Put all 2D spectra into one stack and write it to disk for display purposes
            {
                Image AllPS2D = new Image(new int3(FitPS2D[0].Dims.X, FitPS2D[0].Dims.Y, NTilts));
                float[][] AllPS2DData = AllPS2D.GetHost(Intent.Write);
                for (int i = 0; i < NTilts; i++)
                {
                    AllPS2DData[i] = FitPS2D[i].GetHost(Intent.Read)[0];
                    FitPS2D[i].Dispose();
                }

                AllPS2D.WriteMRC(PowerSpectrumPath);
            }

            // Store 1D spectrum data
            TiltPS1D.Clear();
            TiltSimulatedBackground.Clear();
            TiltSimulatedScale.Clear();
            for (int i = 0; i < NTilts; i++)
            {
                TiltPS1D.Add(FitPS1D[i]);
                TiltSimulatedBackground.Add(new Cubic1D(FitBackground[i].Data.Select(v => new float2(v.X, 0)).ToArray()));
                TiltSimulatedScale.Add(FitScale[i]);
            }

            PS1D = FitPS1D[IndicesSortedDose[0]];
            SimulatedBackground = TiltSimulatedBackground[IndicesSortedDose[0]];
            SimulatedScale = FitScale[IndicesSortedDose[0]];

            OnPropertyChanged("PS1D");

            Simulated1D = GetSimulated1D();
            CTFQuality = GetCTFQuality();

            SaveMeta();
        }

        public void ProcessCTFOneAngle(Image angleImage,
                                       float angle,
                                       bool fromScratch,
                                       bool fixAstigmatism,
                                       float2 astigmatism,
                                       CTF previousCTF,
                                       CubicGrid previousGrid,
                                       Cubic1D previousBackground,
                                       Cubic1D previousScale,
                                       out CTF thisCTF,
                                       out CubicGrid thisGrid,
                                       out float2[] thisPS1D,
                                       out Cubic1D thisBackground,
                                       out Cubic1D thisScale,
                                       out Image thisPS2D)
        {
            CTF TempCTF = previousCTF != null ? previousCTF.GetCopy() : new CTF();
            float2[] TempPS1D = null;
            Cubic1D TempBackground = null, TempScale = null;
            CubicGrid TempGrid = null;

            #region Dimensions and grids

            int NFrames = angleImage.Dims.Z;
            int2 DimsImage = angleImage.DimsSlice;
            int2 DimsRegion = new int2(MainWindow.Options.CTFWindow, MainWindow.Options.CTFWindow);

            float OverlapFraction = 0.5f;
            int2 DimsPositionGrid;
            int3[] PositionGrid = Helper.GetEqualGridSpacing(DimsImage, new int2(DimsRegion.X, DimsRegion.Y), OverlapFraction, out DimsPositionGrid);
            int NPositions = (int)DimsPositionGrid.Elements();

            if (previousGrid == null)
                TempGrid = new CubicGrid(new int3(2, 2, 1));
            else
                TempGrid = new CubicGrid(new int3(2, 2, 1), previousGrid.FlatValues);

            bool CTFSpace = true;
            bool CTFTime = false;
            int3 CTFSpectraGrid = new int3(DimsPositionGrid.X, DimsPositionGrid.Y, 1);

            int MinFreqInclusive = (int)(MainWindow.Options.CTFRangeMin * DimsRegion.X / 2);
            int MaxFreqExclusive = (int)(MainWindow.Options.CTFRangeMax * DimsRegion.X / 2);
            int NFreq = MaxFreqExclusive - MinFreqInclusive;

            #endregion

            #region Allocate GPU memory

            Image CTFSpectra = new Image(IntPtr.Zero, new int3(DimsRegion.X, DimsRegion.X, (int)CTFSpectraGrid.Elements()), true);
            Image CTFMean = new Image(IntPtr.Zero, new int3(DimsRegion), true);
            Image CTFCoordsCart = new Image(new int3(DimsRegion), true, true);
            Image CTFCoordsPolarTrimmed = new Image(new int3(NFreq, DimsRegion.X, 1), false, true);

            #endregion

            // Extract movie regions, create individual spectra in Cartesian coordinates and their mean.

            #region Create spectra

            GPU.CreateSpectra(angleImage.GetDevice(Intent.Read),
                              DimsImage,
                              NFrames,
                              PositionGrid,
                              NPositions,
                              DimsRegion,
                              CTFSpectraGrid,
                              CTFSpectra.GetDevice(Intent.Write),
                              CTFMean.GetDevice(Intent.Write));
            angleImage.FreeDevice(); // Won't need it in this method anymore.

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
                TempPS1D = ForPS1D;

                CTFAverage1D.Dispose();
            }

            #endregion

            #region Background fitting methods

            Action UpdateBackgroundFit = () =>
            {
                float2[] ForPS1D = TempPS1D.Skip(Math.Max(5, MinFreqInclusive / 2)).ToArray();
                Cubic1D.FitCTF(ForPS1D,
                               v => v.Select(x => TempCTF.Get1D(x / (float)TempCTF.PixelSize, true)).ToArray(),
                               TempCTF.GetZeros(),
                               TempCTF.GetPeaks(),
                               out TempBackground,
                               out TempScale);
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
                                            MeanCorrectedData[i] = MeanData[i] - TempBackground.Interp(r / DimsRegion.X);
                                        });

                Image CTFAverage1D = new Image(IntPtr.Zero, new int3(DimsRegion.X / 2, 1, 1));

                GPU.CTFMakeAverage(CTFMeanCorrected.GetDevice(Intent.Read),
                                   CTFCoordsCart.GetDevice(Intent.Read),
                                   (uint)CTFMeanCorrected.DimsEffective.ElementsSlice(),
                                   (uint)DimsRegion.X,
                                   new[] { TempCTF.ToStruct() },
                                   TempCTF.ToStruct(),
                                   0,
                                   (uint)DimsRegion.X / 2,
                                   null,
                                   1,
                                   CTFAverage1D.GetDevice(Intent.Write));

                //CTFAverage1D.WriteMRC("CTFAverage1D.mrc");

                float[] RotationalAverageData = CTFAverage1D.GetHost(Intent.Read)[0];
                float2[] ForPS1D = new float2[TempPS1D.Length];
                if (keepbackground)
                    for (int i = 0; i < ForPS1D.Length; i++)
                        ForPS1D[i] = new float2((float)i / DimsRegion.X, RotationalAverageData[i] + TempBackground.Interp((float)i / DimsRegion.X));
                else
                    for (int i = 0; i < ForPS1D.Length; i++)
                        ForPS1D[i] = new float2((float)i / DimsRegion.X, RotationalAverageData[i]);
                MathHelper.UnNaN(ForPS1D);

                TempPS1D = ForPS1D;

                CTFMeanCorrected.Dispose();
                CTFAverage1D.Dispose();
            };

            #endregion

            // Fit background to currently best average (not corrected for astigmatism yet).
            {
                float2[] ForPS1D = TempPS1D.Skip(MinFreqInclusive).Take(Math.Max(2, NFreq / 2)).ToArray();


                float[] CurrentBackground;
                //if (previousBackground == null)
                {
                    int NumNodes = Math.Max(3, (int)((MainWindow.Options.CTFRangeMax - MainWindow.Options.CTFRangeMin) * 5M));
                    TempBackground = Cubic1D.Fit(ForPS1D, NumNodes); // This won't fit falloff and scale, because approx function is 0

                    CurrentBackground = TempBackground.Interp(TempPS1D.Select(p => p.X).ToArray()).Skip(MinFreqInclusive).Take(NFreq / 2).ToArray();
                }
                /*else
                {
                    CurrentBackground = previousBackground.Interp(TempPS1D.Select(p => p.X).ToArray()).Skip(MinFreqInclusive).Take(NFreq / 2).ToArray();
                    TempBackground = new Cubic1D(previousBackground.Data);
                }*/

                float[] Subtracted1D = new float[ForPS1D.Length];
                for (int i = 0; i < ForPS1D.Length; i++)
                    Subtracted1D[i] = ForPS1D[i].Y - CurrentBackground[i];
                MathHelper.NormalizeInPlace(Subtracted1D);

                float ZMin = (float)MainWindow.Options.CTFZMin;
                float ZMax = (float)MainWindow.Options.CTFZMax;
                float PhaseMin = 0f;
                float PhaseMax = MainWindow.Options.CTFDoPhase ? 1f : 0f;

                if (previousCTF != null)
                {
                    ZMin = (float)previousCTF.Defocus - 0.5f;
                    ZMax = (float)previousCTF.Defocus + 0.5f;
                    if (PhaseMax > 0)
                    {
                        PhaseMin = (float)previousCTF.PhaseShift - 0.3f;
                        PhaseMax = (float)previousCTF.PhaseShift + 0.3f;
                    }
                }

                float ZStep = (ZMax - ZMin) / 100f;

                float BestZ = 0, BestPhase = 0, BestScore = -999;
                for (float z = ZMin; z <= ZMax + 1e-5f; z += ZStep)
                {
                    for (float p = PhaseMin; p <= PhaseMax; p += 0.01f)
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
                        float[] SimulatedCTF = CurrentParams.Get1D(TempPS1D.Length, true).Skip(MinFreqInclusive).Take(Math.Max(2, NFreq / 2)).ToArray();
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

                TempCTF = new CTF
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

            if (fromScratch)
            {
                Image CTFMeanPolarTrimmed = CTFMean.AsPolar((uint)MinFreqInclusive, (uint)(MinFreqInclusive + NFreq / 1));

                // Subtract current background.
                Image CurrentBackground = new Image(TempBackground.Interp(TempPS1D.Select(p => p.X).ToArray()).Skip(MinFreqInclusive).Take(NFreq / 1).ToArray());
                CTFMeanPolarTrimmed.SubtractFromLines(CurrentBackground);
                CurrentBackground.Dispose();

                // Normalize for CC (not strictly needed, but it's converted for fp16 later, so let's be on the safe side of the fp16 range.
                GPU.Normalize(CTFMeanPolarTrimmed.GetDevice(Intent.Read), CTFMeanPolarTrimmed.GetDevice(Intent.Write), (uint)CTFMeanPolarTrimmed.ElementsReal, 1);
                //CTFMeanPolarTrimmed.WriteMRC("ctfmeanpolartrimmed.mrc");

                CTF StartParams = new CTF
                {
                    PixelSize = (MainWindow.Options.CTFPixelMin + MainWindow.Options.CTFPixelMax) * 0.5M,
                    PixelSizeDelta = Math.Abs(MainWindow.Options.CTFPixelMax - MainWindow.Options.CTFPixelMin),
                    PixelSizeAngle = MainWindow.Options.CTFPixelAngle,

                    Defocus = TempCTF.Defocus, // (MainWindow.Options.CTFZMin + MainWindow.Options.CTFZMax) * 0.5M,
                    DefocusDelta = 0,
                    DefocusAngle = 0,

                    PhaseShift = TempCTF.PhaseShift,

                    Cs = MainWindow.Options.CTFCs,
                    Voltage = MainWindow.Options.CTFVoltage,
                    Amplitude = MainWindow.Options.CTFAmplitude
                };

                CTFFitStruct FitParams = new CTFFitStruct
                {
                    Defocus = new float3(-0.4e-6f,
                                         0.4e-6f,
                                         0.025e-6f),

                    Defocusdelta = new float3(0, 0.8e-6f, 0.02e-6f),
                    Astigmatismangle = new float3(0, 2 * (float)Math.PI, 1 * (float)Math.PI / 18),
                    Phaseshift = MainWindow.Options.CTFDoPhase ? new float3(-0.2f * (float)Math.PI, 0.2f * (float)Math.PI, 0.025f * (float)Math.PI) : new float3(0, 0, 0)
                };

                CTFStruct ResultStruct = GPU.CTFFitMean(CTFMeanPolarTrimmed.GetDevice(Intent.Read),
                                                        CTFCoordsPolarTrimmed.GetDevice(Intent.Read),
                                                        CTFMeanPolarTrimmed.DimsSlice,
                                                        StartParams.ToStruct(),
                                                        FitParams,
                                                        true);
                TempCTF.FromStruct(ResultStruct);
                TempCTF.Defocus = Math.Max(TempCTF.Defocus, MainWindow.Options.CTFZMin);

                CTFMeanPolarTrimmed.Dispose();

                UpdateRotationalAverage(true); // This doesn't have a nice background yet.
                UpdateBackgroundFit(); // Now get a reasonably nice background.

                UpdateRotationalAverage(true); // This time, with the nice background.
                UpdateBackgroundFit(); // Make the background even nicer!
            }
            else if (previousCTF != null)
            {
                TempCTF.DefocusDelta = previousCTF.DefocusDelta;
                TempCTF.DefocusAngle = previousCTF.DefocusAngle;
            }

            if (fixAstigmatism)
            {
                TempCTF.DefocusDelta = (decimal)astigmatism.X;
                TempCTF.DefocusAngle = (decimal)astigmatism.Y;
            }

            #endregion

            if (previousGrid == null)
                TempGrid = new CubicGrid(TempGrid.Dimensions, (float)TempCTF.Defocus, (float)TempCTF.Defocus, Dimension.X);


            // Do BFGS optimization of defocus, astigmatism and phase shift,
            // using 2D simulation for comparison

            #region BFGS

            bool[] CTFSpectraConsider = new bool[CTFSpectraGrid.Elements()];
            for (int i = 0; i < CTFSpectraConsider.Length; i++)
                CTFSpectraConsider[i] = true;
            int NCTFSpectraConsider = CTFSpectraConsider.Length;

            {
                Image CTFSpectraPolarTrimmed = CTFSpectra.AsPolar((uint)MinFreqInclusive, (uint)(MinFreqInclusive + NFreq));
                CTFSpectra.FreeDevice(); // This will only be needed again for the final PS1D.

                #region Create background and scale

                float[] CurrentScale = TempScale.Interp(TempPS1D.Select(p => p.X).ToArray());

                Image CTFSpectraScale = new Image(new int3(NFreq, DimsRegion.X, 1));
                float[] CTFSpectraScaleData = CTFSpectraScale.GetHost(Intent.Write)[0];

                // Trim polar to relevant frequencies, and populate coordinates.
                Parallel.For(0, DimsRegion.X, y =>
                {
                    for (int x = 0; x < NFreq; x++)
                        CTFSpectraScaleData[y * NFreq + x] = CurrentScale[x + MinFreqInclusive];
                });
                //CTFSpectraScale.WriteMRC("ctfspectrascale.mrc");

                // Background is just 1 line since we're in polar.
                Image CurrentBackground = new Image(TempBackground.Interp(TempPS1D.Select(p => p.X).ToArray()).Skip(MinFreqInclusive).Take(NFreq).ToArray());

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
                float[][] WiggleWeights = TempGrid.GetWiggleWeights(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 0));

                // Helper method for getting CTFStructs for the entire spectra grid.
                Func<double[], CTF, float[], CTFStruct[]> EvalGetCTF = (input, ctf, defocusValues) =>
                {
                    decimal AlteredPhase = MainWindow.Options.CTFDoPhase ? (decimal)input[input.Length - 3] : 0;
                    decimal AlteredDelta = (decimal)input[input.Length - 2];
                    decimal AlteredAngle = (decimal)(input[input.Length - 1] * 20 / (Math.PI / 180));

                    CTF Local = ctf.GetCopy();
                    Local.PhaseShift = AlteredPhase;
                    Local.DefocusDelta = AlteredDelta;
                    Local.DefocusAngle = AlteredAngle;

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
                    CubicGrid Altered = new CubicGrid(TempGrid.Dimensions, input.Take((int)TempGrid.Dimensions.Elements()).Select(v => (float)v).ToArray());
                    float[] DefocusValues = Altered.GetInterpolatedNative(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 0));

                    CTFStruct[] LocalParams = EvalGetCTF(input, TempCTF, DefocusValues);

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
                    int StartComponent = input.Length - 3;
                    //int StartComponent = 0;
                    for (int i = StartComponent; i < input.Length; i++)
                    {
                        if (fixAstigmatism && i > StartComponent)
                            continue;

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
                        {
                            CubicGrid AlteredPlus = new CubicGrid(TempGrid.Dimensions, input.Take((int)TempGrid.Dimensions.Elements()).Select(v => (float)v + Step).ToArray());
                            float[] DefocusValues = AlteredPlus.GetInterpolatedNative(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 0));

                            CTFStruct[] LocalParams = EvalGetCTF(input, TempCTF, DefocusValues);

                            GPU.CTFCompareToSim(CTFSpectraPolarTrimmedHalf.GetDevice(Intent.Read),
                                                CTFCoordsPolarTrimmedHalf.GetDevice(Intent.Read),
                                                CTFSpectraScaleHalf.GetDevice(Intent.Read),
                                                (uint)CTFSpectraPolarTrimmedHalf.ElementsSliceReal,
                                                LocalParams,
                                                ResultPlus,
                                                (uint)LocalParams.Length);
                        }
                        {
                            CubicGrid AlteredMinus = new CubicGrid(TempGrid.Dimensions, input.Take((int)TempGrid.Dimensions.Elements()).Select(v => (float)v - Step).ToArray());
                            float[] DefocusValues = AlteredMinus.GetInterpolatedNative(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 0));

                            CTFStruct[] LocalParams = EvalGetCTF(input, TempCTF, DefocusValues);

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
                        Parallel.For(0, TempGrid.Dimensions.Elements(), i => Result[i] = MathHelper.ReduceWeighted(LocalGradients, WiggleWeights[i]) / LocalGradients.Length / (2f * Step) * 1000f);
                    }

                    foreach (var i in Result)
                        if (double.IsNaN(i) || double.IsInfinity(i))
                            throw new Exception("Bad score.");

                    return Result;
                };

                #endregion

                #region Minimize first time with potential outpiers

                double[] StartParams = new double[TempGrid.Dimensions.Elements() + 3];
                for (int i = 0; i < TempGrid.Dimensions.Elements(); i++)
                    StartParams[i] = TempGrid.FlatValues[i];
                StartParams[StartParams.Length - 3] = (double)TempCTF.PhaseShift;
                StartParams[StartParams.Length - 2] = (double)TempCTF.DefocusDelta;
                StartParams[StartParams.Length - 1] = (double)TempCTF.DefocusAngle / 20 * (Math.PI / 180);

                // Compute correlation for individual spectra, and throw away those that are >.75 sigma worse than mean.

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

                TempCTF.Defocus = (decimal)MathHelper.Mean(Optimizer.Solution.Take((int)TempGrid.Dimensions.Elements()).Select(v => (float)v));
                TempCTF.PhaseShift = (decimal)Optimizer.Solution[StartParams.Length - 3];
                TempCTF.DefocusDelta = (decimal)Optimizer.Solution[StartParams.Length - 2];
                TempCTF.DefocusAngle = (decimal)(Optimizer.Solution[StartParams.Length - 1] * 20 / (Math.PI / 180));

                if (TempCTF.DefocusDelta < 0)
                {
                    TempCTF.DefocusAngle += 90;
                    TempCTF.DefocusDelta *= -1;
                }
                TempCTF.DefocusAngle = ((int)TempCTF.DefocusAngle + 180 * 99) % 180;

                TempGrid = new CubicGrid(TempGrid.Dimensions, Optimizer.Solution.Take((int)TempGrid.Dimensions.Elements()).Select(v => (float)v).ToArray());

                #endregion

                // Dispose GPU resources manually because GC can't be bothered to do it in time.
                CTFSpectraPolarTrimmedHalf.Dispose();
                CTFCoordsPolarTrimmedHalf.Dispose();
                CTFSpectraScaleHalf.Dispose();

                #region Get nicer envelope fit

                {
                    {
                        Image CTFSpectraBackground = new Image(new int3(DimsRegion), true);
                        float[] CTFSpectraBackgroundData = CTFSpectraBackground.GetHost(Intent.Write)[0];

                        // Construct background in Cartesian coordinates.
                        Helper.ForEachElementFT(DimsRegion, (x, y, xx, yy, r, a) =>
                        {
                            CTFSpectraBackgroundData[y * CTFSpectraBackground.DimsEffective.X + x] = TempBackground.Interp(r / DimsRegion.X);
                        });

                        CTFSpectra.SubtractFromSlices(CTFSpectraBackground);

                        float[] DefocusValues = TempGrid.GetInterpolatedNative(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 0));
                        CTFStruct[] LocalParams = DefocusValues.Select(v =>
                        {
                            CTF Local = TempCTF.GetCopy();
                            Local.Defocus = (decimal)v + 0.0M;

                            return Local.ToStruct();
                        }).ToArray();

                        Image CTFAverage1D = new Image(IntPtr.Zero, new int3(DimsRegion.X / 2, 1, 1));

                        CTF CTFAug = TempCTF.GetCopy();
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
                        float2[] ForPS1D = new float2[TempPS1D.Length];
                        for (int i = 0; i < ForPS1D.Length; i++)
                            ForPS1D[i] = new float2((float)i / DimsRegion.X, (float)Math.Round(RotationalAverageData[i], 4) + TempBackground.Interp((float)i / DimsRegion.X));
                        MathHelper.UnNaN(ForPS1D);
                        TempPS1D = ForPS1D;

                        CTFSpectraBackground.Dispose();
                        CTFAverage1D.Dispose();
                        CTFSpectra.FreeDevice();
                    }

                    TempCTF.Defocus = Math.Max(TempCTF.Defocus, MainWindow.Options.CTFZMin);
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
                        Average2DData[y * DimsAverage.X + x] = OriginalAverageData[(y + DimsRegion.X / 2) * (DimsRegion.X / 2 + 1) + x] - TempBackground.Interp(r);
                    }

                    for (int x = 0; x < DimsRegion.X / 2; x++)
                    {
                        int xx = x * x;
                        float r = (float)Math.Sqrt(xx + yy) / DimsRegion.X;
                        Average2DData[y * DimsAverage.X + x + DimsRegion.X / 2] = OriginalAverageData[(DimsRegion.X / 2 - y) * (DimsRegion.X / 2 + 1) + (DimsRegion.X / 2 - 1 - x)] - TempBackground.Interp(r);
                    }
                }

                thisPS2D = new Image(Average2DData, DimsAverage);
            }

            #endregion

            for (int i = 0; i < TempPS1D.Length; i++)
                TempPS1D[i].Y -= TempBackground.Interp(TempPS1D[i].X);

            CTFSpectra.Dispose();
            CTFMean.Dispose();
            CTFCoordsCart.Dispose();
            CTFCoordsPolarTrimmed.Dispose();

            thisPS1D = TempPS1D;
            thisBackground = TempBackground;
            thisScale = TempScale;
            thisCTF = TempCTF;
            thisGrid = TempGrid;
        }

        public void ExportSubtomos(Star tableIn, Image tiltStack, int size, int3 volumeDimensions)
        {
            VolumeDimensions = volumeDimensions;

            #region Get rows from table

            List<int> RowIndices = new List<int>();
            string[] ColumnMicrographName = tableIn.GetColumn("rlnMicrographName");
            for (int i = 0; i < ColumnMicrographName.Length; i++)
                if (ColumnMicrographName[i].Contains(RootName + "."))
                    RowIndices.Add(i);

            //if (RowIndices.Count == 0)
            //    return;

            int NParticles = RowIndices.Count;

            #endregion

            #region Make sure all columns and directories are there

            if (!tableIn.HasColumn("rlnImageName"))
                tableIn.AddColumn("rlnImageName");
            if (!tableIn.HasColumn("rlnCtfImage"))
                tableIn.AddColumn("rlnCtfImage");

            if (!Directory.Exists(ParticlesDir))
                Directory.CreateDirectory(ParticlesDir);
            if (!Directory.Exists(ParticleCTFDir))
                Directory.CreateDirectory(ParticleCTFDir);

            #endregion

            #region Get subtomo positions from table

            float3[] Origins = new float3[NParticles];
            float3[] ParticleAngles = new float3[NParticles];
            {
                string[] ColumnPosX = tableIn.GetColumn("rlnCoordinateX");
                string[] ColumnPosY = tableIn.GetColumn("rlnCoordinateY");
                string[] ColumnPosZ = tableIn.GetColumn("rlnCoordinateZ");
                string[] ColumnOriginX = tableIn.GetColumn("rlnOriginX");
                string[] ColumnOriginY = tableIn.GetColumn("rlnOriginY");
                string[] ColumnOriginZ = tableIn.GetColumn("rlnOriginZ");
                string[] ColumnAngleRot = tableIn.GetColumn("rlnAngleRot");
                string[] ColumnAngleTilt = tableIn.GetColumn("rlnAngleTilt");
                string[] ColumnAnglePsi = tableIn.GetColumn("rlnAnglePsi");

                for (int i = 0; i < NParticles; i++)
                {
                    float3 Pos = new float3(float.Parse(ColumnPosX[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosY[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosZ[RowIndices[i]], CultureInfo.InvariantCulture));
                    float3 Shift = new float3(float.Parse(ColumnOriginX[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginY[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginZ[RowIndices[i]], CultureInfo.InvariantCulture));

                    //Origins[i] *= new float3(3838f / 959f, 3710f / 927f, 4f);
                    Origins[i] = Pos - Shift;

                    float3 Angle = new float3(0, 0, 0);
                    if (ColumnAngleRot != null && ColumnAngleTilt != null && ColumnAnglePsi != null)
                        Angle = new float3(float.Parse(ColumnAngleRot[RowIndices[i]], CultureInfo.InvariantCulture),
                                           float.Parse(ColumnAngleTilt[RowIndices[i]], CultureInfo.InvariantCulture),
                                           float.Parse(ColumnAnglePsi[RowIndices[i]], CultureInfo.InvariantCulture));
                    ParticleAngles[i] = Angle;

                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateX", Origins[i].X.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateY", Origins[i].Y.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateZ", Origins[i].Z.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginX", "0.0");
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginY", "0.0");
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginZ", "0.0");

                    //tableIn.SetRowValue(RowIndices[i], "rlnAngleRot", "0.0");
                    //tableIn.SetRowValue(RowIndices[i], "rlnAngleTilt", "0.0");
                    //tableIn.SetRowValue(RowIndices[i], "rlnAnglePsi", "0.0");
                }
            }

            #endregion

            tiltStack.FreeDevice();

            /*int SizeCropped = 50;//size / 2;

            int3 Grid = (VolumeDimensions + SizeCropped - 1) / SizeCropped;
            List<float3> GridCoords = new List<float3>();
            for (int z = 0; z < Grid.Z; z++)
                for (int y = 0; y < Grid.Y; y++)
                    for (int x = 0; x < Grid.X; x++)
                        GridCoords.Add(new float3(x * SizeCropped + SizeCropped / 2,
                                                  y * SizeCropped + SizeCropped / 2,
                                                  z * SizeCropped + SizeCropped / 2));

            Origins = GridCoords.ToArray();
            NParticles = Origins.Length;*/

            if (NParticles == 0)
                return;

            Image CTFCoords = GetCTFCoords(size, size);

            int PlanForw, PlanBack, PlanForwCTF;
            Projector.GetPlans(new int3(size, size, size), 2, out PlanForw, out PlanBack, out PlanForwCTF);
            
            //Parallel.For(0, NParticles, new ParallelOptions() { MaxDegreeOfParallelism = 1 }, p =>
            for (int p = 0; p < NParticles; p++)
            {
                lock (tableIn)
                {
                    tableIn.SetRowValue(RowIndices[p], "rlnImageName", "particles/" + RootName + "_" + p.ToString("D5") + ".mrc");
                    tableIn.SetRowValue(RowIndices[p], "rlnCtfImage", "particlectf/" + RootName + "_" + p.ToString("D5") + ".mrc");

                    //tableIn.Save("D:\\rubisco\\luisexported.star");
                }

                if (File.Exists(ParticlesDir + RootName + "_" + p.ToString("D5") + ".mrc"))
                    return;

                Image Subtomo, SubtomoCTF;
                GetSubtomo(tiltStack, Origins[p], ParticleAngles[p], CTFCoords, out Subtomo, out SubtomoCTF, PlanForw, PlanBack, PlanForwCTF);
                //Image SubtomoCropped = Subtomo.AsPadded(new int3(SizeCropped, SizeCropped, SizeCropped));

                Subtomo.WriteMRC(ParticlesDir + RootName + "_" + p.ToString("D5") + ".mrc");
                SubtomoCTF.WriteMRC(ParticleCTFDir + RootName + "_" + p.ToString("D5") + ".mrc");
                //SubtomoCropped.Dispose();

                Subtomo?.Dispose();
                SubtomoCTF?.Dispose();
            }//);

            GPU.DestroyFFTPlan(PlanForw);
            GPU.DestroyFFTPlan(PlanBack);
            GPU.DestroyFFTPlan(PlanForwCTF);

            CTFCoords.Dispose();
        }

        public void GetSubtomo(Image tiltStack, float3 coords, float3 angles, Image ctfCoords, out Image subtomo, out Image subtomoCTF, int planForw = -1, int planBack = -1, int planForwCTF = -1)
        {
            int Size = ctfCoords.Dims.X;
            float3[] ImageAngles = GetAngleInImages(coords);

            Image ImagesFT = GetSubtomoImages(tiltStack, Size, coords, true);
            Image CTFs = GetSubtomoCTFs(coords, ctfCoords, true, false, false);
            //Image CTFWeights = GetSubtomoCTFs(coords, ctfCoords, true, true);

            ImagesFT.Multiply(CTFs);    // Weight and phase-flip image FTs by CTF, which still has its sign here
            //ImagesFT.Multiply(CTFWeights);
            CTFs.Abs();                 // CTF has to be positive from here on since image FT phases are now flipped

            // CTF has to be converted to complex numbers with imag = 0, and weighted by itself
            float2[] CTFsComplexData = new float2[CTFs.ElementsComplex];
            float[] CTFsContinuousData = CTFs.GetHostContinuousCopy();
            for (int i = 0; i < CTFsComplexData.Length; i++)
                CTFsComplexData[i] = new float2(CTFsContinuousData[i] * CTFsContinuousData[i], 0);

            Image CTFsComplex = new Image(CTFsComplexData, CTFs.Dims, true);

            Projector ProjSubtomo = new Projector(new int3(Size, Size, Size), 2);
            lock (GPU.Sync)
                ProjSubtomo.BackProject(ImagesFT, CTFs, ImageAngles);
            subtomo = ProjSubtomo.Reconstruct(false, planForw, planBack, planForwCTF);
            ProjSubtomo.Dispose();
            
            GPU.NormParticles(subtomo.GetDevice(Intent.Read),
                              subtomo.GetDevice(Intent.Write),
                              subtomo.Dims,
                              (uint)(MainWindow.Options.ExportParticleRadius / CTF.PixelSize),
                              false,
                              1);
            //subtomo = new Image(new int3(1, 1, 1));

            Projector ProjCTF = new Projector(new int3(Size, Size, Size), 2);
            lock (GPU.Sync)
                ProjCTF.BackProject(CTFsComplex, CTFs, ImageAngles);
            subtomoCTF = ProjCTF.Reconstruct(true, planForw, planBack, planForwCTF);
            ProjCTF.Dispose();
            //subtomoCTF = new Image(new int3(1, 1, 1));

            ImagesFT.Dispose();
            CTFs.Dispose();
            //CTFWeights.Dispose();
            CTFsComplex.Dispose();
        }

        public Image GetSubtomoImages(Image tiltStack, int size, float3 coords, bool normalize = false, float imageScale = 1.0f)
        {
            float3[] PerTiltCoords = new float3[NTilts];
            for (int i = 0; i < NTilts; i++)
                PerTiltCoords[i] = coords;

            return GetSubtomoImages(tiltStack, size, PerTiltCoords, normalize, imageScale);
        }

        public Image GetSubtomoImages(Image tiltStack, int size, float3[] coords, bool normalize = false, float imageScale = 1.0f)
        {
            float3[] ImagePositions = GetPositionInImages(coords);

            if (imageScale != 1.0f)
                for (int i = 0; i < ImagePositions.Length; i++)
                    ImagePositions[i] *= imageScale;

            Image Result = new Image(new int3(size, size, NTilts));
            float[][] ResultData = Result.GetHost(Intent.Write);
            float3[] Shifts = new float3[NTilts];

            int3 DimsStack = tiltStack.Dims;

            Parallel.For(0, NTilts, t =>
            {
                ImagePositions[t] -= size / 2;
                int2 IntPosition = new int2((int)ImagePositions[t].X, (int)ImagePositions[t].Y);
                float2 Residual = new float2(-(ImagePositions[t].X - IntPosition.X), -(ImagePositions[t].Y - IntPosition.Y));
                Shifts[t] = new float3(Residual);

                float[] OriginalData;
                lock (tiltStack)
                    OriginalData = tiltStack.GetHost(Intent.Read)[t];

                float[] ImageData = ResultData[t];
                for (int y = 0; y < size; y++)
                {
                    int PosY = (y + IntPosition.Y + DimsStack.Y) % DimsStack.Y;
                    for (int x = 0; x < size; x++)
                    {
                        int PosX = (x + IntPosition.X + DimsStack.X) % DimsStack.X;
                        ImageData[y * size + x] = OriginalData[PosY * DimsStack.X + PosX];
                    }
                }
            });

            if (normalize)
                GPU.NormParticles(Result.GetDevice(Intent.Read),
                                  Result.GetDevice(Intent.Write),
                                  Result.Dims.Slice(),
                                  (uint)(MainWindow.Options.ExportParticleRadius / CTF.PixelSize),
                                  true,
                                  (uint)NTilts);

            Result.RemapToFT();
            Result.ShiftSlices(Shifts);

            Image ResultFT = Result.AsFFT();
            Result.Dispose();

            return ResultFT;
        }

        public Image GetSubtomoCTFs(float3 coords, Image ctfCoords, bool weighted = true, bool weightsonly = false, bool useglobalweights = true)
        {
            float3[] PerTiltCoords = new float3[NTilts];
            for (int i = 0; i < NTilts; i++)
                PerTiltCoords[i] = coords;

            return GetSubtomoCTFs(PerTiltCoords, ctfCoords, weighted, weightsonly, useglobalweights);
        }

        public Image GetSubtomoCTFs(float3[] coords, Image ctfCoords, bool weighted = true, bool weightsonly = false, bool useglobalweights = true)
        {
            float3[] ImagePositions = GetPositionInImages(coords);

            float GridStep = 1f / (NTilts - 1);
            CTFStruct[] Params = new CTFStruct[NTilts];
            for (int t = 0; t < NTilts; t++)
            {
                decimal Defocus = (decimal)ImagePositions[t].Z;
                decimal DefocusDelta = (decimal)GridCTFDefocusDelta.GetInterpolated(new float3(0.5f, 0.5f, t * GridStep));
                decimal DefocusAngle = (decimal)GridCTFDefocusAngle.GetInterpolated(new float3(0.5f, 0.5f, t * GridStep));

                CTF CurrCTF = CTF.GetCopy();
                if (!weightsonly)
                {
                    CurrCTF.Defocus = Defocus;
                    CurrCTF.DefocusDelta = DefocusDelta;
                    CurrCTF.DefocusAngle = DefocusAngle;
                }
                else
                {
                    CurrCTF.Defocus = 0;
                    CurrCTF.DefocusDelta = 0;
                    CurrCTF.Cs = 0;
                    CurrCTF.Amplitude = 1;
                }

                //CurrCTF.PixelSize *= 4;
                if (weighted)
                {
                    if (GridAngleWeights.Dimensions.Elements() <= 1)
                        CurrCTF.Scale = (decimal)Math.Cos(AnglesCorrect[t] * Helper.ToRad);
                    else
                        CurrCTF.Scale = (decimal)GridAngleWeights.GetInterpolated(new float3(0.5f, 0.5f, t * GridStep));

                    if (GridDoseBfacs.Dimensions.Elements() <= 1)
                        CurrCTF.Bfactor = (decimal)-Dose[t] * 8;
                    else
                        CurrCTF.Bfactor = (decimal)GridDoseBfacs.GetInterpolated(new float3(0.5f, 0.5f, t * GridStep));

                    if (useglobalweights)
                    {
                        CurrCTF.Scale *= (decimal)GlobalWeight;
                        CurrCTF.Bfactor += (decimal)GlobalBfactor;
                    }
                }

                Params[t] = CurrCTF.ToStruct();
            }

            Image Result = new Image(IntPtr.Zero, new int3(ctfCoords.Dims.X, ctfCoords.Dims.Y, NTilts), true);
            GPU.CreateCTF(Result.GetDevice(Intent.Write), ctfCoords.GetDevice(Intent.Read), (uint)Result.ElementsSliceReal, Params, false, (uint)NTilts);

            return Result;
        }

        public Image GetCTFCoords(int size, int originalSize)
        {
            float PixelSize = (float)MainWindow.Options.BinnedPixelSize;
            Image CTFCoords;
            {
                float2[] CTFCoordsData = new float2[(size / 2 + 1) * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size / 2 + 1; x++)
                    {
                        int xx = x;
                        int yy = y < size / 2 + 1 ? y : y - size;

                        float xs = xx / (float)originalSize;
                        float ys = yy / (float)originalSize;
                        float r = (float)Math.Sqrt(xs * xs + ys * ys);
                        float angle = (float)Math.Atan2(yy, xx);

                        CTFCoordsData[y * (size / 2 + 1) + x] = new float2(r, angle);
                    }

                CTFCoords = new Image(CTFCoordsData, new int3(size, size, 1), true);
            }

            return CTFCoords;
        }

        public void MakePerTomogramReconstructions(Star tableIn, Image tiltStack, int size, int3 volumeDimensions)
        {
            VolumeDimensions = volumeDimensions;

            #region Get rows from table

            List<int> RowIndices = new List<int>();
            string[] ColumnMicrographName = tableIn.GetColumn("rlnMicrographName");
            for (int i = 0; i < ColumnMicrographName.Length; i++)
                if (ColumnMicrographName[i].Contains(RootName + "."))
                    RowIndices.Add(i);

            if (RowIndices.Count == 0)
                return;

            int NParticles = RowIndices.Count;

            #endregion

            #region Make sure all columns and directories are there
            
            if (!Directory.Exists(WeightOptimizationDir))
                Directory.CreateDirectory(WeightOptimizationDir);

            #endregion

            #region Get subtomo positions from table

            float3[] ParticleOrigins = new float3[NParticles];
            float3[] ParticleOrigins2 = new float3[NParticles];
            float3[] ParticleAngles = new float3[NParticles];
            float3[] ParticleAngles2 = new float3[NParticles];
            int[] ParticleSubset = new int[NParticles];
            {
                string[] ColumnPosX = tableIn.GetColumn("rlnCoordinateX");
                string[] ColumnPosY = tableIn.GetColumn("rlnCoordinateY");
                string[] ColumnPosZ = tableIn.GetColumn("rlnCoordinateZ");
                string[] ColumnOriginX = tableIn.GetColumn("rlnOriginX");
                string[] ColumnOriginY = tableIn.GetColumn("rlnOriginY");
                string[] ColumnOriginZ = tableIn.GetColumn("rlnOriginZ");
                string[] ColumnAngleRot = tableIn.GetColumn("rlnAngleRot");
                string[] ColumnAngleTilt = tableIn.GetColumn("rlnAngleTilt");
                string[] ColumnAnglePsi = tableIn.GetColumn("rlnAnglePsi");
                string[] ColumnSubset = tableIn.GetColumn("rlnRandomSubset");

                string[] ColumnPosX2 = tableIn.GetColumn("rlnOriginXPrior");
                string[] ColumnPosY2 = tableIn.GetColumn("rlnOriginYPrior");
                string[] ColumnPosZ2 = tableIn.GetColumn("rlnOriginZPrior");
                string[] ColumnAngleRot2 = tableIn.GetColumn("rlnAngleRotPrior");
                string[] ColumnAngleTilt2 = tableIn.GetColumn("rlnAngleTiltPrior");
                string[] ColumnAnglePsi2 = tableIn.GetColumn("rlnAnglePsiPrior");

                for (int i = 0; i < NParticles; i++)
                {
                    float3 Pos = new float3(float.Parse(ColumnPosX[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosY[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosZ[RowIndices[i]], CultureInfo.InvariantCulture));
                    float3 Pos2 = Pos;
                    if (ColumnPosX2 != null && ColumnPosY2 != null && ColumnPosZ2 != null)
                        Pos2 = new float3(float.Parse(ColumnPosX2[RowIndices[i]], CultureInfo.InvariantCulture),
                                          float.Parse(ColumnPosY2[RowIndices[i]], CultureInfo.InvariantCulture),
                                          float.Parse(ColumnPosZ2[RowIndices[i]], CultureInfo.InvariantCulture));

                    float3 Shift = new float3(float.Parse(ColumnOriginX[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginY[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginZ[RowIndices[i]], CultureInfo.InvariantCulture));

                    ParticleOrigins[i] = Pos - Shift;
                    ParticleOrigins2[i] = Pos2 - Shift;
                    //ParticleOrigins[i] /= new float3(3838f / 959f, 3710f / 927f, 4f);

                    float3 Angle = new float3(float.Parse(ColumnAngleRot[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnAngleTilt[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnAnglePsi[RowIndices[i]], CultureInfo.InvariantCulture));
                    float3 Angle2 = Angle;
                    if (ColumnAngleRot2 != null && ColumnAngleTilt2 != null && ColumnAnglePsi2 != null)
                        Angle2 = new float3(float.Parse(ColumnAngleRot2[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnAngleTilt2[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnAnglePsi2[RowIndices[i]], CultureInfo.InvariantCulture));

                    ParticleAngles[i] = Angle;
                    ParticleAngles2[i] = Angle2;

                    ParticleSubset[i] = int.Parse(ColumnSubset[RowIndices[i]]);
                }
            }

            #endregion

            List<int> SubsetIDs = new List<int>();
            foreach (var i in ParticleSubset)
                if (!SubsetIDs.Contains(i))
                    SubsetIDs.Add(i);
            SubsetIDs.Sort();

            for (int subset = 0; subset < SubsetIDs.Count; subset++)
            {
                if (File.Exists(WeightOptimizationDir + $"{RootName}_subset{SubsetIDs[subset]}.mrc"))
                    continue;

                Tuple<Image, Image> Reconstruction = MakeReconstructionOneTomogram(tiltStack,
                                                                                    SubsetIDs[subset],
                                                                                    size,
                                                                                    ParticleOrigins,
                                                                                    ParticleOrigins2,
                                                                                    ParticleAngles,
                                                                                    ParticleAngles2,
                                                                                    ParticleSubset);

                Reconstruction.Item1.WriteMRC(WeightOptimizationDir + $"{RootName}_subset{SubsetIDs[subset]}.mrc");
                Reconstruction.Item1.Dispose();

                Reconstruction.Item2.WriteMRC(WeightOptimizationDir + $"{RootName}_subset{SubsetIDs[subset]}.weight.mrc");
                Reconstruction.Item2.Dispose();
            }
        }

        public Tuple<Image, Image> MakeReconstructionOneTomogram(Image tiltStack, int subset, int size, float3[] particleOrigins, float3[] particleOrigins2, float3[] particleAngles, float3[] particleAngles2, int[] particleSubset)
        {
            Projector MapProjector = new Projector(new int3(size, size, size), 2);
            Projector WeightProjector = new Projector(new int3(size, size, size), 2);

            List<int> ParticleIDs = new List<int>();
            for (int i = 0; i < particleSubset.Length; i++)
                if (particleSubset[i] == subset)
                    ParticleIDs.Add(i);
            int NParticles = ParticleIDs.Count;

            particleOrigins = ParticleIDs.Select(i => particleOrigins[i]).ToArray();
            particleOrigins2 = ParticleIDs.Select(i => particleOrigins2[i]).ToArray();
            particleAngles = ParticleIDs.Select(i => particleAngles[i]).ToArray();
            particleAngles2 = ParticleIDs.Select(i => particleAngles2[i]).ToArray();

            Image CTFCoords = GetCTFCoords(size, size);

            for (int angleID = 0; angleID < NTilts; angleID++)
            {
                float DoseID = IndicesSortedDose[angleID] / (float)(NTilts - 1);

                for (int b = 0; b < NParticles; b += 1024)
                {
                    int NBatch = Math.Min(1024, NParticles - b);

                    float3[] ParticleOriginsInterp = new float3[NBatch];
                    float3[] ParticleAnglesInterp = new float3[NBatch];
                    for (int n = 0; n < NBatch; n++)
                    {
                        float3 OriginDiff = particleOrigins2[b + n] - particleOrigins[b + n];
                        float3 AngleDiff = particleAngles2[b + n] - particleAngles[b + n];
                        ParticleOriginsInterp[n] = particleOrigins[b + n] + OriginDiff * DoseID;
                        ParticleAnglesInterp[n] = particleAngles[b + n] + AngleDiff * DoseID;
                    }

                    Image ParticleImages = GetImagesOneAngle(tiltStack, size, ParticleOriginsInterp, angleID, true);
                    Image ParticleCTFs = GetCTFsOneAngle(tiltStack, CTFCoords, ParticleOriginsInterp, angleID, true);

                    ParticleImages.Multiply(ParticleCTFs);
                    //ParticleCTFs.Multiply(ParticleCTFs);
                    ParticleCTFs.Abs();

                    MapProjector.BackProject(ParticleImages,
                                             ParticleCTFs,
                                             GetAnglesInOneAngle(ParticleOriginsInterp,
                                                                 ParticleAnglesInterp,
                                                                 angleID));

                    ParticleImages.Dispose();
                    ParticleCTFs.Dispose();

                    // Now reconstruct the weights which will be needed during optimization later
                    ParticleCTFs = GetCTFsOneAngle(tiltStack, CTFCoords, ParticleOriginsInterp, angleID, true);

                    // CTF has to be converted to complex numbers with imag = 0
                    float2[] CTFsComplexData = new float2[ParticleCTFs.ElementsComplex];
                    float[] CTFWeightsData = new float[ParticleCTFs.ElementsComplex];
                    float[] CTFsContinuousData = ParticleCTFs.GetHostContinuousCopy();
                    for (int i = 0; i < CTFsComplexData.Length; i++)
                    {
                        CTFsComplexData[i] = new float2(Math.Abs(CTFsContinuousData[i] * CTFsContinuousData[i]), 0);
                        CTFWeightsData[i] = Math.Abs(CTFsContinuousData[i]);
                    }

                    Image CTFsComplex = new Image(CTFsComplexData, ParticleCTFs.Dims, true);
                    Image CTFWeights = new Image(CTFWeightsData, ParticleCTFs.Dims, true);

                    WeightProjector.BackProject(CTFsComplex,
                                                CTFWeights,
                                                GetAnglesInOneAngle(ParticleOriginsInterp,
                                                                    ParticleAnglesInterp,
                                                                    angleID));

                    ParticleCTFs.Dispose();
                    CTFsComplex.Dispose();
                    CTFWeights.Dispose();
                }
            }

            CTFCoords.Dispose();

            //MapProjector.Weights.WriteMRC($"d_weights{angleID:D3}.mrc");
            Image Reconstruction = MapProjector.Reconstruct(false);
            MapProjector.Dispose();

            foreach (var slice in WeightProjector.Weights.GetHost(Intent.ReadWrite))
                for (int i = 0; i < slice.Length; i++)
                    slice[i] = Math.Min(slice[i], 1);

            Image ReconstructionWeights = WeightProjector.Reconstruct(true);
            WeightProjector.Dispose();

            return new Tuple<Image, Image>(Reconstruction, ReconstructionWeights);
        }

        public void AddToPerAngleReconstructions(Star tableIn, Image tiltStack, int size, int3 volumeDimensions, Dictionary<int, Projector[]> perAngleReconstructions, Dictionary<int, Projector[]> perAngleWeights)
        {
            VolumeDimensions = volumeDimensions;

            #region Get rows from table

            List<int> RowIndices = new List<int>();
            string[] ColumnMicrographName = tableIn.GetColumn("rlnMicrographName");
            for (int i = 0; i < ColumnMicrographName.Length; i++)
                if (ColumnMicrographName[i].Contains(RootName + "."))
                    RowIndices.Add(i);

            if (RowIndices.Count == 0)
                return;

            int NParticles = RowIndices.Count;

            #endregion

            #region Make sure all columns and directories are there

            if (!Directory.Exists(WeightOptimizationDir))
                Directory.CreateDirectory(WeightOptimizationDir);

            #endregion

            #region Get subtomo positions from table

            float3[] ParticleOrigins = new float3[NParticles];
            float3[] ParticleOrigins2 = new float3[NParticles];
            float3[] ParticleAngles = new float3[NParticles];
            float3[] ParticleAngles2 = new float3[NParticles];
            int[] ParticleSubset = new int[NParticles];
            {
                string[] ColumnPosX = tableIn.GetColumn("rlnCoordinateX");
                string[] ColumnPosY = tableIn.GetColumn("rlnCoordinateY");
                string[] ColumnPosZ = tableIn.GetColumn("rlnCoordinateZ");
                string[] ColumnOriginX = tableIn.GetColumn("rlnOriginX");
                string[] ColumnOriginY = tableIn.GetColumn("rlnOriginY");
                string[] ColumnOriginZ = tableIn.GetColumn("rlnOriginZ");
                string[] ColumnAngleRot = tableIn.GetColumn("rlnAngleRot");
                string[] ColumnAngleTilt = tableIn.GetColumn("rlnAngleTilt");
                string[] ColumnAnglePsi = tableIn.GetColumn("rlnAnglePsi");
                string[] ColumnSubset = tableIn.GetColumn("rlnRandomSubset");

                string[] ColumnPosX2 = tableIn.GetColumn("rlnOriginXPrior");
                string[] ColumnPosY2 = tableIn.GetColumn("rlnOriginYPrior");
                string[] ColumnPosZ2 = tableIn.GetColumn("rlnOriginZPrior");
                string[] ColumnAngleRot2 = tableIn.GetColumn("rlnAngleRotPrior");
                string[] ColumnAngleTilt2 = tableIn.GetColumn("rlnAngleTiltPrior");
                string[] ColumnAnglePsi2 = tableIn.GetColumn("rlnAnglePsiPrior");

                for (int i = 0; i < NParticles; i++)
                {
                    float3 Pos = new float3(float.Parse(ColumnPosX[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosY[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosZ[RowIndices[i]], CultureInfo.InvariantCulture));
                    float3 Pos2 = Pos;
                    if (ColumnPosX2 != null && ColumnPosY2 != null && ColumnPosZ2 != null)
                        Pos2 = new float3(float.Parse(ColumnPosX2[RowIndices[i]], CultureInfo.InvariantCulture),
                                          float.Parse(ColumnPosY2[RowIndices[i]], CultureInfo.InvariantCulture),
                                          float.Parse(ColumnPosZ2[RowIndices[i]], CultureInfo.InvariantCulture));

                    float3 Shift = new float3(float.Parse(ColumnOriginX[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginY[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginZ[RowIndices[i]], CultureInfo.InvariantCulture));

                    ParticleOrigins[i] = Pos - Shift;
                    ParticleOrigins2[i] = Pos2 - Shift;

                    float3 Angle = new float3(float.Parse(ColumnAngleRot[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnAngleTilt[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnAnglePsi[RowIndices[i]], CultureInfo.InvariantCulture));
                    float3 Angle2 = Angle;
                    if (ColumnAngleRot2 != null && ColumnAngleTilt2 != null && ColumnAnglePsi2 != null)
                        Angle2 = new float3(float.Parse(ColumnAngleRot2[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnAngleTilt2[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnAnglePsi2[RowIndices[i]], CultureInfo.InvariantCulture));

                    ParticleAngles[i] = Angle;
                    ParticleAngles2[i] = Angle2;

                    ParticleSubset[i] = int.Parse(ColumnSubset[RowIndices[i]]);
                }
            }

            #endregion

            List<int> SubsetIDs = new List<int>();
            foreach (var i in ParticleSubset)
                if (!SubsetIDs.Contains(i))
                    SubsetIDs.Add(i);
            SubsetIDs.Sort();
            SubsetIDs.Remove(1);
            SubsetIDs.Remove(2);

            foreach (int subsetID in SubsetIDs)
            {
                for (int t = 0; t < NTilts; t++)
                {
                    int AngleID = t;

                    lock (perAngleReconstructions[subsetID][t])
                    {
                        AddToReconstructionOneAngle(tiltStack,
                                                    subsetID,
                                                    size,
                                                    ParticleOrigins,
                                                    ParticleOrigins2,
                                                    ParticleAngles,
                                                    ParticleAngles2,
                                                    ParticleSubset,
                                                    AngleID,
                                                    perAngleReconstructions[subsetID][t],
                                                    perAngleWeights[subsetID][t]);

                        perAngleReconstructions[subsetID][t].FreeDevice();
                        perAngleWeights[subsetID][t].FreeDevice();
                    }
                }
            }
        }

        public void AddToReconstructionOneAngle(Image tiltStack,
                                                int subset,
                                                int size,
                                                float3[] particleOrigins,
                                                float3[] particleOrigins2,
                                                float3[] particleAngles,
                                                float3[] particleAngles2,
                                                int[] particleSubset,
                                                int angleID,
                                                Projector mapProjector,
                                                Projector weightProjector)
        {
            List<int> ParticleIDs = new List<int>();
            for (int i = 0; i < particleSubset.Length; i++)
                if (particleSubset[i] == subset)
                    ParticleIDs.Add(i);
            int NParticles = ParticleIDs.Count;

            particleOrigins = ParticleIDs.Select(i => particleOrigins[i]).ToArray();
            particleOrigins2 = ParticleIDs.Select(i => particleOrigins2[i]).ToArray();
            particleAngles = ParticleIDs.Select(i => particleAngles[i]).ToArray();
            particleAngles2 = ParticleIDs.Select(i => particleAngles2[i]).ToArray();

            Image CTFCoords = GetCTFCoords(size, size);

            float DoseID = IndicesSortedDose[angleID] / (float)(NTilts - 1);

            for (int b = 0; b < NParticles; b += 1024)
            {
                int NBatch = Math.Min(1024, NParticles - b);

                float3[] ParticleOriginsInterp = new float3[NBatch];
                float3[] ParticleAnglesInterp = new float3[NBatch];
                for (int n = 0; n < NBatch; n++)
                {
                    float3 OriginDiff = particleOrigins2[b + n] - particleOrigins[b + n];
                    float3 AngleDiff = particleAngles2[b + n] - particleAngles[b + n];
                    ParticleOriginsInterp[n] = particleOrigins[b + n] + OriginDiff * DoseID;
                    ParticleAnglesInterp[n] = particleAngles[b + n] + AngleDiff * DoseID;
                }

                Image ParticleImages = GetImagesOneAngle(tiltStack, size, ParticleOriginsInterp, angleID, true);
                Image ParticleCTFs = GetCTFsOneAngle(tiltStack, CTFCoords, ParticleOriginsInterp, angleID, false);

                ParticleImages.Multiply(ParticleCTFs);
                //ParticleCTFs.Multiply(ParticleCTFs);
                ParticleCTFs.Abs();

                mapProjector.BackProject(ParticleImages,
                                         ParticleCTFs,
                                         GetAnglesInOneAngle(ParticleOriginsInterp,
                                                             ParticleAnglesInterp,
                                                             angleID));

                ParticleImages.Dispose();
                ParticleCTFs.Dispose();

                // Now reconstruct the weights which will be needed during optimization later
                ParticleCTFs = GetCTFsOneAngle(tiltStack, CTFCoords, ParticleOriginsInterp, angleID, false);

                // CTF has to be converted to complex numbers with imag = 0
                float2[] CTFsComplexData = new float2[ParticleCTFs.ElementsComplex];
                float[] CTFWeightsData = new float[ParticleCTFs.ElementsComplex];
                float[] CTFsContinuousData = ParticleCTFs.GetHostContinuousCopy();
                for (int i = 0; i < CTFsComplexData.Length; i++)
                {
                    CTFsComplexData[i] = new float2(Math.Abs(CTFsContinuousData[i] * CTFsContinuousData[i]), 0);
                    CTFWeightsData[i] = Math.Abs(CTFsContinuousData[i]);
                }

                Image CTFsComplex = new Image(CTFsComplexData, ParticleCTFs.Dims, true);
                Image CTFWeights = new Image(CTFWeightsData, ParticleCTFs.Dims, true);

                weightProjector.BackProject(CTFsComplex,
                                            CTFWeights,
                                            GetAnglesInOneAngle(ParticleOriginsInterp,
                                                                ParticleAnglesInterp,
                                                                angleID));

                ParticleCTFs.Dispose();
                CTFsComplex.Dispose();
                CTFWeights.Dispose();
            }

            CTFCoords.Dispose();
        }

        public Image GetImagesOneAngle(Image tiltStack, int size, float3[] particleOrigins, int angleID, bool normalize)
        {
            int NParticles = particleOrigins.Length;

            float3[] ImagePositions = GetPositionsInOneAngle(particleOrigins, angleID);

            Image Result = new Image(new int3(size, size, NParticles));
            float[][] ResultData = Result.GetHost(Intent.Write);
            float3[] Shifts = new float3[NParticles];

            int3 DimsStack = tiltStack.Dims;

            Parallel.For(0, NParticles, p =>
            {
                ImagePositions[p] -= new float3(size / 2, size / 2, 0);
                int2 IntPosition = new int2((int)ImagePositions[p].X, (int)ImagePositions[p].Y);
                float2 Residual = new float2(-(ImagePositions[p].X - IntPosition.X), -(ImagePositions[p].Y - IntPosition.Y));
                Residual -= size / 2;
                Shifts[p] = new float3(Residual);

                float[] OriginalData;
                lock (tiltStack)
                    OriginalData = tiltStack.GetHost(Intent.Read)[angleID];

                float[] ImageData = ResultData[p];
                for (int y = 0; y < size; y++)
                {
                    int PosY = (y + IntPosition.Y + DimsStack.Y) % DimsStack.Y;
                    for (int x = 0; x < size; x++)
                    {
                        int PosX = (x + IntPosition.X + DimsStack.X) % DimsStack.X;
                        ImageData[y * size + x] = OriginalData[PosY * DimsStack.X + PosX];
                    }
                }
            });
            if (normalize)
                GPU.NormParticles(Result.GetDevice(Intent.Read),
                                  Result.GetDevice(Intent.Write),
                                  Result.Dims.Slice(),
                                  (uint)(MainWindow.Options.ExportParticleRadius / CTF.PixelSize),
                                  true,
                                  (uint)NParticles);
            //Result.WriteMRC($"d_paticleimages_{angleID:D3}.mrc");

            Result.ShiftSlices(Shifts);

            Image ResultFT = Result.AsFFT();
            Result.Dispose();

            return ResultFT;
        }

        public Image GetCTFsOneAngle(Image tiltStack, Image ctfCoords, float3[] particleOrigins, int angleID, bool weighted = true, bool weightsonly = false, bool useglobalweights = true)
        {
            int NParticles = particleOrigins.Length;
            float3[] ImagePositions = GetPositionsInOneAngle(particleOrigins, angleID);

            float GridStep = 1f / (NTilts - 1);
            CTFStruct[] Params = new CTFStruct[NParticles];
            for (int p = 0; p < NParticles; p++)
            {
                decimal Defocus = (decimal)ImagePositions[p].Z;
                decimal DefocusDelta = (decimal)GridCTFDefocusDelta.GetInterpolated(new float3(0.5f, 0.5f, angleID * GridStep));
                decimal DefocusAngle = (decimal)GridCTFDefocusAngle.GetInterpolated(new float3(0.5f, 0.5f, angleID * GridStep));

                CTF CurrCTF = CTF.GetCopy();
                if (!weightsonly)
                {
                    CurrCTF.Defocus = Defocus;
                    CurrCTF.DefocusDelta = DefocusDelta;
                    CurrCTF.DefocusAngle = DefocusAngle;
                }
                else
                {
                    CurrCTF.Defocus = 0;
                    CurrCTF.DefocusDelta = 0;
                    CurrCTF.Cs = 0;
                    CurrCTF.Amplitude = 1;
                }
                //CurrCTF.PixelSize *= 4;

                if (weighted)
                {
                    if (GridAngleWeights.Dimensions.Elements() <= 1)
                        CurrCTF.Scale = (decimal)Math.Cos(AnglesCorrect[angleID] * Helper.ToRad);
                    else
                        CurrCTF.Scale = (decimal)GridAngleWeights.GetInterpolated(new float3(0.5f, 0.5f, angleID * GridStep));

                    if (GridDoseBfacs.Dimensions.Elements() <= 1)
                        CurrCTF.Bfactor = (decimal)-Dose[angleID] * 8;
                    else
                        CurrCTF.Bfactor = (decimal)GridDoseBfacs.GetInterpolated(new float3(0.5f, 0.5f, angleID * GridStep));

                    if (useglobalweights)
                    {
                        CurrCTF.Scale *= (decimal)GlobalWeight;
                        CurrCTF.Bfactor += (decimal)GlobalBfactor;
                    }
                }

                Params[p] = CurrCTF.ToStruct();
            }

            Image Result = new Image(IntPtr.Zero, new int3(ctfCoords.Dims.X, ctfCoords.Dims.Y, NParticles), true);
            GPU.CreateCTF(Result.GetDevice(Intent.Write), ctfCoords.GetDevice(Intent.Read), (uint)Result.ElementsSliceReal, Params, false, (uint)NParticles);

            return Result;
        }

        public float3[] GetPositionInImages(float3 coords)
        {
            float3[] PerTiltCoords = new float3[NTilts];
            for (int i = 0; i < NTilts; i++)
                PerTiltCoords[i] = coords;

            return GetPositionInImages(PerTiltCoords);
        }

        public float3[] GetPositionInImages(float3[] coords)
        {
            float3 Center = new float3(VolumeDimensions.X / 2, VolumeDimensions.Y / 2, VolumeDimensions.Z / 2);

            float GridStep = 1f / (NTilts - 1);

            int[] PrecalcDoseIDs = IndicesSortedDose;

            float3[] GridCoords = new float3[coords.Length];
            float3[] TemporalGridCoords = new float3[coords.Length];
            for (int i = 0; i < coords.Length; i++)
            {
                int t = i % NTilts;

                GridCoords[i] = new float3(coords[i].X / Dimensions.X, coords[i].Y / Dimensions.Y, t * GridStep);
                TemporalGridCoords[i] = new float3(GridCoords[i].X, GridCoords[i].Y, PrecalcDoseIDs[t] * GridStep);
            }

            float[] GridAngleXInterp = GridAngleX.GetInterpolatedNative(GridCoords);
            float[] GridAngleYInterp = GridAngleY.GetInterpolatedNative(GridCoords);
            float[] GridAngleZInterp = GridAngleZ.GetInterpolatedNative(GridCoords);

            float[] GridLocalXInterp = GridLocalX.GetInterpolatedNative(TemporalGridCoords);
            float[] GridLocalYInterp = GridLocalY.GetInterpolatedNative(TemporalGridCoords);
            float[] GridLocalZInterp = GridLocalZ.GetInterpolatedNative(TemporalGridCoords);

            float[] GridCTFInterp = GridCTF.GetInterpolatedNative(GridCoords.Take(NTilts).ToArray());

            float3[] Transformed = new float3[coords.Length];

            Matrix3[] TiltMatrices = AnglesCorrect.Select(a => Matrix3.Euler(0, a * Helper.ToRad, 0)).ToArray();

            for (int i = 0; i < coords.Length; i++)
            {
                int t = i % NTilts;
                float3 Centered = coords[i] - Center;
                
                Matrix3 CorrectionMatrix = Matrix3.RotateZ(GridAngleZInterp[i] * Helper.ToRad) *
                                           Matrix3.RotateY(GridAngleYInterp[i] * Helper.ToRad) *
                                           Matrix3.RotateX(GridAngleXInterp[i] * Helper.ToRad);

                Matrix3 Rotation = CorrectionMatrix * TiltMatrices[t];

                float3 SampleWarping = new float3(GridLocalXInterp[i],
                                                  GridLocalYInterp[i],
                                                  GridLocalZInterp[i]);
                Centered += SampleWarping;

                Transformed[i] = (Rotation * Centered) + Center;

                // Defocus is affected by the inverse of the rotation matrix
                Transformed[i].Z = (AreAnglesInverted ? -1 : 1) *
                                   (Rotation * Centered).Z *
                                   (float)MainWindow.Options.BinnedPixelSize * 1 / 1e4f +
                                   GridCTFInterp[t];
            }

            float3[] TransformedCoords = new float3[coords.Length];
            for (int i = 0; i < coords.Length; i++)
            {
                int t = i % NTilts;
                TransformedCoords[i] = new float3(Transformed[i].X / Dimensions.X, Transformed[i].Y / Dimensions.Y, t * GridStep);
            }

            float[] GridMovementXInterp = GridMovementX.GetInterpolatedNative(TransformedCoords);
            float[] GridMovementYInterp = GridMovementY.GetInterpolatedNative(TransformedCoords);

            for (int i = 0; i < coords.Length; i++)
            {
                Transformed[i].X += GridMovementXInterp[i];
                Transformed[i].Y += GridMovementYInterp[i];
            }

            return Transformed;
        }

        public float3[] GetPositionsInOneAngle(float3[] coords, int angleID)
        {
            float3[] Result = new float3[coords.Length];

            float3 Center = new float3(VolumeDimensions.X / 2, VolumeDimensions.Y / 2, VolumeDimensions.Z / 2);

            float GridStep = 1f / (NTilts - 1);

            int[] PrecalcDoseIDs = IndicesSortedDose;

            for (int p = 0; p < coords.Length; p++)
            {
                float3 GridCoords = new float3(coords[p].X / Dimensions.X, coords[p].Y / Dimensions.Y, angleID * GridStep);
                float3 Centered = coords[p] - Center;

                Matrix3 TiltMatrix = Matrix3.Euler(0, AnglesCorrect[angleID] * Helper.ToRad, 0);
                Matrix3 CorrectionMatrix = Matrix3.RotateZ(GridAngleZ.GetInterpolated(GridCoords) * Helper.ToRad) *
                                           Matrix3.RotateY(GridAngleY.GetInterpolated(GridCoords) * Helper.ToRad) *
                                           Matrix3.RotateX(GridAngleX.GetInterpolated(GridCoords) * Helper.ToRad);

                Matrix3 Rotation = CorrectionMatrix * TiltMatrix;

                float3 TemporalGridCoords = new float3(GridCoords.X, GridCoords.Y, PrecalcDoseIDs[angleID] * GridStep);
                float3 SampleWarping = new float3(GridLocalX.GetInterpolated(TemporalGridCoords),
                                                  GridLocalY.GetInterpolated(TemporalGridCoords),
                                                  GridLocalZ.GetInterpolated(TemporalGridCoords));
                Centered += SampleWarping;

                float3 Transformed = (Rotation * Centered) + Center;

                // Defocus is affected by the inverse of the rotation matrix
                Transformed.Z = (AreAnglesInverted ? -1 : 1) *
                                (Rotation * Centered).Z *
                                (float)MainWindow.Options.BinnedPixelSize * 1 / 1e4f +
                                GridCTF.GetInterpolated(GridCoords);

                float3 TransformedCoords = new float3(Transformed.X / Dimensions.X, Transformed.Y / Dimensions.Y, angleID * GridStep);
                float3 CorrectionStage = new float3(GridMovementX.GetInterpolated(TransformedCoords), GridMovementY.GetInterpolated(TransformedCoords), 0);

                Transformed = Transformed + CorrectionStage;

                Result[p] = Transformed;
            }

            return Result;
        }

        public float3[] GetAngleInImages(float3 coords)
        {
            float3[] PerTiltCoords = new float3[NTilts];
            for (int i = 0; i < NTilts; i++)
                PerTiltCoords[i] = coords;

            return GetAngleInImages(PerTiltCoords);
        }

        public float3[] GetAngleInImages(float3[] coords)
        {
            float3[] Result = new float3[coords.Length];

            float GridStep = 1f / (NTilts - 1);

            float3[] GridCoords = new float3[coords.Length];
            float3[] TemporalGridCoords = new float3[coords.Length];
            for (int i = 0; i < coords.Length; i++)
            {
                int t = i % NTilts;
                GridCoords[i] = new float3(coords[i].X / Dimensions.X, coords[i].Y / Dimensions.Y, t * GridStep);
            }

            float[] GridAngleXInterp = GridAngleX.GetInterpolatedNative(GridCoords);
            float[] GridAngleYInterp = GridAngleY.GetInterpolatedNative(GridCoords);
            float[] GridAngleZInterp = GridAngleZ.GetInterpolatedNative(GridCoords);

            Matrix3[] TiltMatrices = AnglesCorrect.Select(a => Matrix3.Euler(0, a * Helper.ToRad, 0)).ToArray();

            for (int i = 0; i < coords.Length; i++)
            {
                int t = i % NTilts;

                Matrix3 CorrectionMatrix = Matrix3.RotateZ(GridAngleZInterp[i] * Helper.ToRad) *
                                           Matrix3.RotateY(GridAngleYInterp[i] * Helper.ToRad) *
                                           Matrix3.RotateX(GridAngleXInterp[i] * Helper.ToRad);

                Matrix3 Rotation = CorrectionMatrix * TiltMatrices[t];

                Result[i] = Matrix3.EulerFromMatrix(Rotation);
            }

            return Result;
        }

        public float3[] GetParticleAngleInImages(float3 coords, float3 angle)
        {
            float3[] PerTiltCoords = new float3[NTilts];
            float3[] PerTiltAngles = new float3[NTilts];
            for (int i = 0; i < NTilts; i++)
            {
                PerTiltCoords[i] = coords;
                PerTiltAngles[i] = angle;
            }

            return GetParticleAngleInImages(PerTiltCoords, PerTiltAngles);
        }

        public float3[] GetParticleAngleInImages(float3[] coords, float3[] angle)
        {
            float3[] Result = new float3[coords.Length];

            float GridStep = 1f / (NTilts - 1);

            float3[] GridCoords = new float3[coords.Length];
            float3[] TemporalGridCoords = new float3[coords.Length];
            for (int i = 0; i < coords.Length; i++)
            {
                int t = i % NTilts;
                GridCoords[i] = new float3(coords[i].X / Dimensions.X, coords[i].Y / Dimensions.Y, t * GridStep);
            }

            float[] GridAngleXInterp = GridAngleX.GetInterpolatedNative(GridCoords);
            float[] GridAngleYInterp = GridAngleY.GetInterpolatedNative(GridCoords);
            float[] GridAngleZInterp = GridAngleZ.GetInterpolatedNative(GridCoords);

            Matrix3[] TiltMatrices = AnglesCorrect.Select(a => Matrix3.Euler(0, a * Helper.ToRad, 0)).ToArray();

            for (int i = 0; i < coords.Length; i++)
            {
                int t = i % NTilts;

                Matrix3 ParticleMatrix = Matrix3.Euler(angle[i].X * Helper.ToRad,
                                                       angle[i].Y * Helper.ToRad,
                                                       angle[i].Z * Helper.ToRad);

                Matrix3 CorrectionMatrix = Matrix3.RotateZ(GridAngleZInterp[i] * Helper.ToRad) *
                                           Matrix3.RotateY(GridAngleYInterp[i] * Helper.ToRad) *
                                           Matrix3.RotateX(GridAngleXInterp[i] * Helper.ToRad);

                Matrix3 Rotation = CorrectionMatrix * TiltMatrices[t] * ParticleMatrix;

                Result[i] = Matrix3.EulerFromMatrix(Rotation);
            }

            return Result;
        }

        public float3[] GetAnglesInOneAngle(float3[] coords, float3[] particleAngles, int angleID)
        {
            int NParticles = coords.Length;
            float3[] Result = new float3[NParticles];

            float GridStep = 1f / (NTilts - 1);

            for (int p = 0; p < NParticles; p++)
            {
                float3 GridCoords = new float3(coords[p].X / Dimensions.X, coords[p].Y / Dimensions.Y, angleID * GridStep);

                Matrix3 ParticleMatrix = Matrix3.Euler(particleAngles[p].X * Helper.ToRad,
                                                       particleAngles[p].Y * Helper.ToRad,
                                                       particleAngles[p].Z * Helper.ToRad);

                Matrix3 TiltMatrix = Matrix3.Euler(0, AnglesCorrect[angleID] * Helper.ToRad, 0);

                Matrix3 CorrectionMatrix = Matrix3.RotateZ(GridAngleZ.GetInterpolated(GridCoords) * Helper.ToRad) *
                                           Matrix3.RotateY(GridAngleY.GetInterpolated(GridCoords) * Helper.ToRad) *
                                           Matrix3.RotateX(GridAngleX.GetInterpolated(GridCoords) * Helper.ToRad);

                Matrix3 Rotation = CorrectionMatrix * TiltMatrix * ParticleMatrix;

                Result[p] = Matrix3.EulerFromMatrix(Rotation);
            }

            return Result;
        }

        public void PerformOptimizationStep(Star tableIn, Image tiltStack, int size, int3 volumeDimensions, Dictionary<int, Projector> references, float resolution, Dictionary<int, Projector> outReconstructions, Dictionary<int, Projector> outCTFReconstructions)
        {
            VolumeDimensions = volumeDimensions;

            #region Get rows from table

            List<int> RowIndices = new List<int>();
            string[] ColumnMicrographName = tableIn.GetColumn("rlnMicrographName");
            for (int i = 0; i < ColumnMicrographName.Length; i++)
                if (ColumnMicrographName[i].Contains(RootName + "."))
                    RowIndices.Add(i);

            if (RowIndices.Count == 0)
                return;

            int NParticles = RowIndices.Count;

            #endregion

            #region Make sure all columns and directories are there

            if (!tableIn.HasColumn("rlnImageName"))
                tableIn.AddColumn("rlnImageName");
            if (!tableIn.HasColumn("rlnCtfImage"))
                tableIn.AddColumn("rlnCtfImage");
            if (!tableIn.HasColumn("rlnParticleSelectZScore"))
                tableIn.AddColumn("rlnParticleSelectZScore");

            if (!Directory.Exists(ParticlesDir))
                Directory.CreateDirectory(ParticlesDir);
            if (!Directory.Exists(ParticleCTFDir))
                Directory.CreateDirectory(ParticleCTFDir);

            #endregion

            #region Get subtomo positions from table

            float3[] ParticleOrigins = new float3[NParticles];
            float3[] ParticleOrigins2 = new float3[NParticles];
            float3[] ParticleAngles = new float3[NParticles];
            float3[] ParticleAngles2 = new float3[NParticles];
            int[] ParticleSubset = new int[NParticles];
            {
                string[] ColumnPosX = tableIn.GetColumn("rlnCoordinateX");
                string[] ColumnPosY = tableIn.GetColumn("rlnCoordinateY");
                string[] ColumnPosZ = tableIn.GetColumn("rlnCoordinateZ");
                string[] ColumnOriginX = tableIn.GetColumn("rlnOriginX");
                string[] ColumnOriginY = tableIn.GetColumn("rlnOriginY");
                string[] ColumnOriginZ = tableIn.GetColumn("rlnOriginZ");
                string[] ColumnAngleRot = tableIn.GetColumn("rlnAngleRot");
                string[] ColumnAngleTilt = tableIn.GetColumn("rlnAngleTilt");
                string[] ColumnAnglePsi = tableIn.GetColumn("rlnAnglePsi");
                string[] ColumnSubset = tableIn.GetColumn("rlnRandomSubset");

                string[] ColumnPosX2 = tableIn.GetColumn("rlnOriginXPrior");
                string[] ColumnPosY2 = tableIn.GetColumn("rlnOriginYPrior");
                string[] ColumnPosZ2 = tableIn.GetColumn("rlnOriginZPrior");
                string[] ColumnAngleRot2 = tableIn.GetColumn("rlnAngleRotPrior");
                string[] ColumnAngleTilt2 = tableIn.GetColumn("rlnAngleTiltPrior");
                string[] ColumnAnglePsi2 = tableIn.GetColumn("rlnAnglePsiPrior");

                for (int i = 0; i < NParticles; i++)
                {
                    float3 Pos = new float3(float.Parse(ColumnPosX[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosY[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosZ[RowIndices[i]], CultureInfo.InvariantCulture));
                    float3 Pos2 = Pos;
                    //if (ColumnPosX2 != null && ColumnPosY2 != null && ColumnPosZ2 != null)
                    //    Pos2 = new float3(float.Parse(ColumnPosX2[RowIndices[i]], CultureInfo.InvariantCulture),
                    //                      float.Parse(ColumnPosY2[RowIndices[i]], CultureInfo.InvariantCulture),
                    //                      float.Parse(ColumnPosZ2[RowIndices[i]], CultureInfo.InvariantCulture));

                    float3 Shift = new float3(float.Parse(ColumnOriginX[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginY[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginZ[RowIndices[i]], CultureInfo.InvariantCulture));

                    ParticleOrigins[i] = Pos - Shift;
                    ParticleOrigins2[i] = Pos2 - Shift;

                    float3 Angle = new float3(float.Parse(ColumnAngleRot[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnAngleTilt[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnAnglePsi[RowIndices[i]], CultureInfo.InvariantCulture));
                    float3 Angle2 = Angle;
                    //if (ColumnAngleRot2 != null && ColumnAngleTilt2 != null && ColumnAnglePsi2 != null)
                    //    Angle2 = new float3(float.Parse(ColumnAngleRot2[RowIndices[i]], CultureInfo.InvariantCulture),
                    //                        float.Parse(ColumnAngleTilt2[RowIndices[i]], CultureInfo.InvariantCulture),
                    //                        float.Parse(ColumnAnglePsi2[RowIndices[i]], CultureInfo.InvariantCulture));

                    ParticleAngles[i] = Angle;
                    ParticleAngles2[i] = Angle2;

                    ParticleSubset[i] = int.Parse(ColumnSubset[RowIndices[i]]);

                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateX", ParticleOrigins[i].X.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateY", ParticleOrigins[i].Y.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateZ", ParticleOrigins[i].Z.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginX", "0.0");
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginY", "0.0");
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginZ", "0.0");
                }
            }

            #endregion

            #region Deal with subsets

            List<int> SubsetIDs = new List<int>();
            foreach (var i in ParticleSubset)
                if (!SubsetIDs.Contains(i))
                    SubsetIDs.Add(i);
            SubsetIDs.Sort();

            // For each subset, create a list of its particle IDs
            Dictionary<int, List<int>> SubsetParticleIDs = SubsetIDs.ToDictionary(subsetID => subsetID, subsetID => new List<int>());
            for (int i = 0; i < ParticleSubset.Length; i++)
                SubsetParticleIDs[ParticleSubset[i]].Add(i);
            foreach (var list in SubsetParticleIDs.Values)
                list.Sort();

            // Note where each subset starts and ends in a unified, sorted (by subset) particle ID list
            Dictionary<int, Tuple<int, int>> SubsetRanges = new Dictionary<int, Tuple<int, int>>();
            {
                int Start = 0;
                foreach (var pair in SubsetParticleIDs)
                {
                    SubsetRanges.Add(pair.Key, new Tuple<int, int>(Start, Start + pair.Value.Count));
                    Start += pair.Value.Count;
                }
            }

            List<int> SubsetContinuousIDs = new List<int>();
            foreach (var pair in SubsetParticleIDs)
                SubsetContinuousIDs.AddRange(pair.Value);

            // Reorder particle information to match the order of SubsetContinuousIDs
            ParticleOrigins = SubsetContinuousIDs.Select(i => ParticleOrigins[i]).ToArray();
            ParticleOrigins2 = SubsetContinuousIDs.Select(i => ParticleOrigins2[i]).ToArray();
            ParticleAngles = SubsetContinuousIDs.Select(i => ParticleAngles[i]).ToArray();
            ParticleAngles2 = SubsetContinuousIDs.Select(i => ParticleAngles2[i]).ToArray();
            ParticleSubset = SubsetContinuousIDs.Select(i => ParticleSubset[i]).ToArray();

            #endregion

            if (GridMovementX.Dimensions.Elements() == 1)
            {
                int MaxSlice = SubsetRanges.Last().Value.Item2 > 100 ? 1 : 1;

                GridMovementX = new CubicGrid(new int3(MaxSlice, MaxSlice, NTilts));
                GridMovementY = new CubicGrid(new int3(MaxSlice, MaxSlice, NTilts));

                //GridLocalX = new CubicGrid(new int3(4, 4, 4));
                //GridLocalY = new CubicGrid(new int3(4, 4, 4));
                //GridLocalZ = new CubicGrid(new int3(4, 4, 4));

                GridAngleX = new CubicGrid(new int3(1, 1, NTilts));
                GridAngleY = new CubicGrid(new int3(1, 1, NTilts));
                GridAngleZ = new CubicGrid(new int3(1, 1, NTilts));
            }
            if (GridLocalX.Dimensions.Elements() == 1)
            {
                GridLocalX = new CubicGrid(new int3(4, 4, 4));
                GridLocalY = new CubicGrid(new int3(4, 4, 4));
                GridLocalZ = new CubicGrid(new int3(4, 4, 4));
            }
            //else
            //{
            //    GridMovementX = GridMovementX.Resize(new int3(4, 4, NTilts));
            //    GridMovementY = GridMovementY.Resize(new int3(4, 4, NTilts));
            //}

            int CoarseSize = (int)Math.Round(size * ((float)CTF.PixelSize * 2 / resolution)) / 2 * 2;
            int3 CoarseDims = new int3(CoarseSize, CoarseSize, 1);

            // Positions the particles were extracted at/shifted to, to calculate effectively needed shifts later
            float2[] ExtractedAt = new float2[NParticles * NTilts];

            // Extract images, mask and resize them, create CTFs
            Image ParticleImages = new Image(new int3(CoarseSize, CoarseSize, NParticles * NTilts), true, true);
            Image ParticleCTFs = new Image(new int3(CoarseSize, CoarseSize, NParticles * NTilts), true);
            Image ParticleWeights = null;
            Image ShiftFactors = null;

            #region Preflight
            float KeepBFac = GlobalBfactor;
            GlobalBfactor = 0;
            {
                Image CTFCoords = GetCTFCoords(CoarseSize, size);

                #region Precalculate vectors for shifts in Fourier space
                {
                    float2[] ShiftFactorsData = new float2[(CoarseSize / 2 + 1) * CoarseSize];
                    for (int y = 0; y < CoarseSize; y++)
                        for (int x = 0; x < CoarseSize / 2 + 1; x++)
                        {
                            int xx = x;
                            int yy = y < CoarseSize / 2 + 1 ? y : y - CoarseSize;

                            ShiftFactorsData[y * (CoarseSize / 2 + 1) + x] = new float2((float)-xx / size * 2f * (float)Math.PI,
                                                                                          (float)-yy / size * 2f * (float)Math.PI);
                        }

                    ShiftFactors = new Image(ShiftFactorsData, new int3(CoarseSize, CoarseSize, 1), true);
                }
                #endregion

                #region Create mask with soft edge
                Image Mask;
                Image MaskSubt;
                {
                    Image MaskBig = new Image(new int3(size, size, 1));
                    float MaskRadius = MainWindow.Options.ExportParticleRadius / (float)CTF.PixelSize;
                    float SoftEdge = 16f;

                    float[] MaskBigData = MaskBig.GetHost(Intent.Write)[0];
                    for (int y = 0; y < size; y++)
                    {
                        int yy = y - size / 2;
                        yy *= yy;
                        for (int x = 0; x < size; x++)
                        {
                            int xx = x - size / 2;
                            xx *= xx;
                            float R = (float)Math.Sqrt(xx + yy);

                            if (R <= MaskRadius)
                                MaskBigData[y * size + x] = 1;
                            else
                                MaskBigData[y * size + x] = (float)(Math.Cos(Math.Min(1, (R - MaskRadius) / SoftEdge) * Math.PI) * 0.5 + 0.5);
                        }
                    }
                    //MaskBig.WriteMRC("d_maskbig.mrc");

                    Mask = MaskBig.AsScaled(new int2(CoarseSize, CoarseSize));
                    Mask.RemapToFT();

                    MaskBigData = MaskBig.GetHost(Intent.Write)[0];
                    for (int y = 0; y < size; y++)
                    {
                        int yy = y - size / 2;
                        yy *= yy;
                        for (int x = 0; x < size; x++)
                        {
                            int xx = x - size / 2;
                            xx *= xx;
                            float R = (float)Math.Sqrt(xx + yy);

                            if (R <= 30)
                                MaskBigData[y * size + x] = 1;
                            else
                                MaskBigData[y * size + x] = 0;
                        }
                    }

                    MaskSubt = MaskBig.AsScaled(new int2(CoarseSize, CoarseSize));
                    MaskSubt.RemapToFT();

                    MaskBig.Dispose();
                }
                //Mask.WriteMRC("d_masksmall.mrc");
                #endregion

                #region Create Fourier space mask
                Image FourierMask = new Image(CoarseDims, true);
                {
                    float[] FourierMaskData = FourierMask.GetHost(Intent.Write)[0];
                    int MaxR2 = CoarseSize * CoarseSize / 4;
                    for (int y = 0; y < CoarseSize; y++)
                    {
                        int yy = y < CoarseSize / 2 + 1 ? y : y - CoarseSize;
                        yy *= yy;

                        for (int x = 0; x < CoarseSize / 2 + 1; x++)
                        {
                            int xx = x * x;
                            int R2 = yy + xx;

                            FourierMaskData[y * (CoarseSize / 2 + 1) + x] = R2 < MaxR2 ? 1 : 0;
                        }
                    }
                }
                #endregion

                #region For each particle, create CTFs and extract & preprocess images for entire tilt series
                for (int p = 0; p < NParticles; p++)
                {
                    float3 ParticleCoords = ParticleOrigins[p];
                    float3[] Positions = GetPositionInImages(ParticleCoords);
                    float3[] ProjAngles = GetParticleAngleInImages(ParticleCoords, ParticleAngles[p]);

                    Image Extracted = new Image(new int3(size, size, NTilts));
                    float[][] ExtractedData = Extracted.GetHost(Intent.Write);
                    float3[] Residuals = new float3[NTilts];

                    Image SubtrahendsCTF = new Image(new int3(CoarseSize, CoarseSize, NTilts), true);

                    // Create CTFs
                    {
                        CTFStruct[] CTFParams = new CTFStruct[NTilts];

                        float GridStep = 1f / (NTilts - 1);
                        CTFStruct[] Params = new CTFStruct[NTilts];
                        for (int t = 0; t < NTilts; t++)
                        {
                            decimal Defocus = (decimal)Positions[t].Z;
                            decimal DefocusDelta = (decimal)GridCTFDefocusDelta.GetInterpolated(new float3(0.5f, 0.5f, t * GridStep));
                            decimal DefocusAngle = (decimal)GridCTFDefocusAngle.GetInterpolated(new float3(0.5f, 0.5f, t * GridStep));

                            CTF CurrCTF = CTF.GetCopy();
                            CurrCTF.Defocus = Defocus;
                            CurrCTF.DefocusDelta = DefocusDelta;
                            CurrCTF.DefocusAngle = DefocusAngle;
                            CurrCTF.Scale = (decimal)Math.Cos(Angles[t] * Helper.ToRad);
                            CurrCTF.Bfactor = (decimal)-Dose[t] * 8;

                            Params[t] = CurrCTF.ToStruct();
                        }

                        GPU.CreateCTF(ParticleCTFs.GetDeviceSlice(NTilts * p, Intent.Write),
                                      CTFCoords.GetDevice(Intent.Read),
                                      (uint)CoarseDims.ElementsFFT(),
                                      Params,
                                      false,
                                      (uint)NTilts);
                    }
                    //{
                    //    CTFStruct[] CTFParams = new CTFStruct[NTilts];

                    //    float GridStep = 1f / (NTilts - 1);
                    //    CTFStruct[] Params = new CTFStruct[NTilts];
                    //    for (int t = 0; t < NTilts; t++)
                    //    {
                    //        decimal Defocus = (decimal)Positions[t].Z;
                    //        decimal DefocusDelta = (decimal)GridCTFDefocusDelta.GetInterpolated(new float3(0.5f, 0.5f, t * GridStep));
                    //        decimal DefocusAngle = (decimal)GridCTFDefocusAngle.GetInterpolated(new float3(0.5f, 0.5f, t * GridStep));

                    //        CTF CurrCTF = CTF.GetCopy();
                    //        CurrCTF.Defocus = Defocus;
                    //        CurrCTF.DefocusDelta = DefocusDelta;
                    //        CurrCTF.DefocusAngle = DefocusAngle;
                    //        CurrCTF.Scale = 1;
                    //        CurrCTF.Bfactor = 0;

                    //        Params[t] = CurrCTF.ToStruct();
                    //    }

                    //    GPU.CreateCTF(SubtrahendsCTF.GetDevice(Intent.Write),
                    //                  CTFCoords.GetDevice(Intent.Read),
                    //                  (uint)CoarseDims.ElementsFFT(),
                    //                  Params,
                    //                  false,
                    //                  (uint)NTilts);
                    //}

                    // Extract images
                    {
                        for (int t = 0; t < NTilts; t++)
                        {
                            ExtractedAt[p * NTilts + t] = new float2(Positions[t].X, Positions[t].Y);

                            Positions[t] -= size / 2;
                            int2 IntPosition = new int2((int)Positions[t].X, (int)Positions[t].Y);
                            float2 Residual = new float2(-(Positions[t].X - IntPosition.X), -(Positions[t].Y - IntPosition.Y));
                            Residuals[t] = new float3(Residual / size * CoarseSize);

                            float[] OriginalData;
                            lock (tiltStack)
                                OriginalData = tiltStack.GetHost(Intent.Read)[t];

                            float[] ImageData = ExtractedData[t];
                            for (int y = 0; y < size; y++)
                            {
                                int PosY = (y + IntPosition.Y + tiltStack.Dims.Y) % tiltStack.Dims.Y;
                                for (int x = 0; x < size; x++)
                                {
                                    int PosX = (x + IntPosition.X + tiltStack.Dims.X) % tiltStack.Dims.X;
                                    ImageData[y * size + x] = OriginalData[PosY * tiltStack.Dims.X + PosX];
                                }
                            }
                        }

                        GPU.NormParticles(Extracted.GetDevice(Intent.Read),
                                          Extracted.GetDevice(Intent.Write),
                                          new int3(size, size, 1),
                                          (uint)(MainWindow.Options.ExportParticleRadius / CTF.PixelSize),
                                          true,
                                          (uint)NTilts);

                        Image Scaled = Extracted.AsScaled(new int2(CoarseSize, CoarseSize));
                        //Scaled.WriteMRC("d_scaled.mrc");
                        Extracted.Dispose();

                        Scaled.ShiftSlices(Residuals);
                        Scaled.RemapToFT();

                        //GPU.NormalizeMasked(Scaled.GetDevice(Intent.Read),
                        //              Scaled.GetDevice(Intent.Write),
                        //              MaskSubt.GetDevice(Intent.Read),
                        //              (uint)Scaled.ElementsSliceReal,
                        //              (uint)NTilts);

                        //{
                        //    //Image SubtrahendsFT = subtrahendReference.Project(new int2(CoarseSize, CoarseSize), ProjAngles, CoarseSize / 2);
                        //    //SubtrahendsFT.Multiply(SubtrahendsCTF);

                        //    //Image Subtrahends = SubtrahendsFT.AsIFFT();
                        //    //SubtrahendsFT.Dispose();

                        //    ////GPU.NormalizeMasked(Subtrahends.GetDevice(Intent.Read),
                        //    ////                    Subtrahends.GetDevice(Intent.Write),
                        //    ////                    MaskSubt.GetDevice(Intent.Read),
                        //    ////                    (uint)Subtrahends.ElementsSliceReal,
                        //    ////                    (uint)NTilts);

                        //    //Scaled.Subtract(Subtrahends);
                        //    //Subtrahends.Dispose();

                        //    Image FocusMaskFT = maskReference.Project(new int2(CoarseSize, CoarseSize), ProjAngles, CoarseSize / 2);
                        //    Image FocusMask = FocusMaskFT.AsIFFT();
                        //    FocusMaskFT.Dispose();

                        //    Scaled.Multiply(FocusMask);
                        //    FocusMask.Dispose();
                        //}

                        Scaled.MultiplySlices(Mask);

                        GPU.FFT(Scaled.GetDevice(Intent.Read),
                                ParticleImages.GetDeviceSlice(p * NTilts, Intent.Write),
                                CoarseDims,
                                (uint)NTilts);

                        Scaled.Dispose();
                        SubtrahendsCTF.Dispose();
                    }
                }
                #endregion

                ParticleCTFs.MultiplySlices(FourierMask);

                Mask.Dispose();
                FourierMask.Dispose();
                MaskSubt.Dispose();

                Image ParticleCTFsAbs = new Image(ParticleCTFs.GetDevice(Intent.Read), ParticleCTFs.Dims, true);
                ParticleCTFsAbs.Abs();
                ParticleWeights = ParticleCTFsAbs.AsSum2D();
                ParticleCTFsAbs.Dispose();
                {
                    float[] ParticleWeightsData = ParticleWeights.GetHost(Intent.ReadWrite)[0];
                    float Max = MathHelper.Max(ParticleWeightsData);
                    for (int i = 0; i < ParticleWeightsData.Length; i++)
                        ParticleWeightsData[i] /= Max;
                }

                CTFCoords.Dispose();

                //Image CheckImages = ParticleImages.AsIFFT();
                //CheckImages.WriteMRC("d_particleimages.mrc");
                //CheckImages.Dispose();

                //ParticleCTFs.WriteMRC("d_particlectfs.mrc");
            }
            GlobalBfactor = KeepBFac;
            #endregion

            bool DoPerParticleMotion = true;
            bool DoImageAlignment = true;

            #region BFGS evaluation and gradient

            double[] StartParams;

            Func<double[], Tuple<float2[], float3[]>> GetImageShiftsAndAngles;
            Func<double[], float2[]> GetImageShifts;
            Func<float3[], Image> GetProjections;
            Func<double[], double[]> EvalIndividual;
            Func<double[], double> Eval;
            Func<double[], double[]> Gradient;
            {
                List<double> StartParamsList = new List<double>();
                StartParamsList.AddRange(CreateVectorFromGrids(Dimensions.X));
                StartParamsList.AddRange(CreateVectorFromParameters(ParticleOrigins, ParticleOrigins2, ParticleAngles, ParticleAngles2, size));
                StartParams = StartParamsList.ToArray();

                // Remember where the values for each grid are stored in the optimized vector
                List<Tuple<int, int>> VectorGridRanges = new List<Tuple<int, int>>();
                List<int> GridElements = new List<int>();
                List<int> GridSliceElements = new List<int>();
                {
                    int Start = 0;
                    VectorGridRanges.Add(new Tuple<int, int>(Start, Start + (int)GridMovementX.Dimensions.Elements()));
                    Start += (int)GridMovementX.Dimensions.Elements();
                    VectorGridRanges.Add(new Tuple<int, int>(Start, Start + (int)GridMovementY.Dimensions.Elements()));
                    Start += (int)GridMovementY.Dimensions.Elements();

                    VectorGridRanges.Add(new Tuple<int, int>(Start, Start + (int)GridAngleX.Dimensions.Elements()));
                    Start += (int)GridAngleX.Dimensions.Elements();
                    VectorGridRanges.Add(new Tuple<int, int>(Start, Start + (int)GridAngleY.Dimensions.Elements()));
                    Start += (int)GridAngleY.Dimensions.Elements();
                    VectorGridRanges.Add(new Tuple<int, int>(Start, Start + (int)GridAngleZ.Dimensions.Elements()));
                    Start += (int)GridAngleZ.Dimensions.Elements();

                    VectorGridRanges.Add(new Tuple<int, int>(Start, Start + (int)GridLocalX.Dimensions.Elements()));
                    Start += (int)GridLocalX.Dimensions.Elements();
                    VectorGridRanges.Add(new Tuple<int, int>(Start, Start + (int)GridLocalY.Dimensions.Elements()));
                    Start += (int)GridLocalY.Dimensions.Elements();
                    VectorGridRanges.Add(new Tuple<int, int>(Start, Start + (int)GridLocalZ.Dimensions.Elements()));

                    GridElements.Add((int)GridMovementX.Dimensions.Elements());
                    GridElements.Add((int)GridMovementY.Dimensions.Elements());

                    GridElements.Add((int)GridAngleX.Dimensions.Elements());
                    GridElements.Add((int)GridAngleY.Dimensions.Elements());
                    GridElements.Add((int)GridAngleZ.Dimensions.Elements());

                    GridElements.Add((int)GridLocalX.Dimensions.Elements());
                    GridElements.Add((int)GridLocalY.Dimensions.Elements());
                    GridElements.Add((int)GridLocalZ.Dimensions.Elements());

                    GridSliceElements.Add((int)GridMovementX.Dimensions.ElementsSlice());
                    GridSliceElements.Add((int)GridMovementY.Dimensions.ElementsSlice());

                    GridSliceElements.Add((int)GridAngleX.Dimensions.ElementsSlice());
                    GridSliceElements.Add((int)GridAngleY.Dimensions.ElementsSlice());
                    GridSliceElements.Add((int)GridAngleZ.Dimensions.ElementsSlice());

                    GridSliceElements.Add((int)GridLocalX.Dimensions.ElementsSlice());
                    GridSliceElements.Add((int)GridLocalY.Dimensions.ElementsSlice());
                    GridSliceElements.Add((int)GridLocalZ.Dimensions.ElementsSlice());
                }
                int NVectorGridParams = VectorGridRanges.Last().Item2;
                int NVectorParticleParams = NParticles * 12;

                GetImageShiftsAndAngles = input =>
                {
                    // Retrieve particle positions & angles, and grids from input vector
                    float3[] NewPositions, NewPositions2, NewAngles, NewAngles2;
                    GetParametersFromVector(input, NParticles, size, out NewPositions, out NewPositions2, out NewAngles, out NewAngles2);
                    SetGridsFromVector(input, Dimensions.X);

                    // Using current positions, angles and grids, get parameters for image shifts and reference projection angles
                    float2[] ImageShifts = new float2[NParticles * NTilts];
                    float3[] ImageAngles = new float3[NParticles * NTilts];
                    float3[] PerTiltPositions = new float3[NParticles * NTilts];
                    float3[] PerTiltAngles = new float3[NParticles * NTilts];
                    int[] SortedDosePrecalc = IndicesSortedDose;
                    for (int p = 0; p < NParticles; p++)
                    {
                        if (DoPerParticleMotion)
                        {
                            float3 CoordsDiff = NewPositions2[p] - NewPositions[p];
                            float3 AnglesDiff = NewAngles2[p] - NewAngles[p];
                            for (int t = 0; t < NTilts; t++)
                            {
                                float DoseID = SortedDosePrecalc[t] / (float)(NTilts - 1);
                                PerTiltPositions[p * NTilts + t] = NewPositions[p] + CoordsDiff * DoseID;
                                PerTiltAngles[p * NTilts + t] = NewAngles[p] + AnglesDiff * DoseID;
                            }
                        }
                        else
                        {
                            for (int t = 0; t < NTilts; t++)
                            {
                                PerTiltPositions[p * NTilts + t] = NewPositions[p];
                                PerTiltAngles[p * NTilts + t] = NewAngles[p];
                            }
                        }
                    }

                    float3[] CurrPositions = GetPositionInImages(PerTiltPositions);
                    float3[] CurrAngles = GetParticleAngleInImages(PerTiltPositions, PerTiltAngles);
                    for (int i = 0; i < ImageShifts.Length; i++)
                    {
                        ImageShifts[i] = new float2(ExtractedAt[i].X - CurrPositions[i].X,
                                                    ExtractedAt[i].Y - CurrPositions[i].Y); // -diff because those are extraction positions, i. e. opposite direction of shifts
                        ImageAngles[i] = CurrAngles[i];
                    }

                    return new Tuple<float2[], float3[]>(ImageShifts, ImageAngles);
                };

                GetImageShifts = input =>
                {
                    // Retrieve particle positions & angles, and grids from input vector
                    float3[] NewPositions, NewPositions2, NewAngles, NewAngles2;
                    GetParametersFromVector(input, NParticles, size, out NewPositions, out NewPositions2, out NewAngles, out NewAngles2);
                    SetGridsFromVector(input, Dimensions.X);

                    // Using current positions, angles and grids, get parameters for image shifts and reference projection angles
                    float2[] ImageShifts = new float2[NParticles * NTilts];
                    float3[] PerTiltPositions = new float3[NParticles * NTilts];
                    int[] SortedDosePrecalc = IndicesSortedDose;
                    for (int p = 0; p < NParticles; p++)
                    {
                        if (DoPerParticleMotion)
                        {
                            float3 CoordsDiff = NewPositions2[p] - NewPositions[p];
                            float3 AnglesDiff = NewAngles2[p] - NewAngles[p];
                            for (int t = 0; t < NTilts; t++)
                            {
                                float DoseID = SortedDosePrecalc[t] / (float)(NTilts - 1);
                                PerTiltPositions[p * NTilts + t] = NewPositions[p] + CoordsDiff * DoseID;
                            }
                        }
                        else
                        {
                            for (int t = 0; t < NTilts; t++)
                                PerTiltPositions[p * NTilts + t] = NewPositions[p];
                        }
                    }

                    float3[] CurrPositions = GetPositionInImages(PerTiltPositions);
                    for (int i = 0; i < ImageShifts.Length; i++)
                        ImageShifts[i] = new float2(ExtractedAt[i].X - CurrPositions[i].X,
                                                    ExtractedAt[i].Y - CurrPositions[i].Y); // -diff because those are extraction positions, i. e. opposite direction of shifts

                    return ImageShifts;
                };

                GetProjections = imageAngles =>
                {
                    Image Projections = new Image(IntPtr.Zero, new int3(CoarseSize, CoarseSize, NParticles * NTilts), true, true);
                    foreach (var subset in SubsetRanges)
                    {
                        Projector Reference = references[subset.Key];
                        int SubsetStart = subset.Value.Item1 * NTilts;
                        int SubsetEnd = subset.Value.Item2 * NTilts;
                        float3[] SubsetAngles = imageAngles.Skip(SubsetStart).Take(SubsetEnd - SubsetStart).ToArray();

                        GPU.ProjectForward(Reference.Data.GetDevice(Intent.Read),
                                           Projections.GetDeviceSlice(SubsetStart, Intent.Write),
                                           Reference.Data.Dims,
                                           new int2(CoarseSize, CoarseSize),
                                           Helper.ToInterleaved(SubsetAngles),
                                           Reference.Oversampling,
                                           (uint)(SubsetEnd - SubsetStart));
                    }

                    /*Image CheckProjections = Projections.AsIFFT();
                    //CheckProjections.RemapFromFT();
                    CheckProjections.WriteMRC("d_projections.mrc");
                    CheckProjections.Dispose();*/

                    return Projections;
                };

                EvalIndividual = input =>
                {
                    Tuple<float2[], float3[]> ShiftsAndAngles = GetImageShiftsAndAngles(input);

                    Image Projections = GetProjections(ShiftsAndAngles.Item2);

                    float[] Results = new float[NParticles * NTilts];

                    GPU.TomoRefineGetDiff(ParticleImages.GetDevice(Intent.Read),
                                          Projections.GetDevice(Intent.Read),
                                          ShiftFactors.GetDevice(Intent.Read),
                                          ParticleCTFs.GetDevice(Intent.Read),
                                          ParticleWeights.GetDevice(Intent.Read),
                                          new int2(CoarseSize, CoarseSize),
                                          Helper.ToInterleaved(ShiftsAndAngles.Item1),
                                          Results,
                                          (uint)(NParticles * NTilts));

                    Projections.Dispose();

                    return Results.Select(i => (double)i).ToArray();
                };

                int OptimizationIterations = 0;
                bool GetOut = false;

                double Delta = 0.1;
                float Delta2 = 2 * (float)Delta;

                int[] WarpGridIDs = { 5, 6, 7 };
                Dictionary<int, float2[][]> WiggleWeightsWarp = new Dictionary<int, float2[][]>();
                foreach (var gridID in WarpGridIDs)
                {
                    int NElements = GridElements[gridID];
                    WiggleWeightsWarp.Add(gridID, new float2[NElements][]);

                    for (int ge = 0; ge < NElements; ge++)
                    {
                        double[] InputMinus = new double[StartParams.Length], InputPlus = new double[StartParams.Length];
                        for (int i = 0; i < StartParams.Length; i++)
                        {
                            InputMinus[i] = StartParams[i];
                            InputPlus[i] = StartParams[i];
                        }

                        InputMinus[VectorGridRanges[gridID].Item1 + ge] -= Delta;
                        InputPlus[VectorGridRanges[gridID].Item1 + ge] += Delta;

                        float2[] ImageShiftsPlus = GetImageShifts(InputPlus);
                        float2[] ImageShiftsMinus = GetImageShifts(InputMinus);

                        float2[] Weights = new float2[ImageShiftsPlus.Length];

                        for (int i = 0; i < ImageShiftsPlus.Length; i++)
                            Weights[i] = (ImageShiftsPlus[i] - ImageShiftsMinus[i]) / Delta2;

                        WiggleWeightsWarp[gridID][ge] = Weights;
                    }
                }

                Eval = input =>
                {
                    double Result = EvalIndividual(input).Sum();
                    lock (tableIn)
                        Debug.WriteLine(GPU.GetDevice() + ", " + RootName + ": " + Result);
                    OptimizationIterations++;

                    return Result;
                };

                Func<double[], double[], double, double[]> GradientParticles = (inputMinus, inputPlus, delta) =>
                {
                    double[] EvalMinus = EvalIndividual(inputMinus);
                    double[] EvalPlus = EvalIndividual(inputPlus);

                    double[] Diff = new double[EvalMinus.Length];
                    for (int i = 0; i < Diff.Length; i++)
                        Diff[i] = (EvalPlus[i] - EvalMinus[i]) / (2 * delta);

                    return Diff;
                };

                Gradient = input =>
                {
                    double[] Result = new double[input.Length];

                    if (OptimizationIterations > 60)
                        return Result;

                    float2[] ImageShiftGradients = new float2[NParticles * NTilts];
                    #region Compute gradient for individual image shifts
                    {
                        Tuple<float2[], float3[]> ShiftsAndAngles = GetImageShiftsAndAngles(input);
                        Image Projections = GetProjections(ShiftsAndAngles.Item2);

                        float2[] ShiftsXPlus = new float2[NParticles * NTilts];
                        float2[] ShiftsXMinus = new float2[NParticles * NTilts];
                        float2[] ShiftsYPlus = new float2[NParticles * NTilts];
                        float2[] ShiftsYMinus = new float2[NParticles * NTilts];

                        float2 DeltaX = new float2((float)Delta, 0);
                        float2 DeltaY = new float2(0, (float)Delta);

                        for (int i = 0; i < ShiftsXPlus.Length; i++)
                        {
                            ShiftsXPlus[i] = ShiftsAndAngles.Item1[i] + DeltaX;
                            ShiftsXMinus[i] = ShiftsAndAngles.Item1[i] - DeltaX;

                            ShiftsYPlus[i] = ShiftsAndAngles.Item1[i] + DeltaY;
                            ShiftsYMinus[i] = ShiftsAndAngles.Item1[i] - DeltaY;
                        }

                        float[] ScoresXPlus = new float[NParticles * NTilts];
                        float[] ScoresXMinus = new float[NParticles * NTilts];
                        float[] ScoresYPlus = new float[NParticles * NTilts];
                        float[] ScoresYMinus = new float[NParticles * NTilts];

                        GPU.TomoRefineGetDiff(ParticleImages.GetDevice(Intent.Read),
                                              Projections.GetDevice(Intent.Read),
                                              ShiftFactors.GetDevice(Intent.Read),
                                              ParticleCTFs.GetDevice(Intent.Read),
                                              ParticleWeights.GetDevice(Intent.Read),
                                              new int2(CoarseSize, CoarseSize),
                                              Helper.ToInterleaved(ShiftsXPlus),
                                              ScoresXPlus,
                                              (uint)(NParticles * NTilts));
                        GPU.TomoRefineGetDiff(ParticleImages.GetDevice(Intent.Read),
                                              Projections.GetDevice(Intent.Read),
                                              ShiftFactors.GetDevice(Intent.Read),
                                              ParticleCTFs.GetDevice(Intent.Read),
                                              ParticleWeights.GetDevice(Intent.Read),
                                              new int2(CoarseSize, CoarseSize),
                                              Helper.ToInterleaved(ShiftsXMinus),
                                              ScoresXMinus,
                                              (uint)(NParticles * NTilts));
                        GPU.TomoRefineGetDiff(ParticleImages.GetDevice(Intent.Read),
                                              Projections.GetDevice(Intent.Read),
                                              ShiftFactors.GetDevice(Intent.Read),
                                              ParticleCTFs.GetDevice(Intent.Read),
                                              ParticleWeights.GetDevice(Intent.Read),
                                              new int2(CoarseSize, CoarseSize),
                                              Helper.ToInterleaved(ShiftsYPlus),
                                              ScoresYPlus,
                                              (uint)(NParticles * NTilts));
                        GPU.TomoRefineGetDiff(ParticleImages.GetDevice(Intent.Read),
                                              Projections.GetDevice(Intent.Read),
                                              ShiftFactors.GetDevice(Intent.Read),
                                              ParticleCTFs.GetDevice(Intent.Read),
                                              ParticleWeights.GetDevice(Intent.Read),
                                              new int2(CoarseSize, CoarseSize),
                                              Helper.ToInterleaved(ShiftsYMinus),
                                              ScoresYMinus,
                                              (uint)(NParticles * NTilts));

                        Projections.Dispose();

                        for (int i = 0; i < ImageShiftGradients.Length; i++)
                        {
                            ImageShiftGradients[i] = new float2((ScoresXPlus[i] - ScoresXMinus[i]) / Delta2,
                                                           (ScoresYPlus[i] - ScoresYMinus[i]) / Delta2);
                        }
                    }
                    #endregion

                    // First, do particle parameters, i. e. 3D position within tomogram, rotation, across 2 points in time
                    // Altering each particle's parameters results in a change in its NTilts images, but nothing else
                    {
                        int[] TranslationIDs = DoPerParticleMotion ? new[] { 0, 1, 2, 3, 4, 5 } : new[] { 0, 1, 2 };
                        int[] RotationIDs = DoPerParticleMotion ? new [] {6, 7, 8, 9, 10, 11} : new [] { 6, 7, 8};
                        foreach (var paramID in RotationIDs)
                        {
                            double[] InputMinus = new double[input.Length], InputPlus = new double[input.Length];
                            for (int i = 0; i < input.Length; i++)
                            {
                                InputMinus[i] = input[i];
                                InputPlus[i] = input[i];
                            }
                            for (int p = 0; p < NParticles; p++)
                            {
                                InputMinus[NVectorGridParams + p * 12 + paramID] -= Delta;
                                InputPlus[NVectorGridParams + p * 12 + paramID] += Delta;
                            }

                            double[] ResultParticles = GradientParticles(InputMinus, InputPlus, Delta);
                            for (int p = 0; p < NParticles; p++)
                            {
                                double ParticleSum = 0;
                                for (int t = 0; t < NTilts; t++)
                                    ParticleSum += ResultParticles[p * NTilts + t];

                                Result[NVectorGridParams + p * 12 + paramID] = ParticleSum;
                            }
                        }

                        // Translation-related gradients can all be computed efficiently from previously retrieved per-image gradients
                        foreach (var paramID in TranslationIDs)
                        {
                            double[] InputMinus = new double[input.Length], InputPlus = new double[input.Length];
                            for (int i = 0; i < input.Length; i++)
                            {
                                InputMinus[i] = input[i];
                                InputPlus[i] = input[i];
                            }
                            for (int p = 0; p < NParticles; p++)
                            {
                                InputMinus[NVectorGridParams + p * 12 + paramID] -= Delta;
                                InputPlus[NVectorGridParams + p * 12 + paramID] += Delta;
                            }

                            float2[] ImageShiftsPlus = GetImageShifts(InputPlus);
                            float2[] ImageShiftsMinus = GetImageShifts(InputMinus);

                            for (int p = 0; p < NParticles; p++)
                            {
                                double ParticleSum = 0;
                                for (int t = 0; t < NTilts; t++)
                                {
                                    int i = p * NTilts + t;
                                    float2 ShiftDelta = (ImageShiftsPlus[i] - ImageShiftsMinus[i]) / Delta2;
                                    float ShiftGradient = ShiftDelta.X * ImageShiftGradients[i].X + ShiftDelta.Y * ImageShiftGradients[i].Y;

                                    ParticleSum += ShiftGradient;
                                }

                                Result[NVectorGridParams + p * 12 + paramID] = ParticleSum;
                            }
                        }

                        // If there is no per-particle motion, just copy the gradients for these parameters from parameterIDs 0-5
                        if (!DoPerParticleMotion)
                        {
                            int[] RedundantIDs = { 3, 4, 5, 9, 10, 11 };
                            foreach (var paramID in RedundantIDs)
                                for (int p = 0; p < NParticles; p++)
                                    Result[NVectorGridParams + p * 12 + paramID] = Result[NVectorGridParams + p * 12 + paramID - 3];
                        }
                    }

                    // Now deal with grids. Each grid slice (i. e. temporal point) will correspond to one tilt only, thus the gradient
                    // for each slice is the (weighted, in case of spatial resolution) sum of NParticles images in the corresponding tilt.
                    if (DoImageAlignment)
                    {
                        int[] RotationGridIDs = { 2, 3, 4 };
                        foreach (var gridID in RotationGridIDs)
                        {
                            int SliceElements = GridSliceElements[gridID];

                            for (int se = 0; se < SliceElements; se++)
                            {
                                double[] InputMinus = new double[input.Length], InputPlus = new double[input.Length];
                                for (int i = 0; i < input.Length; i++)
                                {
                                    InputMinus[i] = input[i];
                                    InputPlus[i] = input[i];
                                }
                                for (int gp = VectorGridRanges[gridID].Item1 + se; gp < VectorGridRanges[gridID].Item2; gp += SliceElements)
                                {
                                    InputMinus[gp] -= Delta;
                                    InputPlus[gp] += Delta;
                                }

                                double[] ResultParticles = GradientParticles(InputMinus, InputPlus, Delta);
                                for (int i = 0; i < ResultParticles.Length; i++)
                                {
                                    int GridTime = i % NTilts;
                                    Result[VectorGridRanges[gridID].Item1 + GridTime * SliceElements + se] += ResultParticles[i];
                                }
                            }
                        }

                        // Translation-related gradients can all be computed efficiently from previously retrieved per-image gradients
                        int[] TranslationGridIDs = { 0, 1 };
                        foreach (var gridID in TranslationGridIDs)
                        {
                            int SliceElements = GridSliceElements[gridID];

                            for (int se = 0; se < SliceElements; se++)
                            {
                                double[] InputMinus = new double[input.Length], InputPlus = new double[input.Length];
                                for (int i = 0; i < input.Length; i++)
                                {
                                    InputMinus[i] = input[i];
                                    InputPlus[i] = input[i];
                                }
                                for (int gp = VectorGridRanges[gridID].Item1 + se; gp < VectorGridRanges[gridID].Item2; gp += SliceElements)
                                {
                                    InputMinus[gp] -= Delta;
                                    InputPlus[gp] += Delta;
                                }

                                float2[] ImageShiftsPlus = GetImageShifts(InputPlus);
                                float2[] ImageShiftsMinus = GetImageShifts(InputMinus);

                                for (int i = 0; i < ImageShiftsPlus.Length; i++)
                                {
                                    float2 ShiftDelta = (ImageShiftsPlus[i] - ImageShiftsMinus[i]) / Delta2;
                                    float ShiftGradient = ShiftDelta.X * ImageShiftGradients[i].X + ShiftDelta.Y * ImageShiftGradients[i].Y;

                                    int GridSlice = i % NTilts;
                                    Result[VectorGridRanges[gridID].Item1 + GridSlice * SliceElements + se] += ShiftGradient;
                                }
                            }
                        }
                        // Warp grids don't have any shortcuts for getting multiple gradients at once, so they use pre-calculated wiggle weights
                        foreach (var gridID in WarpGridIDs)
                        {
                            int NElements = GridElements[gridID];

                            for (int ge = 0; ge < NElements; ge++)
                            {
                                float2[] Weights = WiggleWeightsWarp[gridID][ge];

                                for (int i = 0; i < Weights.Length; i++)
                                {
                                    float2 ShiftDelta = Weights[i];
                                    float ShiftGradient = ShiftDelta.X * ImageShiftGradients[i].X + ShiftDelta.Y * ImageShiftGradients[i].Y;
                                    
                                    Result[VectorGridRanges[gridID].Item1 + ge] += ShiftGradient;
                                }
                            }
                        }
                    }

                    return Result;
                };
            }

            #endregion

            BroydenFletcherGoldfarbShanno Optimizer = new BroydenFletcherGoldfarbShanno(StartParams.Length, Eval, Gradient);
            Optimizer.Epsilon = 3e-7;

            Optimizer.Maximize(StartParams);

            float3[] OptimizedOrigins, OptimizedOrigins2, OptimizedAngles, OptimizedAngles2;
            GetParametersFromVector(StartParams, NParticles, size, out OptimizedOrigins, out OptimizedOrigins2, out OptimizedAngles, out OptimizedAngles2);
            SetGridsFromVector(StartParams, Dimensions.X);

            #region Calculate correlation scores, update table with new positions and angles
            {
                double[] ImageScores = EvalIndividual(StartParams);
                float[] ParticleScores = new float[NParticles];
                for (int i = 0; i < ImageScores.Length; i++)
                    ParticleScores[i / NTilts] += (float)ImageScores[i];

                //if (!tableIn.HasColumn("rlnOriginXPrior"))
                //    tableIn.AddColumn("rlnOriginXPrior");
                //if (!tableIn.HasColumn("rlnOriginYPrior"))
                //    tableIn.AddColumn("rlnOriginYPrior");
                //if (!tableIn.HasColumn("rlnOriginZPrior"))
                //    tableIn.AddColumn("rlnOriginZPrior");

                //if (!tableIn.HasColumn("rlnAngleRotPrior"))
                //    tableIn.AddColumn("rlnAngleRotPrior");
                //if (!tableIn.HasColumn("rlnAngleTiltPrior"))
                //    tableIn.AddColumn("rlnAngleTiltPrior");
                //if (!tableIn.HasColumn("rlnAnglePsiPrior"))
                //    tableIn.AddColumn("rlnAnglePsiPrior");

                lock (tableIn)
                    for (int p = 0; p < NParticles; p++)
                    {
                        int Row = RowIndices[SubsetContinuousIDs[p]];

                        tableIn.SetRowValue(Row, "rlnCoordinateX", OptimizedOrigins[p].X.ToString(CultureInfo.InvariantCulture));
                        tableIn.SetRowValue(Row, "rlnCoordinateY", OptimizedOrigins[p].Y.ToString(CultureInfo.InvariantCulture));
                        tableIn.SetRowValue(Row, "rlnCoordinateZ", OptimizedOrigins[p].Z.ToString(CultureInfo.InvariantCulture));

                        //tableIn.SetRowValue(Row, "rlnOriginXPrior", OptimizedOrigins2[p].X.ToString(CultureInfo.InvariantCulture));
                        //tableIn.SetRowValue(Row, "rlnOriginYPrior", OptimizedOrigins2[p].Y.ToString(CultureInfo.InvariantCulture));
                        //tableIn.SetRowValue(Row, "rlnOriginZPrior", OptimizedOrigins2[p].Z.ToString(CultureInfo.InvariantCulture));

                        tableIn.SetRowValue(Row, "rlnAngleRot", OptimizedAngles[p].X.ToString(CultureInfo.InvariantCulture));
                        tableIn.SetRowValue(Row, "rlnAngleTilt", OptimizedAngles[p].Y.ToString(CultureInfo.InvariantCulture));
                        tableIn.SetRowValue(Row, "rlnAnglePsi", OptimizedAngles[p].Z.ToString(CultureInfo.InvariantCulture));

                        //tableIn.SetRowValue(Row, "rlnAngleRotPrior", OptimizedAngles2[p].X.ToString(CultureInfo.InvariantCulture));
                        //tableIn.SetRowValue(Row, "rlnAngleTiltPrior", OptimizedAngles2[p].Y.ToString(CultureInfo.InvariantCulture));
                        //tableIn.SetRowValue(Row, "rlnAnglePsiPrior", OptimizedAngles2[p].Z.ToString(CultureInfo.InvariantCulture));

                        tableIn.SetRowValue(Row, "rlnParticleSelectZScore", ParticleScores[p].ToString(CultureInfo.InvariantCulture));
                    }
            }
            #endregion

            ParticleImages?.Dispose();
            ParticleCTFs?.Dispose();
            ParticleWeights?.Dispose();
            ShiftFactors?.Dispose();

            #region Extract particles at full resolution and back-project them into the reconstruction volumes
            {
                GPU.SetDevice(0);

                Image CTFCoords = GetCTFCoords(size, size);
                int[] SortedDosePrecalc = IndicesSortedDose;

                foreach (var subsetRange in SubsetRanges)
                {
                    lock (outReconstructions[subsetRange.Key])
                    {
                        for (int p = subsetRange.Value.Item1; p < subsetRange.Value.Item2; p++)
                        {
                            float3[] PerTiltPositions = new float3[NTilts];
                            float3[] PerTiltAngles = new float3[NTilts];
                            float3 CoordsDiff = OptimizedOrigins2[p] - OptimizedOrigins[p];
                            float3 AnglesDiff = OptimizedAngles2[p] - OptimizedAngles[p];
                            for (int t = 0; t < NTilts; t++)
                            {
                                float DoseID = SortedDosePrecalc[t] / (float)(NTilts - 1);
                                PerTiltPositions[t] = OptimizedOrigins[p] + CoordsDiff * DoseID;
                                PerTiltAngles[t] = OptimizedAngles[p] + AnglesDiff * DoseID;
                            }

                            Image FullParticleImages = GetSubtomoImages(tiltStack, size, PerTiltPositions, true);
                            Image FullParticleCTFs = GetSubtomoCTFs(PerTiltPositions, CTFCoords);

                            FullParticleImages.Multiply(FullParticleCTFs);
                            FullParticleCTFs.Abs();

                            float3[] FullParticleAngles = GetParticleAngleInImages(PerTiltPositions, PerTiltAngles);

                            outReconstructions[subsetRange.Key].BackProject(FullParticleImages, FullParticleCTFs, FullParticleAngles);

                            FullParticleImages.Dispose();
                            FullParticleCTFs.Dispose();
                        }

                        for (int p = subsetRange.Value.Item1; p < subsetRange.Value.Item2; p++)
                        {
                            float3[] PerTiltPositions = new float3[NTilts];
                            float3[] PerTiltAngles = new float3[NTilts];
                            float3 CoordsDiff = OptimizedOrigins2[p] - OptimizedOrigins[p];
                            float3 AnglesDiff = OptimizedAngles2[p] - OptimizedAngles[p];
                            for (int t = 0; t < NTilts; t++)
                            {
                                float DoseID = SortedDosePrecalc[t] / (float)(NTilts - 1);
                                PerTiltPositions[t] = OptimizedOrigins[p] + CoordsDiff * DoseID;
                                PerTiltAngles[t] = OptimizedAngles[p] + AnglesDiff * DoseID;
                            }

                            float3[] FullParticleAngles = GetParticleAngleInImages(PerTiltPositions, PerTiltAngles);

                            Image FullParticleCTFs = GetSubtomoCTFs(PerTiltPositions, CTFCoords, false);
                            Image FullParticleCTFWeights = GetSubtomoCTFs(PerTiltPositions, CTFCoords, true);

                            // CTF has to be converted to complex numbers with imag = 0
                            float2[] CTFsComplexData = new float2[FullParticleCTFs.ElementsComplex];
                            float[] CTFWeightsData = new float[FullParticleCTFs.ElementsComplex];
                            float[] CTFsContinuousData = FullParticleCTFs.GetHostContinuousCopy();
                            float[] CTFWeightsContinuousData = FullParticleCTFWeights.GetHostContinuousCopy();
                            for (int i = 0; i < CTFsComplexData.Length; i++)
                            {
                                CTFsComplexData[i] = new float2(Math.Abs(CTFsContinuousData[i] * CTFWeightsContinuousData[i]), 0);
                                CTFWeightsData[i] = Math.Abs(CTFWeightsContinuousData[i]);
                            }

                            Image CTFsComplex = new Image(CTFsComplexData, FullParticleCTFs.Dims, true);
                            Image CTFWeights = new Image(CTFWeightsData, FullParticleCTFs.Dims, true);

                            outCTFReconstructions[subsetRange.Key].BackProject(CTFsComplex, CTFWeights, FullParticleAngles);

                            FullParticleCTFs.Dispose();
                            FullParticleCTFWeights.Dispose();
                            CTFsComplex.Dispose();
                            CTFWeights.Dispose();
                        }

                        outReconstructions[subsetRange.Key].FreeDevice();
                        outCTFReconstructions[subsetRange.Key].FreeDevice();
                    }
                }

                CTFCoords.Dispose();
            }
            #endregion

            SaveMeta();
        }

        private void SetGridsFromVector(double[] v, int sizeImage)
        {
            float AverageMoveImage = sizeImage / 180f;
            int Start = 0;

            GridMovementX = new CubicGrid(GridMovementX.Dimensions, v.Skip(Start).Take((int)GridMovementX.Dimensions.Elements()).Select(i => (float)i).ToArray());
            Start += (int)GridMovementX.Dimensions.Elements();

            GridMovementY = new CubicGrid(GridMovementY.Dimensions, v.Skip(Start).Take((int)GridMovementY.Dimensions.Elements()).Select(i => (float)i).ToArray());
            Start += (int)GridMovementY.Dimensions.Elements();

            GridAngleX = new CubicGrid(GridAngleX.Dimensions, v.Skip(Start).Take((int)GridAngleX.Dimensions.Elements()).Select(i => (float)i / AverageMoveImage).ToArray());
            Start += (int)GridAngleX.Dimensions.Elements();

            GridAngleY = new CubicGrid(GridAngleY.Dimensions, v.Skip(Start).Take((int)GridAngleY.Dimensions.Elements()).Select(i => (float)i / AverageMoveImage).ToArray());
            Start += (int)GridAngleY.Dimensions.Elements();

            GridAngleZ = new CubicGrid(GridAngleZ.Dimensions, v.Skip(Start).Take((int)GridAngleZ.Dimensions.Elements()).Select(i => (float)i / AverageMoveImage).ToArray());
            Start += (int)GridAngleZ.Dimensions.Elements();

            GridLocalX = new CubicGrid(GridLocalX.Dimensions, v.Skip(Start).Take((int)GridLocalX.Dimensions.Elements()).Select(i => (float)i).ToArray());
            Start += (int)GridLocalX.Dimensions.Elements();

            GridLocalY = new CubicGrid(GridLocalY.Dimensions, v.Skip(Start).Take((int)GridLocalY.Dimensions.Elements()).Select(i => (float)i).ToArray());
            Start += (int)GridLocalY.Dimensions.Elements();

            GridLocalZ = new CubicGrid(GridLocalZ.Dimensions, v.Skip(Start).Take((int)GridLocalZ.Dimensions.Elements()).Select(i => (float)i).ToArray());
            Start += (int)GridLocalZ.Dimensions.Elements();
        }

        private void GetParametersFromVector(double[] v, int nparticles, int sizeParticle, out float3[] particleShifts, out float3[] particleShifts2, out float3[] particleAngles, out float3[] particleAngles2)
        {
            float AverageMoveParticle = sizeParticle / 180f;
            int Start = 0;
            Start += (int)GridMovementX.Dimensions.Elements();
            Start += (int)GridMovementY.Dimensions.Elements();
            Start += (int)GridAngleX.Dimensions.Elements();
            Start += (int)GridAngleY.Dimensions.Elements();
            Start += (int)GridAngleZ.Dimensions.Elements();
            Start += (int)GridLocalX.Dimensions.Elements();
            Start += (int)GridLocalY.Dimensions.Elements();
            Start += (int)GridLocalZ.Dimensions.Elements();

            particleShifts = new float3[nparticles];
            particleShifts2 = new float3[nparticles];
            particleAngles = new float3[nparticles];
            particleAngles2 = new float3[nparticles];
            for (int n = 0; n < nparticles; n++)
            {
                particleShifts[n] = new float3((float)v[Start + n * 12 + 0], (float)v[Start + n * 12 + 1], (float)v[Start + n * 12 + 2]);
                particleShifts2[n] = new float3((float)v[Start + n * 12 + 3], (float)v[Start + n * 12 + 4], (float)v[Start + n * 12 + 5]);
                particleAngles[n] = new float3((float)v[Start + n * 12 + 6], (float)v[Start + n * 12 + 7], (float)v[Start + n * 12 + 8]) / AverageMoveParticle;
                particleAngles2[n] = new float3((float)v[Start + n * 12 + 9], (float)v[Start + n * 12 + 10], (float)v[Start + n * 12 + 11]) / AverageMoveParticle;
            }
        }

        private double[] CreateVectorFromGrids(int sizeImage)
        {
            float AverageMoveImage = sizeImage / 180f;
            List<float> V = new List<float>();

            V.AddRange(GridMovementX.FlatValues);
            V.AddRange(GridMovementY.FlatValues);

            V.AddRange(GridAngleX.FlatValues.Select(i => i * AverageMoveImage));
            V.AddRange(GridAngleY.FlatValues.Select(i => i * AverageMoveImage));
            V.AddRange(GridAngleZ.FlatValues.Select(i => i * AverageMoveImage));

            V.AddRange(GridLocalX.FlatValues);
            V.AddRange(GridLocalY.FlatValues);
            V.AddRange(GridLocalZ.FlatValues);

            return V.Select(i => (double)i).ToArray();
        }

        private double[] CreateVectorFromParameters(float3[] particleShifts, float3[] particleShifts2, float3[] particleAngles, float3[] particleAngles2, int sizeParticle)
        {
            float AverageMoveParticle = sizeParticle / 180f;
            double[] V = new double[particleShifts.Length * 12];

            for (int n = 0; n < particleShifts.Length; n++)
            {
                V[n * 12 + 0] = particleShifts[n].X;
                V[n * 12 + 1] = particleShifts[n].Y;
                V[n * 12 + 2] = particleShifts[n].Z;

                V[n * 12 + 3] = particleShifts2[n].X;
                V[n * 12 + 4] = particleShifts2[n].Y;
                V[n * 12 + 5] = particleShifts2[n].Z;

                V[n * 12 + 6] = particleAngles[n].X * AverageMoveParticle;
                V[n * 12 + 7] = particleAngles[n].Y * AverageMoveParticle;
                V[n * 12 + 8] = particleAngles[n].Z * AverageMoveParticle;

                V[n * 12 + 9] = particleAngles2[n].X * AverageMoveParticle;
                V[n * 12 + 10] = particleAngles2[n].Y * AverageMoveParticle;
                V[n * 12 + 11] = particleAngles2[n].Z * AverageMoveParticle;
            }

            return V;
        }

        public void PerformGlobalParticleAlignment(Star tableIn,
                                                   Image tiltStack,
                                                   int size,
                                                   int3 volumeDimensions,
                                                   Dictionary<int, Projector> references,
                                                   float resolution,
                                                   int healpixOrder,
                                                   string symmetry,
                                                   float offsetRange,
                                                   float offsetStep,
                                                   Dictionary<int, Projector> outReconstructions,
                                                   Dictionary<int, Projector> outCTFReconstructions)
        {
            VolumeDimensions = volumeDimensions;

            #region Get rows from table

            List<int> RowIndices = new List<int>();
            string[] ColumnMicrographName = tableIn.GetColumn("rlnMicrographName");
            for (int i = 0; i < ColumnMicrographName.Length; i++)
                if (ColumnMicrographName[i].Contains(RootName + "."))
                    RowIndices.Add(i);

            if (RowIndices.Count == 0)
                return;

            int NParticles = RowIndices.Count;

            #endregion

            #region Make sure all columns and directories are there

            if (!tableIn.HasColumn("rlnImageName"))
                tableIn.AddColumn("rlnImageName");
            if (!tableIn.HasColumn("rlnCtfImage"))
                tableIn.AddColumn("rlnCtfImage");
            if (!tableIn.HasColumn("rlnParticleSelectZScore"))
                tableIn.AddColumn("rlnParticleSelectZScore");

            if (!Directory.Exists(ParticlesDir))
                Directory.CreateDirectory(ParticlesDir);
            if (!Directory.Exists(ParticleCTFDir))
                Directory.CreateDirectory(ParticleCTFDir);

            #endregion

            #region Get subtomo positions from table

            float3[] ParticleOrigins = new float3[NParticles];
            float3[] ParticleOrigins2 = new float3[NParticles];
            float3[] ParticleAngles = new float3[NParticles];
            float3[] ParticleAngles2 = new float3[NParticles];
            int[] ParticleSubset = new int[NParticles];
            {
                string[] ColumnPosX = tableIn.GetColumn("rlnCoordinateX");
                string[] ColumnPosY = tableIn.GetColumn("rlnCoordinateY");
                string[] ColumnPosZ = tableIn.GetColumn("rlnCoordinateZ");
                string[] ColumnOriginX = tableIn.GetColumn("rlnOriginX");
                string[] ColumnOriginY = tableIn.GetColumn("rlnOriginY");
                string[] ColumnOriginZ = tableIn.GetColumn("rlnOriginZ");
                string[] ColumnAngleRot = tableIn.GetColumn("rlnAngleRot");
                string[] ColumnAngleTilt = tableIn.GetColumn("rlnAngleTilt");
                string[] ColumnAnglePsi = tableIn.GetColumn("rlnAnglePsi");
                string[] ColumnSubset = tableIn.GetColumn("rlnRandomSubset");

                for (int i = 0; i < NParticles; i++)
                {
                    float3 Pos = new float3(float.Parse(ColumnPosX[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosY[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosZ[RowIndices[i]], CultureInfo.InvariantCulture));
                    float3 Pos2 = Pos;

                    float3 Shift = new float3(float.Parse(ColumnOriginX[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginY[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginZ[RowIndices[i]], CultureInfo.InvariantCulture));

                    ParticleOrigins[i] = Pos - Shift;
                    ParticleOrigins2[i] = Pos2 - Shift;

                    float3 Angle = new float3(float.Parse(ColumnAngleRot[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnAngleTilt[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnAnglePsi[RowIndices[i]], CultureInfo.InvariantCulture));
                    float3 Angle2 = Angle;

                    ParticleAngles[i] = Angle;
                    ParticleAngles2[i] = Angle2;

                    ParticleSubset[i] = int.Parse(ColumnSubset[RowIndices[i]]);

                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateX", ParticleOrigins[i].X.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateY", ParticleOrigins[i].Y.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateZ", ParticleOrigins[i].Z.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginX", "0.0");
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginY", "0.0");
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginZ", "0.0");
                }
            }

            #endregion

            #region Deal with subsets

            List<int> SubsetIDs = new List<int>();
            foreach (var i in ParticleSubset)
                if (!SubsetIDs.Contains(i))
                    SubsetIDs.Add(i);
            SubsetIDs.Sort();

            // For each subset, create a list of its particle IDs
            Dictionary<int, List<int>> SubsetParticleIDs = SubsetIDs.ToDictionary(subsetID => subsetID, subsetID => new List<int>());
            for (int i = 0; i < ParticleSubset.Length; i++)
                SubsetParticleIDs[ParticleSubset[i]].Add(i);
            foreach (var list in SubsetParticleIDs.Values)
                list.Sort();

            // Note where each subset starts and ends in a unified, sorted (by subset) particle ID list
            Dictionary<int, Tuple<int, int>> SubsetRanges = new Dictionary<int, Tuple<int, int>>();
            {
                int Start = 0;
                foreach (var pair in SubsetParticleIDs)
                {
                    SubsetRanges.Add(pair.Key, new Tuple<int, int>(Start, Start + pair.Value.Count));
                    Start += pair.Value.Count;
                }
            }

            List<int> SubsetContinuousIDs = new List<int>();
            foreach (var pair in SubsetParticleIDs)
                SubsetContinuousIDs.AddRange(pair.Value);

            // Reorder particle information to match the order of SubsetContinuousIDs
            ParticleOrigins = SubsetContinuousIDs.Select(i => ParticleOrigins[i]).ToArray();
            ParticleOrigins2 = SubsetContinuousIDs.Select(i => ParticleOrigins2[i]).ToArray();
            ParticleAngles = SubsetContinuousIDs.Select(i => ParticleAngles[i]).ToArray();
            ParticleAngles2 = SubsetContinuousIDs.Select(i => ParticleAngles2[i]).ToArray();
            ParticleSubset = SubsetContinuousIDs.Select(i => ParticleSubset[i]).ToArray();

            #endregion

            int CoarseSize = (int)Math.Round(size * ((float)CTF.PixelSize * 2 / resolution)) / 2 * 2;
            int3 CoarseDims = new int3(CoarseSize, CoarseSize, 1);

            // Positions the particles were extracted at/shifted to, to calculate effectively needed shifts later
            float2[] ExtractedAt = new float2[NParticles * NTilts];

            // Extract images, mask and resize them, create CTFs
            Image ParticleImages = new Image(new int3(CoarseSize, CoarseSize, NParticles * NTilts), true, true);
            Image ParticleCTFs = new Image(new int3(CoarseSize, CoarseSize, NParticles * NTilts), true);
            Image ParticleWeights = null;
            Image ShiftFactors = null;

            #region Preflight

            float KeepBFac = GlobalBfactor;
            GlobalBfactor = 0;
            {
                Image CTFCoords = GetCTFCoords(CoarseSize, size);

                #region Precalculate vectors for shifts in Fourier space

                {
                    float2[] ShiftFactorsData = new float2[(CoarseSize / 2 + 1) * CoarseSize];
                    for (int y = 0; y < CoarseSize; y++)
                        for (int x = 0; x < CoarseSize / 2 + 1; x++)
                        {
                            int xx = x;
                            int yy = y < CoarseSize / 2 + 1 ? y : y - CoarseSize;

                            ShiftFactorsData[y * (CoarseSize / 2 + 1) + x] = new float2((float)-xx / size * 2f * (float)Math.PI,
                                                                                        (float)-yy / size * 2f * (float)Math.PI);
                        }

                    ShiftFactors = new Image(ShiftFactorsData, new int3(CoarseSize, CoarseSize, 1), true);
                }

                #endregion

                #region Create mask with soft edge

                Image Mask;
                Image MaskSubt;
                {
                    Image MaskBig = new Image(new int3(size, size, 1));
                    float MaskRadius = MainWindow.Options.ExportParticleRadius / (float)CTF.PixelSize;
                    float SoftEdge = 16f;

                    float[] MaskBigData = MaskBig.GetHost(Intent.Write)[0];
                    for (int y = 0; y < size; y++)
                    {
                        int yy = y - size / 2;
                        yy *= yy;
                        for (int x = 0; x < size; x++)
                        {
                            int xx = x - size / 2;
                            xx *= xx;
                            float R = (float)Math.Sqrt(xx + yy);

                            if (R <= MaskRadius)
                                MaskBigData[y * size + x] = 1;
                            else
                                MaskBigData[y * size + x] = (float)(Math.Cos(Math.Min(1, (R - MaskRadius) / SoftEdge) * Math.PI) * 0.5 + 0.5);
                        }
                    }
                    //MaskBig.WriteMRC("d_maskbig.mrc");

                    Mask = MaskBig.AsScaled(new int2(CoarseSize, CoarseSize));
                    Mask.RemapToFT();

                    MaskBigData = MaskBig.GetHost(Intent.Write)[0];
                    for (int y = 0; y < size; y++)
                    {
                        int yy = y - size / 2;
                        yy *= yy;
                        for (int x = 0; x < size; x++)
                        {
                            int xx = x - size / 2;
                            xx *= xx;
                            float R = (float)Math.Sqrt(xx + yy);

                            if (R <= 30)
                                MaskBigData[y * size + x] = 1;
                            else
                                MaskBigData[y * size + x] = 0;
                        }
                    }

                    MaskSubt = MaskBig.AsScaled(new int2(CoarseSize, CoarseSize));
                    MaskSubt.RemapToFT();

                    MaskBig.Dispose();
                }
                //Mask.WriteMRC("d_masksmall.mrc");

                #endregion

                #region Create Fourier space mask

                Image FourierMask = new Image(CoarseDims, true);
                {
                    float[] FourierMaskData = FourierMask.GetHost(Intent.Write)[0];
                    int MaxR2 = CoarseSize * CoarseSize / 4;
                    for (int y = 0; y < CoarseSize; y++)
                    {
                        int yy = y < CoarseSize / 2 + 1 ? y : y - CoarseSize;
                        yy *= yy;

                        for (int x = 0; x < CoarseSize / 2 + 1; x++)
                        {
                            int xx = x * x;
                            int R2 = yy + xx;

                            FourierMaskData[y * (CoarseSize / 2 + 1) + x] = R2 < MaxR2 ? 1 : 0;
                        }
                    }
                }

                #endregion

                #region For each particle, create CTFs and extract & preprocess images for entire tilt series

                for (int p = 0; p < NParticles; p++)
                {
                    float3 ParticleCoords = ParticleOrigins[p];
                    float3[] Positions = GetPositionInImages(ParticleCoords);
                    float3[] ProjAngles = GetParticleAngleInImages(ParticleCoords, ParticleAngles[p]);

                    Image Extracted = new Image(new int3(size, size, NTilts));
                    float[][] ExtractedData = Extracted.GetHost(Intent.Write);
                    float3[] Residuals = new float3[NTilts];

                    Image SubtrahendsCTF = new Image(new int3(CoarseSize, CoarseSize, NTilts), true);

                    // Create CTFs
                    {
                        CTFStruct[] CTFParams = new CTFStruct[NTilts];

                        float GridStep = 1f / (NTilts - 1);
                        CTFStruct[] Params = new CTFStruct[NTilts];
                        for (int t = 0; t < NTilts; t++)
                        {
                            decimal Defocus = (decimal)Positions[t].Z;
                            decimal DefocusDelta = (decimal)GridCTFDefocusDelta.GetInterpolated(new float3(0.5f, 0.5f, t * GridStep));
                            decimal DefocusAngle = (decimal)GridCTFDefocusAngle.GetInterpolated(new float3(0.5f, 0.5f, t * GridStep));

                            CTF CurrCTF = CTF.GetCopy();
                            CurrCTF.Defocus = Defocus;
                            CurrCTF.DefocusDelta = DefocusDelta;
                            CurrCTF.DefocusAngle = DefocusAngle;
                            CurrCTF.Scale = (decimal)Math.Cos(Angles[t] * Helper.ToRad);
                            CurrCTF.Bfactor = (decimal)-Dose[t] * 8;

                            Params[t] = CurrCTF.ToStruct();
                        }

                        GPU.CreateCTF(ParticleCTFs.GetDeviceSlice(NTilts * p, Intent.Write),
                                      CTFCoords.GetDevice(Intent.Read),
                                      (uint)CoarseDims.ElementsFFT(),
                                      Params,
                                      false,
                                      (uint)NTilts);
                    }

                    // Extract images
                    {
                        for (int t = 0; t < NTilts; t++)
                        {
                            ExtractedAt[p * NTilts + t] = new float2(Positions[t].X, Positions[t].Y);

                            Positions[t] -= size / 2;
                            int2 IntPosition = new int2((int)Positions[t].X, (int)Positions[t].Y);
                            float2 Residual = new float2(-(Positions[t].X - IntPosition.X), -(Positions[t].Y - IntPosition.Y));
                            Residuals[t] = new float3(Residual / size * CoarseSize);

                            float[] OriginalData;
                            lock (tiltStack)
                                OriginalData = tiltStack.GetHost(Intent.Read)[t];

                            float[] ImageData = ExtractedData[t];
                            for (int y = 0; y < size; y++)
                            {
                                int PosY = (y + IntPosition.Y + tiltStack.Dims.Y) % tiltStack.Dims.Y;
                                for (int x = 0; x < size; x++)
                                {
                                    int PosX = (x + IntPosition.X + tiltStack.Dims.X) % tiltStack.Dims.X;
                                    ImageData[y * size + x] = OriginalData[PosY * tiltStack.Dims.X + PosX];
                                }
                            }
                        }

                        GPU.NormParticles(Extracted.GetDevice(Intent.Read),
                                          Extracted.GetDevice(Intent.Write),
                                          new int3(size, size, 1),
                                          (uint)(MainWindow.Options.ExportParticleRadius / CTF.PixelSize),
                                          true,
                                          (uint)NTilts);

                        Image Scaled = Extracted.AsScaled(new int2(CoarseSize, CoarseSize));
                        //Scaled.WriteMRC("d_scaled.mrc");
                        Extracted.Dispose();

                        Scaled.ShiftSlices(Residuals);
                        Scaled.RemapToFT();

                        //GPU.NormalizeMasked(Scaled.GetDevice(Intent.Read),
                        //              Scaled.GetDevice(Intent.Write),
                        //              MaskSubt.GetDevice(Intent.Read),
                        //              (uint)Scaled.ElementsSliceReal,
                        //              (uint)NTilts);

                        //{
                        //    //Image SubtrahendsFT = subtrahendReference.Project(new int2(CoarseSize, CoarseSize), ProjAngles, CoarseSize / 2);
                        //    //SubtrahendsFT.Multiply(SubtrahendsCTF);

                        //    //Image Subtrahends = SubtrahendsFT.AsIFFT();
                        //    //SubtrahendsFT.Dispose();

                        //    ////GPU.NormalizeMasked(Subtrahends.GetDevice(Intent.Read),
                        //    ////                    Subtrahends.GetDevice(Intent.Write),
                        //    ////                    MaskSubt.GetDevice(Intent.Read),
                        //    ////                    (uint)Subtrahends.ElementsSliceReal,
                        //    ////                    (uint)NTilts);

                        //    //Scaled.Subtract(Subtrahends);
                        //    //Subtrahends.Dispose();

                        //    Image FocusMaskFT = maskReference.Project(new int2(CoarseSize, CoarseSize), ProjAngles, CoarseSize / 2);
                        //    Image FocusMask = FocusMaskFT.AsIFFT();
                        //    FocusMaskFT.Dispose();

                        //    Scaled.Multiply(FocusMask);
                        //    FocusMask.Dispose();
                        //}

                        Scaled.MultiplySlices(Mask);

                        GPU.FFT(Scaled.GetDevice(Intent.Read),
                                ParticleImages.GetDeviceSlice(p * NTilts, Intent.Write),
                                CoarseDims,
                                (uint)NTilts);

                        Scaled.Dispose();
                        SubtrahendsCTF.Dispose();
                    }
                }

                #endregion

                ParticleCTFs.MultiplySlices(FourierMask);

                Mask.Dispose();
                FourierMask.Dispose();
                MaskSubt.Dispose();

                Image ParticleCTFsAbs = new Image(ParticleCTFs.GetDevice(Intent.Read), ParticleCTFs.Dims, true);
                ParticleCTFsAbs.Abs();
                ParticleWeights = ParticleCTFsAbs.AsSum2D();
                ParticleCTFsAbs.Dispose();
                {
                    float[] ParticleWeightsData = ParticleWeights.GetHost(Intent.ReadWrite)[0];
                    float Max = MathHelper.Max(ParticleWeightsData);
                    for (int i = 0; i < ParticleWeightsData.Length; i++)
                        ParticleWeightsData[i] /= Max;
                }

                CTFCoords.Dispose();

                //Image CheckImages = ParticleImages.AsIFFT();
                //CheckImages.WriteMRC("d_particleimages.mrc");
                //CheckImages.Dispose();

                //ParticleCTFs.WriteMRC("d_particlectfs.mrc");
            }
            GlobalBfactor = KeepBFac;

            #endregion
            
            #region Global alignment

            Func<float3[], float2[]> GetImageShifts = input =>
                {
                    // Using current positions, angles and grids, get parameters for image shifts
                    float2[] ImageShifts = new float2[NParticles * NTilts];
                    float3[] PerTiltPositions = new float3[NParticles * NTilts];
                    for (int p = 0; p < NParticles; p++)
                        for (int t = 0; t < NTilts; t++)
                            PerTiltPositions[p * NTilts + t] = input[p];

                    float3[] CurrPositions = GetPositionInImages(PerTiltPositions);
                    for (int i = 0; i < ImageShifts.Length; i++)
                        ImageShifts[i] = new float2(ExtractedAt[i].X - CurrPositions[i].X,
                                                    ExtractedAt[i].Y - CurrPositions[i].Y); // -diff because those are extraction positions, i. e. opposite direction of shifts

                    return ImageShifts;
                };

            Func<float3[], float3[]> GetImageAngles = input =>
                {
                    int NAngles = input.Length;
                    float3 VolumeCenter = new float3(VolumeDimensions.X / 2, VolumeDimensions.Y / 2, VolumeDimensions.Z / 2);
                    float3[] PerTiltPositions = new float3[NAngles * NTilts];
                    float3[] PerTiltAngles = new float3[NAngles * NTilts];
                    for (int a = 0; a < NAngles; a++)
                        for (int t = 0; t < NTilts; t++)
                        {
                            PerTiltPositions[a * NTilts + t] = VolumeCenter;
                            PerTiltAngles[a * NTilts + t] = input[a];
                        }

                    float3[] ImageAngles = GetParticleAngleInImages(PerTiltPositions, PerTiltAngles);

                    return ImageAngles;
                };

            float3[] RelativeOffsets;
            {
                List<float3> RelativeOffsetList = new List<float3>();
                int NSteps = (int)Math.Ceiling(offsetRange / offsetStep);
                for (int z = -NSteps; z <= NSteps; z++)
                    for (int y = -NSteps; y <= NSteps; y++)
                        for (int x = -NSteps; x <= NSteps; x++)
                        {
                            float R = (float)Math.Sqrt(x * x + y * y + z * z) * offsetStep;
                            if (R > offsetRange + 1e-6f)
                                continue;

                            RelativeOffsetList.Add(new float3(x * offsetStep, y * offsetStep, z * offsetStep));
                        }

                RelativeOffsets = RelativeOffsetList.ToArray();
            }

            float3[] HealpixAngles = Helper.GetHealpixAngles(healpixOrder, symmetry).Select(a => a * Helper.ToRad).ToArray();
            float3[] ProjectionAngles = GetImageAngles(HealpixAngles);

            float3[] OptimizedOrigins = new float3[NParticles];
            float3[] OptimizedAngles = new float3[NParticles];
            float[] BestScores = new float[NParticles].Select(v => -float.MaxValue).ToArray();

            int BatchAngles = 128;
            Image Projections = new Image(new int3(CoarseSize, CoarseSize, BatchAngles * NTilts), true, true);

            foreach (var subset in SubsetRanges)
            {
                int NSubset = subset.Value.Item2 - subset.Value.Item1;

                float[] ImageOffsets = new float[NSubset * NTilts * RelativeOffsets.Length * 2];
                for (int o = 0; o < RelativeOffsets.Length; o++)
                {
                    float3[] OffsetOrigins = new float3[NSubset];
                    for (int p = 0; p < NSubset; p++)
                        OffsetOrigins[p] = ParticleOrigins[subset.Value.Item1 + p] + RelativeOffsets[o];

                    float[] TheseOffsets = Helper.ToInterleaved(GetImageShifts(OffsetOrigins));
                    Array.Copy(TheseOffsets, 0, ImageOffsets, TheseOffsets.Length * o, TheseOffsets.Length);
                }

                int[] ShiftIDs = new int[NSubset];
                int[] AngleIDs = new int[NSubset];
                float[] SubsetScores = new float[NSubset];

                GPU.TomoGlobalAlign(ParticleImages.GetDeviceSlice(subset.Value.Item1 * NTilts, Intent.Read),
                                    ShiftFactors.GetDevice(Intent.Read),
                                    ParticleCTFs.GetDeviceSlice(subset.Value.Item1 * NTilts, Intent.Read),
                                    ParticleWeights.GetDeviceSlice(subset.Value.Item1 * NTilts, Intent.Read),
                                    new int2(CoarseDims),
                                    references[subset.Key].Data.GetDevice(Intent.Read),
                                    references[subset.Key].Data.Dims,
                                    references[subset.Key].Oversampling,
                                    Helper.ToInterleaved(ProjectionAngles),
                                    (uint)HealpixAngles.Length,
                                    ImageOffsets,
                                    (uint)RelativeOffsets.Length,
                                    (uint)NSubset,
                                    (uint)NTilts,
                                    AngleIDs,
                                    ShiftIDs,
                                    SubsetScores);
                
                for (int i = 0; i < NSubset; i++)
                {
                    OptimizedOrigins[subset.Value.Item1 + i] = ParticleOrigins[subset.Value.Item1 + i] + RelativeOffsets[ShiftIDs[i]];
                    OptimizedAngles[subset.Value.Item1 + i] = HealpixAngles[AngleIDs[i]];
                    BestScores[subset.Value.Item1 + i] = SubsetScores[i];
                }
            }

            Projections.Dispose();

            #endregion

            ParticleImages?.Dispose();
            ParticleCTFs?.Dispose();
            ParticleWeights?.Dispose();
            ShiftFactors?.Dispose();

            #region Extract particles at full resolution and back-project them into the reconstruction volumes

            {
                GPU.SetDevice(0);

                Image CTFCoords = GetCTFCoords(size, size);
                int[] SortedDosePrecalc = IndicesSortedDose;

                foreach (var subsetRange in SubsetRanges)
                {
                    lock (outReconstructions[subsetRange.Key])
                    {
                        for (int p = subsetRange.Value.Item1; p < subsetRange.Value.Item2; p++)
                        {
                            float3[] PerTiltPositions = new float3[NTilts];
                            float3[] PerTiltAngles = new float3[NTilts];
                            for (int t = 0; t < NTilts; t++)
                            {
                                PerTiltPositions[t] = OptimizedOrigins[p];
                                PerTiltAngles[t] = OptimizedAngles[p];
                            }

                            Image FullParticleImages = GetSubtomoImages(tiltStack, size, PerTiltPositions, true);
                            Image FullParticleCTFs = GetSubtomoCTFs(PerTiltPositions, CTFCoords);

                            FullParticleImages.Multiply(FullParticleCTFs);
                            FullParticleCTFs.Abs();

                            float3[] FullParticleAngles = GetParticleAngleInImages(PerTiltPositions, PerTiltAngles);

                            outReconstructions[subsetRange.Key].BackProject(FullParticleImages, FullParticleCTFs, FullParticleAngles);

                            FullParticleImages.Dispose();
                            FullParticleCTFs.Dispose();
                        }

                        for (int p = subsetRange.Value.Item1; p < subsetRange.Value.Item2; p++)
                        {
                            float3[] PerTiltPositions = new float3[NTilts];
                            float3[] PerTiltAngles = new float3[NTilts];
                            for (int t = 0; t < NTilts; t++)
                            {
                                PerTiltPositions[t] = OptimizedOrigins[p];
                                PerTiltAngles[t] = OptimizedAngles[p];
                            }

                            float3[] FullParticleAngles = GetParticleAngleInImages(PerTiltPositions, PerTiltAngles);

                            Image FullParticleCTFs = GetSubtomoCTFs(PerTiltPositions, CTFCoords, false);
                            Image FullParticleCTFWeights = GetSubtomoCTFs(PerTiltPositions, CTFCoords, true);

                            // CTF has to be converted to complex numbers with imag = 0
                            float2[] CTFsComplexData = new float2[FullParticleCTFs.ElementsComplex];
                            float[] CTFWeightsData = new float[FullParticleCTFs.ElementsComplex];
                            float[] CTFsContinuousData = FullParticleCTFs.GetHostContinuousCopy();
                            float[] CTFWeightsContinuousData = FullParticleCTFWeights.GetHostContinuousCopy();
                            for (int i = 0; i < CTFsComplexData.Length; i++)
                            {
                                CTFsComplexData[i] = new float2(Math.Abs(CTFsContinuousData[i] * CTFWeightsContinuousData[i]), 0);
                                CTFWeightsData[i] = Math.Abs(CTFWeightsContinuousData[i]);
                            }

                            Image CTFsComplex = new Image(CTFsComplexData, FullParticleCTFs.Dims, true);
                            Image CTFWeights = new Image(CTFWeightsData, FullParticleCTFs.Dims, true);

                            outCTFReconstructions[subsetRange.Key].BackProject(CTFsComplex, CTFWeights, FullParticleAngles);

                            FullParticleCTFs.Dispose();
                            FullParticleCTFWeights.Dispose();
                            CTFsComplex.Dispose();
                            CTFWeights.Dispose();
                        }

                        outReconstructions[subsetRange.Key].FreeDevice();
                        outCTFReconstructions[subsetRange.Key].FreeDevice();
                    }
                }

                CTFCoords.Dispose();
            }

            #endregion

            SaveMeta();
        }

        public void AlignTiltMovies(Star tableIn, int3 stackDimensions, int size, int3 volumeDimensions, Dictionary<int, Projector> references, float resolution)
        {
            Star TableSeries = new Star(DirectoryName + RootName + ".star");

            Image SimulatedSeries = StageDataLoad.LoadMap("d_simulatedseries.mrc", new int2(1, 1), 0, typeof (float));
            //Image SimulatedSeries = StageDataLoad.LoadMap(DirectoryName + RootName + ".ali", new int2(1, 1), 0, typeof(float));
            Image AlignedSeries = new Image(SimulatedSeries.Dims);

            for (int t = 26; t < NTilts; t++)
            {
                Image TiltMovie = StageDataLoad.LoadMap(DirectoryName + TableSeries.GetRowValue(t, "wrpMovieName"), new int2(1, 1), 0, typeof (float));
                Image Template = new Image(SimulatedSeries.GetHost(Intent.Read)[t], SimulatedSeries.Dims.Slice());

                float MovieAngle = float.Parse(TableSeries.GetRowValue(t, "wrpAnglePsi"), CultureInfo.InvariantCulture);
                //float2 MovieShift = new float2(-float.Parse(TableSeries.GetRowValue(t, "wrpShiftX"), CultureInfo.InvariantCulture),
                //                               -float.Parse(TableSeries.GetRowValue(t, "wrpShiftY"), CultureInfo.InvariantCulture));
                float2 MovieShift = new float2(5.64f, -21f);

                Image Aligned = AlignOneTiltMovie(TiltMovie, Template, MovieAngle, MovieShift, resolution);

                AlignedSeries.GetHost(Intent.Write)[t] = Aligned.GetHost(Intent.Read)[0];

                Aligned.Dispose();
                TiltMovie.Dispose();
                Template.Dispose();
            }

            SimulatedSeries.Dispose();

            AlignedSeries.WriteMRC(DirectoryName + RootName + ".aligned");
            AlignedSeries.Dispose();
        }

        public Image SimulateTiltSeries(Star tableIn, int3 stackDimensions, int size, int3 volumeDimensions, Dictionary<int, Projector> references, float resolution)
        {
            VolumeDimensions = volumeDimensions;

            Image SimulatedStack = new Image(stackDimensions);

            #region Get rows from table

            List<int> RowIndices = new List<int>();
            string[] ColumnMicrographName = tableIn.GetColumn("rlnMicrographName");
            for (int i = 0; i < ColumnMicrographName.Length; i++)
                if (ColumnMicrographName[i].Contains(RootName))
                    RowIndices.Add(i);

            if (RowIndices.Count == 0)
                return SimulatedStack;

            int NParticles = RowIndices.Count;

            #endregion

            #region Make sure all columns and directories are there

            if (!tableIn.HasColumn("rlnImageName"))
                tableIn.AddColumn("rlnImageName");
            if (!tableIn.HasColumn("rlnCtfImage"))
                tableIn.AddColumn("rlnCtfImage");
            if (!tableIn.HasColumn("rlnParticleSelectZScore"))
                tableIn.AddColumn("rlnParticleSelectZScore");

            if (!Directory.Exists(ParticlesDir))
                Directory.CreateDirectory(ParticlesDir);
            if (!Directory.Exists(ParticleCTFDir))
                Directory.CreateDirectory(ParticleCTFDir);

            #endregion

            #region Get subtomo positions from table

            float3[] ParticleOrigins = new float3[NParticles];
            float3[] ParticleAngles = new float3[NParticles];
            int[] ParticleSubset = new int[NParticles];
            {
                string[] ColumnPosX = tableIn.GetColumn("rlnCoordinateX");
                string[] ColumnPosY = tableIn.GetColumn("rlnCoordinateY");
                string[] ColumnPosZ = tableIn.GetColumn("rlnCoordinateZ");
                string[] ColumnOriginX = tableIn.GetColumn("rlnOriginX");
                string[] ColumnOriginY = tableIn.GetColumn("rlnOriginY");
                string[] ColumnOriginZ = tableIn.GetColumn("rlnOriginZ");
                string[] ColumnAngleRot = tableIn.GetColumn("rlnAngleRot");
                string[] ColumnAngleTilt = tableIn.GetColumn("rlnAngleTilt");
                string[] ColumnAnglePsi = tableIn.GetColumn("rlnAnglePsi");
                string[] ColumnSubset = tableIn.GetColumn("rlnRandomSubset");

                for (int i = 0; i < NParticles; i++)
                {
                    float3 Pos = new float3(float.Parse(ColumnPosX[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosY[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosZ[RowIndices[i]], CultureInfo.InvariantCulture));
                    float3 Shift = new float3(float.Parse(ColumnOriginX[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginY[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginZ[RowIndices[i]], CultureInfo.InvariantCulture));

                    ParticleOrigins[i] = Pos - Shift;
                    //ParticleOrigins[i] /= new float3(3838f / 959f, 3710f / 927f, 4f);

                    float3 Angle = new float3(float.Parse(ColumnAngleRot[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnAngleTilt[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnAnglePsi[RowIndices[i]], CultureInfo.InvariantCulture));
                    ParticleAngles[i] = Angle;

                    ParticleSubset[i] = int.Parse(ColumnSubset[RowIndices[i]]);

                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateX", ParticleOrigins[i].X.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateY", ParticleOrigins[i].Y.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateZ", ParticleOrigins[i].Z.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginX", "0.0");
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginY", "0.0");
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginZ", "0.0");
                }
            }

            #endregion

            #region Deal with subsets

            List<int> SubsetIDs = new List<int>();
            foreach (var i in ParticleSubset)
                if (!SubsetIDs.Contains(i))
                    SubsetIDs.Add(i);
            SubsetIDs.Sort();

            // For each subset, create a list of its particle IDs
            Dictionary<int, List<int>> SubsetParticleIDs = SubsetIDs.ToDictionary(subsetID => subsetID, subsetID => new List<int>());
            for (int i = 0; i < ParticleSubset.Length; i++)
                SubsetParticleIDs[ParticleSubset[i]].Add(i);
            foreach (var list in SubsetParticleIDs.Values)
                list.Sort();

            // Note where each subset starts and ends in a unified, sorted (by subset) particle ID list
            Dictionary<int, Tuple<int, int>> SubsetRanges = new Dictionary<int, Tuple<int, int>>();
            {
                int Start = 0;
                foreach (var pair in SubsetParticleIDs)
                {
                    SubsetRanges.Add(pair.Key, new Tuple<int, int>(Start, Start + pair.Value.Count));
                    Start += pair.Value.Count;
                }
            }

            List<int> SubsetContinuousIDs = new List<int>();
            foreach (var pair in SubsetParticleIDs)
                SubsetContinuousIDs.AddRange(pair.Value);

            // Reorder particle information to match the order of SubsetContinuousIDs
            ParticleOrigins = SubsetContinuousIDs.Select(i => ParticleOrigins[i]).ToArray();
            ParticleAngles = SubsetContinuousIDs.Select(i => ParticleAngles[i]).ToArray();
            ParticleSubset = SubsetContinuousIDs.Select(i => ParticleSubset[i]).ToArray();

            #endregion
            
            int CoarseSize = (int)Math.Round(size * ((float)CTF.PixelSize * 2 / resolution)) / 2 * 2;
            int3 CoarseDims = new int3(CoarseSize, CoarseSize, 1);

            // Positions the particles were extracted at/shifted to, to calculate effectively needed shifts later
            float3[] ExtractedAt = new float3[NParticles * NTilts];

            // Extract images, mask and resize them, create CTFs

            #region Preflight
            {
                Image CTFCoords = GetCTFCoords(CoarseSize, size);

                #region Create mask with soft edge
                Image Mask;
                {
                    Image MaskBig = new Image(new int3(size, size, 1));
                    float MaskRadius = MainWindow.Options.ExportParticleRadius / (float)CTF.PixelSize;
                    float SoftEdge = 16f;

                    float[] MaskBigData = MaskBig.GetHost(Intent.Write)[0];
                    for (int y = 0; y < size; y++)
                    {
                        int yy = y - size / 2;
                        yy *= yy;
                        for (int x = 0; x < size; x++)
                        {
                            int xx = x - size / 2;
                            xx *= xx;
                            float R = (float)Math.Sqrt(xx + yy);

                            if (R <= MaskRadius)
                                MaskBigData[y * size + x] = 1;
                            else
                                MaskBigData[y * size + x] = (float)(Math.Cos(Math.Min(1, (R - MaskRadius) / SoftEdge) * Math.PI) * 0.5 + 0.5);
                        }
                    }
                    MaskBig.WriteMRC("d_maskbig.mrc");

                    Mask = MaskBig.AsScaled(new int2(CoarseSize, CoarseSize));

                    MaskBig.Dispose();
                }
                Mask.WriteMRC("d_masksmall.mrc");
                #endregion

                #region Create Fourier space mask
                Image FourierMask = new Image(CoarseDims, true);
                {
                    float[] FourierMaskData = FourierMask.GetHost(Intent.Write)[0];
                    int MaxR2 = CoarseSize * CoarseSize / 4;
                    for (int y = 0; y < CoarseSize; y++)
                    {
                        int yy = y < CoarseSize / 2 + 1 ? y : y - CoarseSize;
                        yy *= yy;

                        for (int x = 0; x < CoarseSize / 2 + 1; x++)
                        {
                            int xx = x * x;
                            int R2 = yy + xx;

                            FourierMaskData[y * (CoarseSize / 2 + 1) + x] = R2 < MaxR2 ? 1 : 0;
                        }
                    }
                }
                #endregion

                #region For each particle, create CTFs and projections, and insert them into the simulated tilt series
                for (int p = 0; p < NParticles; p++)
                {
                    Image ParticleImages;
                    Image ParticleCTFs = new Image(new int3(CoarseSize, CoarseSize, NTilts), true);

                    float3 ParticleCoords = ParticleOrigins[p];
                    float3[] Positions = GetPositionInImages(ParticleCoords);
                    
                    // Create CTFs
                    {
                        float GridStep = 1f / (NTilts - 1);
                        CTFStruct[] Params = new CTFStruct[NTilts];
                        for (int t = 0; t < NTilts; t++)
                        {
                            decimal Defocus = (decimal)Positions[t].Z;
                            decimal DefocusDelta = (decimal)GridCTFDefocusDelta.GetInterpolated(new float3(0.5f, 0.5f, t * GridStep));
                            decimal DefocusAngle = (decimal)GridCTFDefocusAngle.GetInterpolated(new float3(0.5f, 0.5f, t * GridStep));

                            CTF CurrCTF = CTF.GetCopy();
                            CurrCTF.Defocus = Defocus;
                            CurrCTF.DefocusDelta = DefocusDelta;
                            CurrCTF.DefocusAngle = DefocusAngle;
                            CurrCTF.Scale = (decimal)Math.Cos(Angles[t] * Helper.ToRad);
                            CurrCTF.Bfactor = (decimal)-Dose[t] * 8;

                            Params[t] = CurrCTF.ToStruct();
                        }

                        GPU.CreateCTF(ParticleCTFs.GetDevice(Intent.Write),
                                      CTFCoords.GetDevice(Intent.Read),
                                      (uint)CoarseDims.ElementsFFT(),
                                      Params,
                                      false,
                                      (uint)NTilts);

                        ParticleCTFs.MultiplySlices(FourierMask);
                    }

                    // Make projections
                    {
                        float3[] ImageShifts = new float3[NTilts];
                        float3[] ImageAngles = new float3[NTilts];

                        float3[] CurrPositions = GetPositionInImages(ParticleOrigins[p]);
                        float3[] CurrAngles = GetParticleAngleInImages(ParticleOrigins[p], ParticleAngles[p]);
                        for (int t = 0; t < NTilts; t++)
                        {
                            ImageShifts[t] = new float3(CurrPositions[t].X - (int)CurrPositions[t].X, // +diff because we are shifting the projections into experimental data frame
                                                        CurrPositions[t].Y - (int)CurrPositions[t].Y,
                                                        CurrPositions[t].Z - (int)CurrPositions[t].Z);
                            ImageAngles[t] = CurrAngles[t];
                        }

                        Image ProjectionsFT = new Image(IntPtr.Zero, new int3(CoarseSize, CoarseSize, NTilts), true, true);

                        Projector Reference = references[ParticleSubset[p]];

                        GPU.ProjectForward(Reference.Data.GetDevice(Intent.Read),
                                           ProjectionsFT.GetDevice(Intent.Write),
                                           Reference.Data.Dims,
                                           new int2(CoarseSize, CoarseSize),
                                           Helper.ToInterleaved(ImageAngles),
                                           Reference.Oversampling,
                                           (uint)NTilts);

                        ProjectionsFT.Multiply(ParticleCTFs);
                        Image Projections = ProjectionsFT.AsIFFT();
                        ProjectionsFT.Dispose();

                        Projections.RemapFromFT();

                        GPU.NormParticles(Projections.GetDevice(Intent.Read),
                                          Projections.GetDevice(Intent.Write),
                                          new int3(CoarseSize, CoarseSize, 1),
                                          (uint)(MainWindow.Options.ExportParticleRadius / CTF.PixelSize * (CTF.PixelSize * 2 / (decimal)resolution)),
                                          true,
                                          (uint)NTilts);

                        Projections.MultiplySlices(Mask);

                        ParticleImages = Projections.AsScaled(new int2(size, size));
                        Projections.Dispose();

                        ParticleImages.ShiftSlices(ImageShifts);
                    }

                    // Extract images
                    {
                        for (int t = 0; t < NTilts; t++)
                        {
                            Positions[t] -= size / 2;
                            int2 IntPosition = new int2((int)Positions[t].X, (int)Positions[t].Y);

                            float[] SimulatedData;
                            lock (SimulatedStack)
                                SimulatedData = SimulatedStack.GetHost(Intent.Write)[t];

                            float[] ImageData = ParticleImages.GetHost(Intent.Read)[t];
                            for (int y = 0; y < size; y++)
                            {
                                int PosY = (y + IntPosition.Y + SimulatedStack.Dims.Y) % SimulatedStack.Dims.Y;
                                for (int x = 0; x < size; x++)
                                {
                                    int PosX = (x + IntPosition.X + SimulatedStack.Dims.X) % SimulatedStack.Dims.X;
                                    SimulatedData[PosY * SimulatedStack.Dims.X + PosX] += ImageData[y * size + x];
                                }
                            }
                        }
                    }
            
                    ParticleImages.Dispose();
                    ParticleCTFs.Dispose();
                }
                #endregion

                Mask.Dispose();
                FourierMask.Dispose();
                CTFCoords.Dispose();
            }
            #endregion

            return SimulatedStack;
        }

        public Image AlignOneTiltMovie(Image tiltMovie, Image template, float initialAngle, float2 initialShift, float resolution)
        {
            float DownscaleFactor = (float)CTF.PixelSize * 2 / resolution;

            //template.Bandpass(0.02f, DownscaleFactor, false);
            //tiltMovie.Bandpass(0.02f, DownscaleFactor, false);

            //template = template.AsPadded(new int2(template.Dims) - 512);

            int2 DimsTemplate = new int2(template.Dims);
            int2 DimsTemplateCoarse = new int2(DimsTemplate) * DownscaleFactor / 2 * 2;
            int2 DimsFrame = new int2(tiltMovie.Dims);
            int NFrames = tiltMovie.Dims.Z;

            //GPU.Normalize(tiltMovie.GetDevice(Intent.Read),
            //              tiltMovie.GetDevice(Intent.Write),
            //              (uint)tiltMovie.ElementsSliceReal,
            //              (uint)tiltMovie.Dims.Z);

            Image TemplateCoarse = template.AsScaled(DimsTemplateCoarse);

            float GlobalAngle = initialAngle;
            float ConditioningAngle = 180f / DimsFrame.X;
            CubicGrid GridFrameX = new CubicGrid(new int3(1, 1, 1), initialShift.X, initialShift.X, Dimension.X);
            CubicGrid GridFrameY = new CubicGrid(new int3(1, 1, 1), initialShift.Y, initialShift.Y, Dimension.X);

            Action<double[]> SetFromVector = input =>
            {
                GlobalAngle = (float)input[0] * ConditioningAngle;
                GridFrameX = new CubicGrid(GridFrameX.Dimensions, input.Skip(1).Take((int)GridFrameX.Dimensions.Elements()).Select(v => (float)v).ToArray());
                GridFrameY = new CubicGrid(GridFrameY.Dimensions, input.Skip(1 + (int)GridFrameX.Dimensions.Elements()).Take((int)GridFrameY.Dimensions.Elements()).Select(v => (float)v).ToArray());
            };

            Func<double[], double[]> EvalIndividual = input =>
            {
                SetFromVector(input);

                Image Transformed;

                float GridStep = 1f / Math.Max(NFrames - 1, 1);
                float2[] FrameShifts = new float2[NFrames];
                for (int i = 0; i < NFrames; i++)
                    FrameShifts[i] = new float2(GridFrameX.GetInterpolated(new float3(0.5f, 0.5f, i * GridStep)),
                                                GridFrameY.GetInterpolated(new float3(0.5f, 0.5f, i * GridStep)));

                float[] FrameAngles = new float[NFrames].Select(v => -GlobalAngle * Helper.ToRad).ToArray();

                Image MovieCopy = new Image(IntPtr.Zero, tiltMovie.Dims);
                //MovieCopy.ShiftSlicesMassive(FrameShifts);

                GPU.ShiftAndRotate2D(tiltMovie.GetDevice(Intent.Read),
                                     MovieCopy.GetDevice(Intent.Write),
                                     DimsFrame,
                                     Helper.ToInterleaved(FrameShifts),
                                     FrameAngles,
                                     (uint)NFrames);

                Transformed = MovieCopy.AsPadded(DimsTemplate);
                MovieCopy.Dispose();
                
                Transformed.MultiplySlices(template);

                Image Sums = new Image(IntPtr.Zero, new int3(NFrames, 1, 1));
                GPU.Sum(Transformed.GetDevice(Intent.Read),
                        Sums.GetDevice(Intent.Write),
                        (uint)Transformed.ElementsSliceReal,
                        (uint)NFrames);

                Transformed.Dispose();

                double[] Result = new double[NFrames];
                for (int i = 0; i < NFrames; i++)
                    Result[i] = Sums.GetHost(Intent.Read)[0][i] / Transformed.ElementsSliceReal * 100;

                return Result;
            };

            Func<double[], double> Eval = input =>
            {
                double[] Scores = EvalIndividual(input);
                double Score = Scores.Sum();
                Debug.WriteLine(Score);

                return Score;
            };

            Func<double[], double[]> Grad = input =>
            {
                double Delta = 0.1 / DownscaleFactor;
                double[] Result = new double[input.Length];

                for (int i = 0; i < input.Length; i++)
                {
                    double[] InputPlus = input.ToArray();
                    InputPlus[i] += Delta;
                    double ScorePlus = EvalIndividual(InputPlus).Sum();

                    double[] InputMinus = input.ToArray();
                    InputMinus[i] -= Delta;
                    double ScoreMinus = EvalIndividual(InputMinus).Sum();

                    Result[i] = (ScorePlus - ScoreMinus) / (Delta * 2);
                }

                return Result;
            };

            List<double> StartList = new List<double>();
            StartList.Add(GlobalAngle / ConditioningAngle);
            StartList.AddRange(GridFrameX.FlatValues.Select(v => (double)v));
            StartList.AddRange(GridFrameY.FlatValues.Select(v => (double)v));
            double[] StartVector = StartList.ToArray();

            BroydenFletcherGoldfarbShanno Optimizer = new BroydenFletcherGoldfarbShanno(StartVector.Length, Eval, Grad);
            Optimizer.Maximize(StartVector);

            TemplateCoarse.Dispose();

            return null;
        }

        public void Correlate(Image tiltStack, Image reference, int size, float lowpassAngstrom, float highpassAngstrom, int3 volumeDimensions, int healpixOrder, string symmetry = "C1")
        {
            if (!Directory.Exists(CorrelationDir))
                Directory.CreateDirectory(CorrelationDir);

            float DownscaleFactor = lowpassAngstrom / (float)(CTF.PixelSize * 2);
            Image DownscaledStack = tiltStack.AsScaledMassive(new int2(tiltStack.Dims) / DownscaleFactor / 2 * 2);
            tiltStack.FreeDevice();

            float HighpassNyquist = (float)(CTF.PixelSize * 2) * DownscaleFactor / highpassAngstrom;
            DownscaledStack.Bandpass(HighpassNyquist, 1, false);

            Image ScaledReference = reference.AsScaled(reference.Dims / DownscaleFactor / 2 * 2);
            reference.FreeDevice();
            Image PaddedReference = ScaledReference.AsPadded(new int3(size, size, size));
            ScaledReference.Dispose();
            PaddedReference.Bandpass(HighpassNyquist, 1, true);
            Projector ProjectorReference = new Projector(PaddedReference, 2);
            PaddedReference.Dispose();

            VolumeDimensions = volumeDimensions / DownscaleFactor / 2 * 2;
            int SizeCropped = size / 2;

            int3 Grid = (VolumeDimensions + SizeCropped - 1) / SizeCropped;
            List<float3> GridCoords = new List<float3>();
            for (int z = 0; z < Grid.Z; z++)
                for (int y = 0; y < Grid.Y; y++)
                    for (int x = 0; x < Grid.X; x++)
                        GridCoords.Add(new float3(x * SizeCropped + SizeCropped / 2,
                                                  y * SizeCropped + SizeCropped / 2,
                                                  z * SizeCropped + SizeCropped / 2));

            float3[] HealpixAngles = Helper.GetHealpixAngles(healpixOrder, symmetry).Select(a => a * Helper.ToRad).ToArray();

            Image CTFCoords = GetCTFCoords(size, (int)(size * DownscaleFactor));

            float[] OutputCorr = new float[VolumeDimensions.Elements()];

            int PlanForw, PlanBack, PlanForwCTF;
            Projector.GetPlans(new int3(size, size, size), 2, out PlanForw, out PlanBack, out PlanForwCTF);

            int BatchSize = 16;
            for (int b = 0; b < GridCoords.Count; b += BatchSize)
            {
                int CurBatch = Math.Min(BatchSize, GridCoords.Count - b);

                Image Subtomos = new Image(IntPtr.Zero, new int3(size, size, size * CurBatch), true, true);
                Image SubtomoCTFs = new Image(IntPtr.Zero, new int3(size, size, size * CurBatch), true);

                for (int st = 0; st < CurBatch; st++)
                {
                    Image ImagesFT = GetSubtomoImages(DownscaledStack, size, GridCoords[b + st], true);
                    Image CTFs = GetSubtomoCTFs(GridCoords[b + st], CTFCoords);
                    //Image CTFWeights = GetSubtomoCTFs(GridCoords[b + st], CTFCoords, true, true);

                    ImagesFT.Multiply(CTFs);    // Weight and phase-flip image FTs by CTF, which still has its sign here
                    //ImagesFT.Multiply(CTFWeights);
                    CTFs.Abs();                 // CTF has to be positive from here on since image FT phases are now flipped

                    // CTF has to be converted to complex numbers with imag = 0, and weighted by itself
                    float2[] CTFsComplexData = new float2[CTFs.ElementsComplex];
                    float[] CTFsContinuousData = CTFs.GetHostContinuousCopy();
                    for (int i = 0; i < CTFsComplexData.Length; i++)
                        CTFsComplexData[i] = new float2(CTFsContinuousData[i] * CTFsContinuousData[i], 0);

                    Image CTFsComplex = new Image(CTFsComplexData, CTFs.Dims, true);

                    Projector ProjSubtomo = new Projector(new int3(size, size, size), 2);
                    lock (GPU.Sync)
                        ProjSubtomo.BackProject(ImagesFT, CTFs, GetAngleInImages(GridCoords[b + st]));
                    Image Subtomo = ProjSubtomo.Reconstruct(false, PlanForw, PlanBack, PlanForwCTF);
                    ProjSubtomo.Dispose();

                    /*Image CroppedSubtomo = Subtomo.AsPadded(new int3(SizeCropped, SizeCropped, SizeCropped));
                    CroppedSubtomo.WriteMRC(ParticlesDir + RootName + "_" + (b + st).ToString("D5") + ".mrc");
                    CroppedSubtomo.Dispose();*/

                    Projector ProjCTF = new Projector(new int3(size, size, size), 2);
                    lock (GPU.Sync)
                        ProjCTF.BackProject(CTFsComplex, CTFs, GetAngleInImages(GridCoords[b + st]));
                    Image SubtomoCTF = ProjCTF.Reconstruct(true, PlanForw, PlanBack, PlanForwCTF);
                    ProjCTF.Dispose();

                    GPU.FFT(Subtomo.GetDevice(Intent.Read), Subtomos.GetDeviceSlice(size * st, Intent.Write), Subtomo.Dims, 1);
                    GPU.CopyDeviceToDevice(SubtomoCTF.GetDevice(Intent.Read), SubtomoCTFs.GetDeviceSlice(size * st, Intent.Write), SubtomoCTF.ElementsReal);

                    ImagesFT.Dispose();
                    CTFs.Dispose();
                    //CTFWeights.Dispose();
                    CTFsComplex.Dispose();

                    Subtomo.Dispose();
                    SubtomoCTF.Dispose();
                }

                Image BestCorrelation = new Image(IntPtr.Zero, new int3(size, size, size * CurBatch));
                Image BestRot = new Image(IntPtr.Zero, new int3(size, size, size * CurBatch));
                Image BestTilt = new Image(IntPtr.Zero, new int3(size, size, size * CurBatch));
                Image BestPsi = new Image(IntPtr.Zero, new int3(size, size, size * CurBatch));

                GPU.CorrelateSubTomos(ProjectorReference.Data.GetDevice(Intent.Read),
                                      ProjectorReference.Oversampling,
                                      ProjectorReference.Data.Dims,
                                      Subtomos.GetDevice(Intent.Read),
                                      SubtomoCTFs.GetDevice(Intent.Read),
                                      new int3(size, size, size),
                                      (uint)CurBatch,
                                      Helper.ToInterleaved(HealpixAngles),
                                      (uint)HealpixAngles.Length,
                                      MainWindow.Options.ExportParticleRadius / ((float)CTF.PixelSize * DownscaleFactor),
                                      BestCorrelation.GetDevice(Intent.Write),
                                      BestRot.GetDevice(Intent.Write),
                                      BestTilt.GetDevice(Intent.Write),
                                      BestPsi.GetDevice(Intent.Write));

                for (int st = 0; st < CurBatch; st++)
                {
                    Image ThisCorrelation = new Image(BestCorrelation.GetDeviceSlice(size * st, Intent.Read), new int3(size, size, size));
                    Image CroppedCorrelation = ThisCorrelation.AsPadded(new int3(SizeCropped, SizeCropped, SizeCropped));

                    //CroppedCorrelation.WriteMRC(CorrelationDir + RootName + "_" + (b + st).ToString("D5") + ".mrc");
                    float[] SubCorr = CroppedCorrelation.GetHostContinuousCopy();
                    int3 Origin = new int3(GridCoords[b + st]) - SizeCropped / 2;
                    for (int z = 0; z < SizeCropped; z++)
                    {
                        int zVol = Origin.Z + z;
                        if (zVol >= VolumeDimensions.Z)
                            continue;

                        for (int y = 0; y < SizeCropped; y++)
                        {
                            int yVol = Origin.Y + y;
                            if (yVol >= VolumeDimensions.Y)
                                continue;

                            for (int x = 0; x < SizeCropped; x++)
                            {
                                int xVol = Origin.X + x;
                                if (xVol >= VolumeDimensions.X)
                                    continue;

                                OutputCorr[(zVol * VolumeDimensions.Y + yVol) * VolumeDimensions.X + xVol] = SubCorr[(z * SizeCropped + y) * SizeCropped + x];
                            }
                        }
                    }

                    CroppedCorrelation.Dispose();
                    ThisCorrelation.Dispose();
                }

                Subtomos.Dispose();
                SubtomoCTFs.Dispose();

                BestCorrelation.Dispose();
                BestRot.Dispose();
                BestTilt.Dispose();
                BestPsi.Dispose();
            }

            GPU.DestroyFFTPlan(PlanForw);
            GPU.DestroyFFTPlan(PlanBack);
            GPU.DestroyFFTPlan(PlanForwCTF);

            CTFCoords.Dispose();
            ProjectorReference.Dispose();
            DownscaledStack.Dispose();

            Image OutputCorrImage = new Image(OutputCorr, VolumeDimensions);
            OutputCorrImage.WriteMRC(CorrelationDir + RootName + ".mrc");
            OutputCorrImage.Dispose();
        }

        public void Reconstruct(Image tiltStack, int size, float lowpassAngstrom, int3 volumeDimensions)
        {
            if (!Directory.Exists(ReconstructionDir))
                Directory.CreateDirectory(ReconstructionDir);

            if (File.Exists(ReconstructionDir + RootName + ".mrc"))
                return;

            VolumeDimensions = volumeDimensions;

            float DownscaleFactor = lowpassAngstrom / (float)(CTF.PixelSize * 2);
            Image DownscaledStack = tiltStack.AsScaledMassive(new int2(tiltStack.Dims) / DownscaleFactor / 2 * 2);
            tiltStack.FreeDevice();

            GPU.Normalize(DownscaledStack.GetDevice(Intent.Read),
                          DownscaledStack.GetDevice(Intent.Write),
                          (uint)DownscaledStack.ElementsSliceReal,
                          (uint)DownscaledStack.Dims.Z);

            int3 VolumeDimensionsCropped = volumeDimensions / DownscaleFactor / 2 * 2;
            int SizeCropped = size / 4;

            int3 Grid = (VolumeDimensionsCropped + SizeCropped - 1) / SizeCropped;
            List<float3> GridCoords = new List<float3>();
            for (int z = 0; z < Grid.Z; z++)
                for (int y = 0; y < Grid.Y; y++)
                    for (int x = 0; x < Grid.X; x++)
                        GridCoords.Add(new float3(x * SizeCropped + SizeCropped / 2,
                                                  y * SizeCropped + SizeCropped / 2,
                                                  z * SizeCropped + SizeCropped / 2));

            Image CTFCoords = GetCTFCoords(size, (int)(size * DownscaleFactor));

            float[] OutputRec = new float[VolumeDimensionsCropped.Elements()];

            int PlanForw, PlanBack, PlanForwCTF;
            Projector.GetPlans(new int3(size, size, size), 2, out PlanForw, out PlanBack, out PlanForwCTF);

            for (int p = 0; p < GridCoords.Count; p++)
            {
                Image ImagesFT = GetSubtomoImages(DownscaledStack, size, GridCoords[p] * DownscaleFactor, false, 1f / DownscaleFactor);
                Image CTFs = GetSubtomoCTFs(GridCoords[p] * DownscaleFactor, CTFCoords);
                //Image CTFWeights = GetSubtomoCTFs(GridCoords[p], CTFCoords, true, true);

                ImagesFT.Multiply(CTFs);    // Weight and phase-flip image FTs by CTF, which still has its sign here
                //ImagesFT.Multiply(CTFWeights);
                CTFs.Abs();                 // Since everything is already phase-flipped, weights are positive

                Projector ProjSubtomo = new Projector(new int3(size, size, size), 2);
                lock (GPU.Sync)
                    ProjSubtomo.BackProject(ImagesFT, CTFs, GetAngleInImages(GridCoords[p] * DownscaleFactor));
                Image Subtomo = ProjSubtomo.Reconstruct(false, PlanForw, PlanBack, PlanForwCTF);

                Image SubtomoCropped = Subtomo.AsPadded(new int3(SizeCropped, SizeCropped, SizeCropped));
                Subtomo.Dispose();
                ProjSubtomo.Dispose();

                /*Image CroppedSubtomo = Subtomo.AsPadded(new int3(SizeCropped, SizeCropped, SizeCropped));
                CroppedSubtomo.WriteMRC(ParticlesDir + RootName + "_" + (b + st).ToString("D5") + ".mrc");
                CroppedSubtomo.Dispose();*/

                ImagesFT.Dispose();
                CTFs.Dispose();
                //CTFWeights.Dispose();
                
                float[] SubtomoData = SubtomoCropped.GetHostContinuousCopy();
                int3 Origin = new int3(GridCoords[p]) - SizeCropped / 2;
                for (int z = 0; z < SizeCropped; z++)
                {
                    int zVol = Origin.Z + z;
                    if (zVol >= VolumeDimensionsCropped.Z)
                        continue;

                    for (int y = 0; y < SizeCropped; y++)
                    {
                        int yVol = Origin.Y + y;
                        if (yVol >= VolumeDimensionsCropped.Y)
                            continue;

                        for (int x = 0; x < SizeCropped; x++)
                        {
                            int xVol = Origin.X + x;
                            if (xVol >= VolumeDimensionsCropped.X)
                                continue;

                            OutputRec[(zVol * VolumeDimensionsCropped.Y + yVol) * VolumeDimensionsCropped.X + xVol] = -SubtomoData[(z * SizeCropped + y) * SizeCropped + x];
                        }
                    }
                }

                SubtomoCropped.Dispose();
            }

            GPU.DestroyFFTPlan(PlanForw);
            GPU.DestroyFFTPlan(PlanBack);
            GPU.DestroyFFTPlan(PlanForwCTF);

            CTFCoords.Dispose();
            DownscaledStack.Dispose();

            Image OutputRecImage = new Image(OutputRec, VolumeDimensionsCropped);
            OutputRecImage.WriteMRC(ReconstructionDir + RootName + ".mrc");
            OutputRecImage.Dispose();
        }

        public void RealspaceRefineGlobal(Star tableIn, Image tiltStack, int size, int3 volumeDimensions, Dictionary<int, Projector> references, float resolution, int healpixorder, string symmetry, Dictionary<int, Projector> outReconstructions)
        {
            VolumeDimensions = volumeDimensions;

            #region Get rows from table

            List<int> RowIndices = new List<int>();
            string[] ColumnMicrographName = tableIn.GetColumn("rlnMicrographName");
            for (int i = 0; i < ColumnMicrographName.Length; i++)
                if (ColumnMicrographName[i].Contains(RootName))
                    RowIndices.Add(i);

            if (RowIndices.Count == 0)
                return;

            int NParticles = RowIndices.Count;

            #endregion

            #region Make sure all columns and directories are there

            if (!tableIn.HasColumn("rlnImageName"))
                tableIn.AddColumn("rlnImageName");
            if (!tableIn.HasColumn("rlnCtfImage"))
                tableIn.AddColumn("rlnCtfImage");
            if (!tableIn.HasColumn("rlnParticleSelectZScore"))
                tableIn.AddColumn("rlnParticleSelectZScore");

            if (!Directory.Exists(ParticlesDir))
                Directory.CreateDirectory(ParticlesDir);
            if (!Directory.Exists(ParticleCTFDir))
                Directory.CreateDirectory(ParticleCTFDir);

            #endregion

            #region Get subtomo positions from table

            float3[] ParticleOrigins = new float3[NParticles];
            float3[] ParticleAngles = new float3[NParticles];
            int[] ParticleSubset = new int[NParticles];
            {
                string[] ColumnPosX = tableIn.GetColumn("rlnCoordinateX");
                string[] ColumnPosY = tableIn.GetColumn("rlnCoordinateY");
                string[] ColumnPosZ = tableIn.GetColumn("rlnCoordinateZ");
                string[] ColumnOriginX = tableIn.GetColumn("rlnOriginX");
                string[] ColumnOriginY = tableIn.GetColumn("rlnOriginY");
                string[] ColumnOriginZ = tableIn.GetColumn("rlnOriginZ");
                string[] ColumnAngleRot = tableIn.GetColumn("rlnAngleRot");
                string[] ColumnAngleTilt = tableIn.GetColumn("rlnAngleTilt");
                string[] ColumnAnglePsi = tableIn.GetColumn("rlnAnglePsi");
                string[] ColumnSubset = tableIn.GetColumn("rlnRandomSubset");

                for (int i = 0; i < NParticles; i++)
                {
                    float3 Pos = new float3(float.Parse(ColumnPosX[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosY[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosZ[RowIndices[i]], CultureInfo.InvariantCulture));
                    float3 Shift = new float3(float.Parse(ColumnOriginX[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginY[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginZ[RowIndices[i]], CultureInfo.InvariantCulture));

                    ParticleOrigins[i] = Pos - Shift;
                    //ParticleOrigins[i] /= new float3(3838f / 959f, 3710f / 927f, 4f);

                    float3 Angle = new float3(float.Parse(ColumnAngleRot[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnAngleTilt[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnAnglePsi[RowIndices[i]], CultureInfo.InvariantCulture));
                    ParticleAngles[i] = Angle;

                    if (ColumnSubset != null)
                        ParticleSubset[i] = int.Parse(ColumnSubset[RowIndices[i]]);
                    else
                        ParticleSubset[i] = (i % 2) + 1;

                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateX", ParticleOrigins[i].X.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateY", ParticleOrigins[i].Y.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateZ", ParticleOrigins[i].Z.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginX", "0.0");
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginY", "0.0");
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginZ", "0.0");
                }
            }

            #endregion

            #region Deal with subsets

            List<int> SubsetIDs = new List<int>();
            foreach (var i in ParticleSubset)
                if (!SubsetIDs.Contains(i))
                    SubsetIDs.Add(i);
            SubsetIDs.Sort();

            // For each subset, create a list of its particle IDs
            Dictionary<int, List<int>> SubsetParticleIDs = SubsetIDs.ToDictionary(subsetID => subsetID, subsetID => new List<int>());
            for (int i = 0; i < ParticleSubset.Length; i++)
                SubsetParticleIDs[ParticleSubset[i]].Add(i);
            foreach (var list in SubsetParticleIDs.Values)
                list.Sort();

            // Note where each subset starts and ends in a unified, sorted (by subset) particle ID list
            Dictionary<int, Tuple<int, int>> SubsetRanges = new Dictionary<int, Tuple<int, int>>();
            {
                int Start = 0;
                foreach (var pair in SubsetParticleIDs)
                {
                    SubsetRanges.Add(pair.Key, new Tuple<int, int>(Start, Start + pair.Value.Count));
                    Start += pair.Value.Count;
                }
            }

            List<int> SubsetContinuousIDs = new List<int>();
            foreach (var pair in SubsetParticleIDs)
                SubsetContinuousIDs.AddRange(pair.Value);

            // Reorder particle information to match the order of SubsetContinuousIDs
            ParticleOrigins = SubsetContinuousIDs.Select(i => ParticleOrigins[i]).ToArray();
            ParticleAngles = SubsetContinuousIDs.Select(i => ParticleAngles[i]).ToArray();
            ParticleSubset = SubsetContinuousIDs.Select(i => ParticleSubset[i]).ToArray();

            #endregion

            int CoarseSize = (int)Math.Round(size * ((float)CTF.PixelSize * 2 / resolution)) / 2 * 2;
            int3 CoarseDims = new int3(CoarseSize, CoarseSize, 1);

            // Create mask, CTF coords, reference projections, shifts
            Image Mask;
            Image CTFCoords = GetCTFCoords(CoarseSize, size);

            Dictionary<int, Image> SubsetProjections = new Dictionary<int, Image>();

            float3[] AnglesOri = Helper.GetHealpixAngles(healpixorder, symmetry);

            List<float3> Shifts = new List<float3>();
            for (int z = -1; z <= 1; z++)
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                        if (x * x + y * y + z * z <= 1)
                            Shifts.Add(new float3((float)x / CoarseSize * size * 0.5f, (float)y / CoarseSize * size * 0.5f, (float)z / CoarseSize * size * 0.5f));

            #region Preflight
            {
                #region Create mask with soft edge
                {
                    Image MaskBig = new Image(new int3(size, size, 1));
                    float MaskRadius = MainWindow.Options.ExportParticleRadius / (float)CTF.PixelSize;
                    float SoftEdge = 16f;

                    float[] MaskBigData = MaskBig.GetHost(Intent.Write)[0];
                    for (int y = 0; y < size; y++)
                    {
                        int yy = y - size / 2;
                        yy *= yy;
                        for (int x = 0; x < size; x++)
                        {
                            int xx = x - size / 2;
                            xx *= xx;
                            float R = (float)Math.Sqrt(xx + yy);

                            if (R <= MaskRadius)
                                MaskBigData[y * size + x] = 1;
                            else
                                MaskBigData[y * size + x] = (float)(Math.Cos(Math.Min(1, (R - MaskRadius) / SoftEdge) * Math.PI) * 0.5 + 0.5);
                        }
                    }
                    MaskBig.WriteMRC("d_maskbig.mrc");

                    Mask = MaskBig.AsScaled(new int2(CoarseSize, CoarseSize));
                    Mask.RemapToFT();

                    MaskBig.Dispose();
                }
                Mask.WriteMRC("d_masksmall.mrc");
                #endregion

                #region Create projections for each angular sample, adjusted for each tilt

                foreach (var subset in SubsetRanges)
                {
                    float3[] AnglesAlt = new float3[AnglesOri.Length * NTilts];
                    for (int t = 0; t < NTilts; t++)
                    {
                        for (int a = 0; a < AnglesOri.Length; a++)
                        {
                            float GridStep = 1f / (NTilts - 1);
                            float3 GridCoords = new float3(0.5f, 0.5f, t * GridStep);

                            Matrix3 ParticleMatrix = Matrix3.Euler(AnglesOri[a].X * Helper.ToRad,
                                                                   AnglesOri[a].Y * Helper.ToRad,
                                                                   AnglesOri[a].Z * Helper.ToRad);

                            Matrix3 TiltMatrix = Matrix3.Euler(0, -AnglesCorrect[t] * Helper.ToRad, 0);

                            Matrix3 CorrectionMatrix = Matrix3.RotateZ(GridAngleZ.GetInterpolated(GridCoords) * Helper.ToRad) *
                                                       Matrix3.RotateY(GridAngleY.GetInterpolated(GridCoords) * Helper.ToRad) *
                                                       Matrix3.RotateX(GridAngleX.GetInterpolated(GridCoords) * Helper.ToRad);

                            Matrix3 Rotation = CorrectionMatrix * TiltMatrix * ParticleMatrix;

                            AnglesAlt[a * NTilts + t] = Matrix3.EulerFromMatrix(Rotation);
                        }
                    }

                    Image ProjFT = references[subset.Key].Project(new int2(CoarseSize, CoarseSize), AnglesAlt, CoarseSize / 2);
                    Image CheckProj = ProjFT.AsIFFT();
                    CheckProj.RemapFromFT();
                    CheckProj.WriteMRC("d_proj.mrc");
                    CheckProj.Dispose();
                    ProjFT.FreeDevice();
                    SubsetProjections[subset.Key] = ProjFT;
                }

                #endregion
            }
            #endregion

            float3[] OptimizedShifts = new float3[NParticles];
            float3[] OptimizedAngles = new float3[NParticles];

            #region Correlate each particle with all projections

            foreach (var subset in SubsetRanges)
            {
                Image Projections = SubsetProjections[subset.Key];

                for (int p = subset.Value.Item1; p < subset.Value.Item2; p++)
                //Parallel.For(subset.Value.Item1, subset.Value.Item2, new ParallelOptions { MaxDegreeOfParallelism = 4 }, p =>
                {
                    Image ParticleImages;
                    Image ParticleCTFs = new Image(new int3(CoarseSize, CoarseSize, NTilts), true);
                    Image ParticleWeights;

                    // Positions the particles were extracted at/shifted to, to calculate effectively needed shifts later
                    float3[] ExtractedAt = new float3[NTilts];

                    float3 ParticleCoords = ParticleOrigins[p];
                    float3[] Positions = GetPositionInImages(ParticleCoords);

                    #region Create CTFs
                    {
                        float GridStep = 1f / (NTilts - 1);
                        CTFStruct[] Params = new CTFStruct[NTilts];
                        for (int t = 0; t < NTilts; t++)
                        {
                            decimal Defocus = (decimal)Positions[t].Z;
                            decimal DefocusDelta = (decimal)GridCTFDefocusDelta.GetInterpolated(new float3(0.5f, 0.5f, t * GridStep));
                            decimal DefocusAngle = (decimal)GridCTFDefocusAngle.GetInterpolated(new float3(0.5f, 0.5f, t * GridStep));

                            CTF CurrCTF = CTF.GetCopy();
                            CurrCTF.Defocus = Defocus;
                            CurrCTF.DefocusDelta = DefocusDelta;
                            CurrCTF.DefocusAngle = DefocusAngle;
                            CurrCTF.Scale = (decimal)Math.Cos(Angles[t] * Helper.ToRad);
                            CurrCTF.Bfactor = (decimal)-Dose[t] * 8;

                            Params[t] = CurrCTF.ToStruct();
                        }

                        GPU.CreateCTF(ParticleCTFs.GetDevice(Intent.Write),
                                      CTFCoords.GetDevice(Intent.Read),
                                      (uint)CoarseDims.ElementsFFT(),
                                      Params,
                                      false,
                                      (uint)NTilts);
                    }
                    #endregion

                    #region Weights are sums of the 2D CTFs
                    {
                        Image CTFAbs = new Image(ParticleCTFs.GetDevice(Intent.Read), ParticleCTFs.Dims, true);
                        CTFAbs.Abs();
                        ParticleWeights = CTFAbs.AsSum2D();
                        CTFAbs.Dispose();
                        {
                            float[] Weights = ParticleWeights.GetHost(Intent.ReadWrite)[0];
                            float Sum = Weights.Sum();
                            for (int i = 0; i < Weights.Length; i++)
                                Weights[i] /= Sum;
                        }
                    }
                    #endregion

                    #region Extract images

                    {
                        Image Extracted = new Image(new int3(size, size, NTilts));
                        float[][] ExtractedData = Extracted.GetHost(Intent.Write);

                        Parallel.For(0, NTilts, t =>
                        {
                            ExtractedAt[t] = new float3((int)Positions[t].X, (int)Positions[t].Y, 0);

                            Positions[t] -= size / 2;
                            int2 IntPosition = new int2((int)Positions[t].X, (int)Positions[t].Y);

                            float[] OriginalData;
                            lock (tiltStack)
                                OriginalData = tiltStack.GetHost(Intent.Read)[t];

                            float[] ImageData = ExtractedData[t];
                            for (int y = 0; y < size; y++)
                            {
                                int PosY = (y + IntPosition.Y + tiltStack.Dims.Y) % tiltStack.Dims.Y;
                                for (int x = 0; x < size; x++)
                                {
                                    int PosX = (x + IntPosition.X + tiltStack.Dims.X) % tiltStack.Dims.X;
                                    ImageData[y * size + x] = -OriginalData[PosY * tiltStack.Dims.X + PosX];
                                }
                            }
                        });

                        ParticleImages = Extracted.AsScaled(new int2(CoarseSize, CoarseSize));
                        ParticleImages.RemapToFT();
                        //Scaled.WriteMRC("d_particleimages.mrc");
                        Extracted.Dispose();
                    }
                    #endregion

                    Image ProjectionsConv = new Image(IntPtr.Zero, Projections.Dims, true, true);
                    GPU.MultiplyComplexSlicesByScalar(Projections.GetDevice(Intent.Read),
                                                      ParticleCTFs.GetDevice(Intent.Read),
                                                      ProjectionsConv.GetDevice(Intent.Write),
                                                      ParticleCTFs.ElementsComplex,
                                                      (uint)AnglesOri.Length);

                    Image ProjectionsReal = ProjectionsConv.AsIFFT();
                    GPU.NormalizeMasked(ProjectionsReal.GetDevice(Intent.Read),
                                        ProjectionsReal.GetDevice(Intent.Write),
                                        Mask.GetDevice(Intent.Read),
                                        (uint)ProjectionsReal.ElementsSliceReal,
                                        (uint)ProjectionsReal.Dims.Z);
                    //ProjectionsReal.WriteMRC("d_projconv.mrc");
                    ProjectionsConv.Dispose();

                    #region For each particle offset, correlate each tilt image with all reference projection

                    float[][] AllResults = new float[Shifts.Count][];
                    //for (int s = 0; s < Shifts.Count; s++)
                    Parallel.For(0, Shifts.Count, new ParallelOptions { MaxDegreeOfParallelism = 2 }, s =>
                    {
                        AllResults[s] = new float[AnglesOri.Length];

                        float3 ParticleCoordsAlt = ParticleOrigins[p] - Shifts[s];
                        float3[] PositionsAlt = GetPositionInImages(ParticleCoordsAlt);
                        float3[] PositionDiff = new float3[NTilts];
                        for (int t = 0; t < NTilts; t++)
                            PositionDiff[t] = (ExtractedAt[t] - PositionsAlt[t]) / size * CoarseSize;

                        float[] ShiftResults = new float[AnglesOri.Length];

                        GPU.TomoRealspaceCorrelate(ProjectionsReal.GetDevice(Intent.Read),
                                                   new int2(CoarseSize, CoarseSize),
                                                   (uint)AnglesOri.Length,
                                                   (uint)NTilts,
                                                   ParticleImages.GetDevice(Intent.Read),
                                                   ParticleCTFs.GetDevice(Intent.Read),
                                                   Mask.GetDevice(Intent.Read),
                                                   ParticleWeights.GetDevice(Intent.Read),
                                                   Helper.ToInterleaved(PositionDiff),
                                                   ShiftResults);

                        AllResults[s] = ShiftResults;
                    });

                    #endregion

                    ProjectionsReal.Dispose();
                    ParticleImages.Dispose();
                    ParticleCTFs.Dispose();
                    ParticleWeights.Dispose();

                    #region Find best offset/angle combination and store it in table

                    float3 BestShift = new float3(0, 0, 0);
                    float3 BestAngle = new float3(0, 0, 0);
                    float BestScore = -1e30f;

                    for (int s = 0; s < Shifts.Count; s++)
                        for (int a = 0; a < AnglesOri.Length; a++)
                            if (AllResults[s][a] > BestScore)
                            {
                                BestScore = AllResults[s][a];
                                BestAngle = AnglesOri[a];
                                BestShift = Shifts[s];
                            }

                    tableIn.SetRowValue(RowIndices[SubsetContinuousIDs[p]], "rlnOriginX", BestShift.X.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[SubsetContinuousIDs[p]], "rlnOriginY", BestShift.Y.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[SubsetContinuousIDs[p]], "rlnOriginZ", BestShift.Z.ToString(CultureInfo.InvariantCulture));

                    tableIn.SetRowValue(RowIndices[SubsetContinuousIDs[p]], "rlnAngleRot", BestAngle.X.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[SubsetContinuousIDs[p]], "rlnAngleTilt", BestAngle.Y.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[SubsetContinuousIDs[p]], "rlnAnglePsi", BestAngle.Z.ToString(CultureInfo.InvariantCulture));

                    tableIn.SetRowValue(RowIndices[SubsetContinuousIDs[p]], "rlnParticleSelectZScore", BestScore.ToString(CultureInfo.InvariantCulture));

                    OptimizedShifts[p] = BestShift;
                    OptimizedAngles[p] = BestAngle;

                    #endregion
                }

                // Dispose all projections for this subset, they won't be needed later
                SubsetProjections[subset.Key].Dispose();
            }
            #endregion

            CTFCoords.Dispose();

            #region Back-project with hopefully better parameters

            {
                CTFCoords = GetCTFCoords(size, size);

                foreach (var subsetRange in SubsetRanges)
                {
                    for (int p = subsetRange.Value.Item1; p < subsetRange.Value.Item2; p++)
                    {
                        float3 ParticleCoords = ParticleOrigins[p] - OptimizedShifts[p];

                        Image FullParticleImages = GetSubtomoImages(tiltStack, size, ParticleCoords, true);
                        Image FullParticleCTFs = GetSubtomoCTFs(ParticleCoords, CTFCoords);
                        //Image FullParticleCTFWeights = GetSubtomoCTFs(ParticleCoords, CTFCoords, true, true);

                        FullParticleImages.Multiply(FullParticleCTFs);
                        //FullParticleImages.Multiply(FullParticleCTFWeights);
                        FullParticleCTFs.Multiply(FullParticleCTFs);

                        float3[] FullParticleAngles = GetParticleAngleInImages(ParticleCoords, OptimizedAngles[p]);

                        outReconstructions[subsetRange.Key].BackProject(FullParticleImages, FullParticleCTFs, FullParticleAngles);

                        FullParticleImages.Dispose();
                        FullParticleCTFs.Dispose();
                        //FullParticleCTFWeights.Dispose();
                    }
                }

                CTFCoords.Dispose();
            }

            #endregion
        }

        public override void LoadMeta()
        {
            if (!File.Exists(XMLPath))
                return;

            using (Stream SettingsStream = File.OpenRead(XMLPath))
            {
                XPathDocument Doc = new XPathDocument(SettingsStream);
                XPathNavigator Reader = Doc.CreateNavigator();
                Reader.MoveToRoot();
                Reader.MoveToFirstChild();

                XPathNavigator NavAngles = Reader.SelectSingleNode("//Angles");
                if (NavAngles != null)
                    Angles = NavAngles.InnerXml.Split('\n').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();

                XPathNavigator NavDose = Reader.SelectSingleNode("//Dose");
                if (NavDose != null)
                    Dose = NavDose.InnerXml.Split('\n').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();

                {
                    TiltPS1D.Clear();
                    List<Tuple<int, float2[]>> TempPS1D = (from XPathNavigator NavPS1D in Reader.Select("//PS1D")
                                                           let ID = int.Parse(NavPS1D.GetAttribute("ID", ""))
                                                           let NewPS1D = NavPS1D.InnerXml.Split(';').Select(v =>
                                                           {
                                                               string[] Pair = v.Split('|');
                                                               return new float2(float.Parse(Pair[0], CultureInfo.InvariantCulture), float.Parse(Pair[1], CultureInfo.InvariantCulture));
                                                           }).ToArray()
                                                           select new Tuple<int, float2[]>(ID, NewPS1D)).ToList();

                    TempPS1D.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                    foreach (var ps1d in TempPS1D)
                        TiltPS1D.Add(ps1d.Item2);
                }

                {
                    TiltSimulatedBackground.Clear();
                    List<Tuple<int, Cubic1D>> TempBackground = (from XPathNavigator NavSimBackground in Reader.Select("//SimulatedBackground")
                                                                let ID = int.Parse(NavSimBackground.GetAttribute("ID", ""))
                                                                let NewBackground = new Cubic1D(NavSimBackground.InnerXml.Split(';').Select(v =>
                                                                {
                                                                    string[] Pair = v.Split('|');
                                                                    return new float2(float.Parse(Pair[0], CultureInfo.InvariantCulture), float.Parse(Pair[1], CultureInfo.InvariantCulture));
                                                                }).ToArray())
                                                                select new Tuple<int, Cubic1D>(ID, NewBackground)).ToList();

                    TempBackground.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                    foreach (var background in TempBackground)
                        TiltSimulatedBackground.Add(background.Item2);
                }

                {
                    TiltSimulatedScale.Clear();
                    List<Tuple<int, Cubic1D>> TempScale = (from XPathNavigator NavSimScale in Reader.Select("//SimulatedScale")
                                                           let ID = int.Parse(NavSimScale.GetAttribute("ID", ""))
                                                           let NewScale = new Cubic1D(NavSimScale.InnerXml.Split(';').Select(v =>
                                                           {
                                                               string[] Pair = v.Split('|');
                                                               return new float2(float.Parse(Pair[0], CultureInfo.InvariantCulture), float.Parse(Pair[1], CultureInfo.InvariantCulture));
                                                           }).ToArray())
                                                           select new Tuple<int, Cubic1D>(ID, NewScale)).ToList();

                    TempScale.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                    foreach (var scale in TempScale)
                        TiltSimulatedScale.Add(scale.Item2);
                }

                XPathNavigator NavCTF = Reader.SelectSingleNode("//CTF");
                if (NavCTF != null)
                    CTF.Load(NavCTF);

                XPathNavigator NavGridCTF = Reader.SelectSingleNode("//GridCTF");
                if (NavGridCTF != null)
                    GridCTF = CubicGrid.Load(NavGridCTF);

                XPathNavigator NavGridCTFDefocusDelta = Reader.SelectSingleNode("//GridCTFDefocusDelta");
                if (NavGridCTFDefocusDelta != null)
                    GridCTFDefocusDelta = CubicGrid.Load(NavGridCTFDefocusDelta);

                XPathNavigator NavGridCTFDefocusAngle = Reader.SelectSingleNode("//GridCTFDefocusAngle");
                if (NavGridCTFDefocusAngle != null)
                    GridCTFDefocusAngle = CubicGrid.Load(NavGridCTFDefocusAngle);

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

                XPathNavigator NavLocalZ = Reader.SelectSingleNode("//GridLocalMovementZ");
                if (NavLocalZ != null)
                    GridLocalZ = CubicGrid.Load(NavLocalZ);

                XPathNavigator NavAngleX = Reader.SelectSingleNode("//GridAngleX");
                if (NavAngleX != null)
                    GridAngleX = CubicGrid.Load(NavAngleX);

                XPathNavigator NavAngleY = Reader.SelectSingleNode("//GridAngleY");
                if (NavAngleY != null)
                    GridAngleY = CubicGrid.Load(NavAngleY);

                XPathNavigator NavAngleZ = Reader.SelectSingleNode("//GridAngleZ");
                if (NavAngleZ != null)
                    GridAngleZ = CubicGrid.Load(NavAngleZ);

                XPathNavigator NavAngleWeights = Reader.SelectSingleNode("//GridAngleWeights");
                if (NavAngleWeights != null)
                    GridAngleWeights = CubicGrid.Load(NavAngleWeights);

                XPathNavigator NavDoseBfacs = Reader.SelectSingleNode("//GridDoseBfacs");
                if (NavDoseBfacs != null)
                    GridDoseBfacs = CubicGrid.Load(NavDoseBfacs);

                {
                    string AnglesInvertedString = Reader.GetAttribute("AreAnglesInverted", "");
                    if (AnglesInvertedString != null && AnglesInvertedString.Length > 0)
                        AreAnglesInverted = bool.Parse(AnglesInvertedString);

                    string WeightString = Reader.GetAttribute("Weight", "");
                    if (WeightString != null && WeightString.Length > 0)
                        GlobalWeight = float.Parse(WeightString, CultureInfo.InvariantCulture);

                    string BfacString = Reader.GetAttribute("Bfactor", "");
                    if (BfacString != null && BfacString.Length > 0)
                        GlobalBfactor = float.Parse(BfacString, CultureInfo.InvariantCulture);

                    string StatusString = Reader.GetAttribute("Status", "");
                    if (!string.IsNullOrEmpty(StatusString))
                    {
                        switch (StatusString)
                        {
                            case "Processed":
                                Status = ProcessingStatus.Processed;
                                break;
                            case "Outdated":
                                Status = ProcessingStatus.Outdated;
                                break;
                            case "Unprocessed":
                                Status = ProcessingStatus.Unprocessed;
                                break;
                            case "Skip":
                                Status = ProcessingStatus.Skip;
                                break;
                        }
                    }
                }
            }
        }

        public override void SaveMeta()
        {
            using (XmlTextWriter Writer = new XmlTextWriter(XMLPath, Encoding.Unicode))
            {
                Writer.Formatting = Formatting.Indented;
                Writer.IndentChar = '\t';
                Writer.Indentation = 1;
                Writer.WriteStartDocument();
                Writer.WriteStartElement("TiltSeries");

                Writer.WriteAttributeString("Status", Status.ToString());
                Writer.WriteAttributeString("AreAnglesInverted", AreAnglesInverted.ToString());
                Writer.WriteAttributeString("Weight", GlobalWeight.ToString(CultureInfo.InvariantCulture));
                Writer.WriteAttributeString("Bfactor", GlobalBfactor.ToString(CultureInfo.InvariantCulture));

                Writer.WriteStartElement("Angles");
                Writer.WriteString(string.Join("\n", Angles.Select(v => v.ToString(CultureInfo.InvariantCulture))));
                Writer.WriteEndElement();

                Writer.WriteStartElement("Dose");
                Writer.WriteString(string.Join("\n", Dose.Select(v => v.ToString(CultureInfo.InvariantCulture))));
                Writer.WriteEndElement();

                foreach (float2[] ps1d in TiltPS1D)
                {
                    Writer.WriteStartElement("PS1D");
                    XMLHelper.WriteAttribute(Writer, "ID", TiltPS1D.IndexOf(ps1d));
                    Writer.WriteString(string.Join(";", ps1d.Select(v => v.X.ToString(CultureInfo.InvariantCulture) + "|" + v.Y.ToString(CultureInfo.InvariantCulture))));
                    Writer.WriteEndElement();
                }

                foreach (Cubic1D simulatedBackground in TiltSimulatedBackground)
                {
                    Writer.WriteStartElement("SimulatedBackground");
                    XMLHelper.WriteAttribute(Writer, "ID", TiltSimulatedBackground.IndexOf(simulatedBackground));
                    Writer.WriteString(string.Join(";",
                                                   simulatedBackground.Data.Select(v => v.X.ToString(CultureInfo.InvariantCulture) +
                                                                                        "|" +
                                                                                        v.Y.ToString(CultureInfo.InvariantCulture))));
                    Writer.WriteEndElement();
                }

                foreach (Cubic1D simulatedScale in TiltSimulatedScale)
                {
                    Writer.WriteStartElement("SimulatedScale");
                    XMLHelper.WriteAttribute(Writer, "ID", TiltSimulatedScale.IndexOf(simulatedScale));
                    Writer.WriteString(string.Join(";",
                                                   simulatedScale.Data.Select(v => v.X.ToString(CultureInfo.InvariantCulture) +
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

                Writer.WriteStartElement("GridCTFDefocusDelta");
                GridCTFDefocusDelta.Save(Writer);
                Writer.WriteEndElement();

                Writer.WriteStartElement("GridCTFDefocusAngle");
                GridCTFDefocusAngle.Save(Writer);
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

                Writer.WriteStartElement("GridLocalMovementZ");
                GridLocalZ.Save(Writer);
                Writer.WriteEndElement();

                Writer.WriteStartElement("GridAngleX");
                GridAngleX.Save(Writer);
                Writer.WriteEndElement();

                Writer.WriteStartElement("GridAngleY");
                GridAngleY.Save(Writer);
                Writer.WriteEndElement();

                Writer.WriteStartElement("GridAngleZ");
                GridAngleZ.Save(Writer);
                Writer.WriteEndElement();

                Writer.WriteStartElement("GridAngleWeights");
                GridAngleWeights.Save(Writer);
                Writer.WriteEndElement();

                Writer.WriteStartElement("GridDoseBfacs");
                GridDoseBfacs.Save(Writer);
                Writer.WriteEndElement();

                Writer.WriteEndElement();
                Writer.WriteEndDocument();
            }
        }
    }
}
