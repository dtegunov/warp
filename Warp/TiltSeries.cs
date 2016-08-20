using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using Accord.Math.Optimization;
using Warp.Headers;
using Warp.Tools;

namespace Warp
{
    public class TiltSeries : Movie
    {
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

        public float[] AnglesCorrect => Angles.Select(v => AreAnglesInverted ? -v : v).ToArray();

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

        public int[] IndicesSortedDose
        {
            get
            {
                if (Dose == null)
                    return null;

                List<int> Sorted = new List<int>(Dose.Length);
                for (int i = 0; i < Dose.Length; i++)
                    Sorted.Add(i);

                Sorted.Sort((a, b) => Dose[a].CompareTo(Dose[b]));

                return Sorted.ToArray();
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
                if (File.Exists(DirectoryName + RootName + ".meta"))
                {
                    using (TextReader Reader = new StreamReader(File.OpenRead(DirectoryName + RootName + ".meta")))
                    {
                        List<float> TempAngles = new List<float>();
                        List<float> TempDose = new List<float>();

                        string Line = null;
                        while ((Line = Reader.ReadLine()) != null)
                        {
                            if (Line[0] == '#')
                                continue;

                            string[] Parts = Line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (Parts.Length != 2)
                                continue;

                            TempAngles.Add(float.Parse(Parts[0], CultureInfo.InvariantCulture));
                            TempDose.Add(float.Parse(Parts[1], CultureInfo.InvariantCulture));
                        }

                        if (TempAngles.Count == 0)
                            throw new Exception("Metadata must contain 2 values per tilt: angle, dose.");
                        if (TempAngles.Count != Dimensions.Z)
                            throw new Exception("Metadata must contain one line for each image in tilt series.");

                        Angles = TempAngles.ToArray();
                        Dose = TempDose.ToArray();
                    }
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
                Image AngleImage = new Image(StackData[AngleID], originalStack.Dims.Slice());

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
            float MeanAstigmatismDelta = MathHelper.Mean(MathHelper.WithinNStdFromMedian(AstigmatismDeltas.ToArray(), 1f));
            float MeanAstigmatismAngle = MathHelper.Mean(MathHelper.WithinNStdFromMedian(AstigmatismAngles.ToArray(), 1f));

            #endregion

            #region Fit every tilt

            for (int i = 0; i < NTilts; i++)
            {
                int AngleID = IndicesSortedDose[i];
                Image AngleImage = new Image(StackData[AngleID], originalStack.Dims.Slice());

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
                    float Distance = originalHeader.Dimensions.X * (float)MainWindow.Options.BinnedPixelSize;
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

                if (Unbiased1 > Unbiased2)
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
            for (int i = 0; i < NTilts; i++)
            {
                TiltPS1D.Add(FitPS1D[i]);
                TiltSimulatedBackground.Add(new Cubic1D(FitBackground[i].Data.Select(v => new float2(v.X, 0)).ToArray()));
                TiltSimulatedScale.Add(FitScale[i]);
            }

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
                    ZMin = Math.Max(ZMin, (float)previousCTF.Defocus - 0.5f);
                    ZMax = Math.Min(ZMax, (float)previousCTF.Defocus + 0.5f);
                    PhaseMin = Math.Max(PhaseMin, (float)previousCTF.PhaseShift - 0.3f);
                    PhaseMax = Math.Min(PhaseMax, (float)previousCTF.PhaseShift + 0.3f);
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

                        float[] DefocusValues = GridCTF.GetInterpolatedNative(CTFSpectraGrid, new float3(DimsRegion.X / 2f / DimsImage.X, DimsRegion.Y / 2f / DimsImage.Y, 0));
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
                        float2[] ForPS1D = new float2[TempPS1D.Length];
                        for (int i = 0; i < ForPS1D.Length; i++)
                            ForPS1D[i] = new float2((float)i / DimsRegion.X, (float)Math.Round(RotationalAverageData[i], 4) + TempBackground.Interp((float)i / DimsRegion.X));
                        MathHelper.UnNaN(ForPS1D);
                        TempPS1D = ForPS1D;

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

            if (!Directory.Exists(ParticlesDir))
                Directory.CreateDirectory(ParticlesDir);
            if (!Directory.Exists(ParticleCTFDir))
                Directory.CreateDirectory(ParticleCTFDir);

            #endregion

            #region Get subtomo positions from table

            float3[] Origins = new float3[NParticles];
            {
                string[] ColumnPosX = tableIn.GetColumn("rlnCoordinateX");
                string[] ColumnPosY = tableIn.GetColumn("rlnCoordinateY");
                string[] ColumnPosZ = tableIn.GetColumn("rlnCoordinateZ");
                string[] ColumnOriginX = tableIn.GetColumn("rlnOriginX");
                string[] ColumnOriginY = tableIn.GetColumn("rlnOriginY");
                string[] ColumnOriginZ = tableIn.GetColumn("rlnOriginZ");

                for (int i = 0; i < NParticles; i++)
                {
                    float3 Pos = new float3(float.Parse(ColumnPosX[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosY[RowIndices[i]], CultureInfo.InvariantCulture),
                                            float.Parse(ColumnPosZ[RowIndices[i]], CultureInfo.InvariantCulture));
                    float3 Shift = new float3(float.Parse(ColumnOriginX[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginY[RowIndices[i]], CultureInfo.InvariantCulture),
                                              float.Parse(ColumnOriginZ[RowIndices[i]], CultureInfo.InvariantCulture));

                    Origins[i] = Pos - Shift;
                    //Origins[i] *= new float3(3838f / 959f, 3710f / 927f, 4f);

                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateX", Origins[i].X.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateY", Origins[i].Y.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnCoordinateZ", Origins[i].Z.ToString(CultureInfo.InvariantCulture));
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginX", "0.0");
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginY", "0.0");
                    tableIn.SetRowValue(RowIndices[i], "rlnOriginZ", "0.0");
                }
            }

            #endregion

            tiltStack.FreeDevice();

            int DevicesUsed = 0;

            int Nx = (tiltStack.Dims.X + 29) / 60;
            int Ny = (tiltStack.Dims.Y + 29) / 60;
            int Nz = (300 + 29) / 60;

            Origins = new float3[Nx * Ny * Nz];
            for (int z = 0, i = 0; z < Nz; z++)
                for (int y = 0; y < Ny; y++)
                    for (int x = 0; x < Nx; x++, i++)
                        Origins[i] = new float3(x * 60 + 30, y * 60 + 30, z * 60 + 30);
            NParticles = Origins.Length;
            
            Parallel.For(0, NParticles, new ParallelOptions() { MaxDegreeOfParallelism = 4 }, p =>
            //for (int p = 0; p < NParticles; p++)
            {
                lock (tiltStack)
                {
                    GPU.SetDevice(DevicesUsed % 2);
                    DevicesUsed++;
                }

                Image CTFCoords = GetCTFCoords(size);

                Image Subtomo, SubtomoCTF;
                GetSubtomo(tiltStack, Origins[p], CTFCoords, out Subtomo, out SubtomoCTF);
                Image SubtomoCropped = Subtomo.AsPadded(new int3(60, 60, 60));

                SubtomoCropped.WriteMRC(ParticlesDir + RootName + "_" + p.ToString("D5") + ".mrc");
                //SubtomoCTF.WriteMRC(ParticleCTFDir + RootName + "_" + p.ToString("D5") + ".mrc");
                SubtomoCropped.Dispose();

                lock (tableIn)
                {
                    //tableIn.SetRowValue(RowIndices[p], "rlnImageName", "particles/" + RootName + "_" + p.ToString("D5") + ".mrc");
                    //tableIn.SetRowValue(RowIndices[p], "rlnCtfImage", "particlectf/" + RootName + "_" + p.ToString("D5") + ".mrc");

                    //tableIn.Save("D:\\rubisco\\luisexported.star");
                }

                Subtomo?.Dispose();
                SubtomoCTF?.Dispose();

                CTFCoords.Dispose();

                lock (tiltStack)
                    DevicesUsed--;
            });
        }

        public void GetSubtomo(Image tiltStack, float3 coords, Image ctfCoords, out Image subtomo, out Image subtomoCTF)
        {
            int Size = ctfCoords.Dims.X;
            float3[] ImageAngles = GetAngleInImages(coords);

            Image ImagesFT = GetSubtomoImages(tiltStack, Size, coords);
            Image CTFs = GetSubtomoCTFs(coords, ctfCoords);

            ImagesFT.Multiply(CTFs);    // Weight and phase-flip image FTs by CTF, which still has its sign here
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
            subtomo = ProjSubtomo.Reconstruct(false);
            ProjSubtomo.Dispose();

            //Projector ProjCTF = new Projector(new int3(Size, Size, Size), 2);
            //lock (GPU.Sync)
            //    ProjCTF.BackProject(CTFsComplex, CTFs, ImageAngles);
            //subtomoCTF = ProjCTF.Reconstruct(true);
            //ProjCTF.Dispose();
            subtomoCTF = new Image(new int3(1, 1, 1));

            ImagesFT.Dispose();
            CTFs.Dispose();
            CTFsComplex.Dispose();
        }

        public float3[] GetPositionInImages(float3 coords)
        {
            float3[] Result = new float3[NTilts];

            float3 Center = new float3(VolumeDimensions.X / 2, VolumeDimensions.Y / 2, VolumeDimensions.Z / 2);

            float GridStep = 1f / (NTilts - 1);
            float3 GridCoords = new float3(coords.X / Dimensions.X, coords.Y / Dimensions.Y, 0.5f);
            float3 Centered = coords - Center;

            for (int t = 0; t < NTilts; t++)
            {
                float3 CurrCoords = new float3(GridCoords.X, GridCoords.Y, t * GridStep);

                Matrix3 TiltMatrix = Matrix3.Euler(0, -AnglesCorrect[t] * Helper.ToRad, 0);
                Matrix3 CorrectionMatrix = Matrix3.RotateZ(GridAngleZ.GetInterpolated(CurrCoords)) *
                                           Matrix3.RotateY(GridAngleY.GetInterpolated(CurrCoords)) *
                                           Matrix3.RotateX(GridAngleX.GetInterpolated(CurrCoords));

                Matrix3 Rotation = TiltMatrix * CorrectionMatrix;

                float3 Transformed = (Rotation * Centered) + Center;
                Transformed.Z -= Center.Z;
                Transformed.Z *= -1;

                float3 CorrectionStage = new float3(GridMovementX.GetInterpolated(CurrCoords), GridMovementY.GetInterpolated(CurrCoords), 0);

                Transformed = Transformed - CorrectionStage;

                Transformed.Z *= (float)MainWindow.Options.BinnedPixelSize * 1 / 1e4f;
                Transformed.Z += GridCTF.GetInterpolated(CurrCoords);

                Result[t] = Transformed;
            }

            return Result;
        }

        public float3[] GetAngleInImages(float3 coords)
        {
            float3[] Result = new float3[NTilts];

            float GridStep = 1f / (NTilts - 1);
            float3 GridCoords = new float3(coords.X / Dimensions.X, coords.Y / Dimensions.Y, 0.5f);

            for (int t = 0; t < NTilts; t++)
            {
                float3 CurrCoords = new float3(GridCoords.X, GridCoords.Y, t * GridStep);

                Matrix3 TiltMatrix = Matrix3.Euler(0, -AnglesCorrect[t] * Helper.ToRad, 0);
                Matrix3 CorrectionMatrix = Matrix3.RotateZ(GridAngleZ.GetInterpolated(CurrCoords)) *
                                           Matrix3.RotateY(GridAngleY.GetInterpolated(CurrCoords)) *
                                           Matrix3.RotateX(GridAngleX.GetInterpolated(CurrCoords));

                Matrix3 Rotation = TiltMatrix * CorrectionMatrix;

                Result[t] = Matrix3.EulerFromMatrix(Rotation);
            }

            return Result;
        }

        public Image GetSubtomoImages(Image tiltStack, int size, float3 coords)
        {
            float3[] ImagePositions = GetPositionInImages(coords);

            Image Result = new Image(new int3(size, size, NTilts));
            float[][] ResultData = Result.GetHost(Intent.Write);
            float3[] Shifts = new float3[NTilts];

            int3 DimsStack = tiltStack.Dims;

            for (int t = 0; t < NTilts; t++)
            {
                int2 IntPosition = new int2((int)ImagePositions[t].X - size / 2, (int)ImagePositions[t].Y - size / 2);
                float2 Residual = new float2(-(ImagePositions[t].X - size / 2 - IntPosition.X), -(ImagePositions[t].Y - size / 2 - IntPosition.Y));
                Residual -= size / 2;
                Shifts[t] = new float3(Residual);

                float[] OriginalData = tiltStack.GetHost(Intent.Read)[t];

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
            }

            GPU.NormParticles(Result.GetDevice(Intent.Read),
                              Result.GetDevice(Intent.Write),
                              Result.Dims.Slice(),
                              (uint)MainWindow.Options.ExportParticleRadius,
                              true,
                              (uint)NTilts);

            Result.ShiftSlices(Shifts);

            Image ResultFT = Result.AsFFT();
            Result.Dispose();

            return ResultFT;
        }

        public Image GetSubtomoCTFs(float3 coords, Image ctfCoords)
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
                CurrCTF.Defocus = Defocus;
                CurrCTF.DefocusDelta = DefocusDelta;
                CurrCTF.DefocusAngle = DefocusAngle;
                CurrCTF.PixelSize *= 4;
                //CurrCTF.Scale = (decimal)Math.Cos(Angles[t] * Helper.ToRad);
                //CurrCTF.Bfactor = (decimal)-Dose[t] * 8;

                Params[t] = CurrCTF.ToStruct();
            }

            Image Result = new Image(IntPtr.Zero, new int3(ctfCoords.Dims.X, ctfCoords.Dims.Y, NTilts), true);
            GPU.CreateCTF(Result.GetDevice(Intent.Write), ctfCoords.GetDevice(Intent.Read), (uint)Result.ElementsSliceReal, Params, false, (uint)NTilts);

            return Result;
        }

        public Image GetCTFCoords(int size)
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

                        float xs = xx / (float)size;
                        float ys = yy / (float)size;
                        float r = (float)Math.Sqrt(xs * xs + ys * ys);
                        float angle = (float)(Math.Atan2(yy, xx));

                        CTFCoordsData[y * (size / 2 + 1) + x] = new float2(r, angle);
                    }

                CTFCoords = new Image(CTFCoordsData, new int3(size, size, 1), true);
            }

            return CTFCoords;
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

                {
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

                    string AnglesInvertedString = Reader.GetAttribute("AreAnglesInverted", "");
                    if (AnglesInvertedString != null && AnglesInvertedString.Length > 0)
                        AreAnglesInverted = bool.Parse(AnglesInvertedString);
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

                Writer.WriteEndElement();
                Writer.WriteEndDocument();
            }
        }
    }
}
