using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using Warp.Tools;

namespace Warp
{
    public class ProcessingOptions : DataBase
    {
        #region General

        private decimal _PixelMin = 1M;
        public decimal PixelMin
        {
            get { return _PixelMin; }
            set
            {
                if (value != _PixelMin)
                {
                    _PixelMin = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _PixelMax = 1M;
        public decimal PixelMax
        {
            get { return _PixelMax; }
            set
            {
                if (value != _PixelMax)
                {
                    _PixelMax = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _PixelAngle = 0M;
        public decimal PixelAngle
        {
            get { return _PixelAngle; }
            set
            {
                if (value != _PixelAngle)
                {
                    _PixelAngle = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _DetectorPixel = 5M;
        public decimal DetectorPixel
        {
            get { return _DetectorPixel; }
            set { if (value != _DetectorPixel) { _DetectorPixel = value; OnPropertyChanged(); } }
        }

        #endregion

        #region Preprocessing

        private string _GainPath = "";

        public string GainPath
        {
            get { return _GainPath; }
            set
            {
                if (value != _GainPath)
                {
                    _GainPath = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _CorrectGain = false;

        public bool CorrectGain
        {
            get { return _CorrectGain; }
            set
            {
                if (value != _CorrectGain)
                {
                    _CorrectGain = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _BinTimes = 0;
        public decimal BinTimes
        {
            get { return _BinTimes; }
            set { if (value != _BinTimes) { _BinTimes = value; OnPropertyChanged(); } }
        }

        #endregion

        #region Use particles

        private string _DataStarPath = "";
        public string DataStarPath
        {
            get { return _DataStarPath; }
            set { if (value != _DataStarPath) { _DataStarPath = value; OnPropertyChanged(); } }
        }

        private string _ModelStarPath = "";
        public string ModelStarPath
        {
            get { return _ModelStarPath; }
            set { if (value != _ModelStarPath) { _ModelStarPath = value; OnPropertyChanged(); } }
        }

        private string _ReferencePath = "";
        public string ReferencePath
        {
            get { return _ReferencePath; }
            set { if (value != _ReferencePath) { _ReferencePath = value; OnPropertyChanged(); } }
        }

        private string _MaskPath = "";
        public string MaskPath
        {
            get { return _MaskPath; }
            set { if (value != _MaskPath) { _MaskPath = value; OnPropertyChanged(); } }
        }

        private int _ProjectionOversample = 2;
        public int ProjectionOversample
        {
            get { return _ProjectionOversample; }
            set { if (value != _ProjectionOversample) { _ProjectionOversample = value; OnPropertyChanged(); } }
        }

        #endregion

        #region CTF

        private bool _ProcessCTF = true;
        public bool ProcessCTF
        {
            get { return _ProcessCTF; }
            set { if (value != _ProcessCTF) { _ProcessCTF = value; OnPropertyChanged(); } }
        }

        private int _CTFWindow = 512;
        public int CTFWindow
        {
            get { return _CTFWindow; }
            set
            {
                if (value != _CTFWindow)
                {
                    _CTFWindow = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _CTFRangeMin = 0.05M;
        public decimal CTFRangeMin
        {
            get { return _CTFRangeMin; }
            set
            {
                if (value != _CTFRangeMin)
                {
                    _CTFRangeMin = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _CTFRangeMax = 1M;
        public decimal CTFRangeMax
        {
            get { return _CTFRangeMax; }
            set
            {
                if (value != _CTFRangeMax)
                {
                    _CTFRangeMax = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _CTFMinQuality = 0.8M;
        public decimal CTFMinQuality
        {
            get { return _CTFMinQuality; }
            set
            {
                if (value != _CTFMinQuality)
                {
                    _CTFMinQuality = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _CTFVoltage = 300;
        public int CTFVoltage
        {
            get { return _CTFVoltage; }
            set
            {
                if (value != _CTFVoltage)
                {
                    _CTFVoltage = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _CTFCs = 2.1M;
        public decimal CTFCs
        {
            get { return _CTFCs; }
            set
            {
                if (value != _CTFCs)
                {
                    _CTFCs = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _CTFAmplitude = 0.07M;
        public decimal CTFAmplitude
        {
            get { return _CTFAmplitude; }
            set
            {
                if (value != _CTFAmplitude)
                {
                    _CTFAmplitude = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _CTFDoPhase = true;
        public bool CTFDoPhase
        {
            get { return _CTFDoPhase; }
            set { if (value != _CTFDoPhase) { _CTFDoPhase = value; OnPropertyChanged(); } }
        }

        private decimal _CTFAstigmatism = 0M;
        public decimal CTFAstigmatism
        {
            get { return _CTFAstigmatism; }
            set
            {
                if (value != _CTFAstigmatism)
                {
                    _CTFAstigmatism = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _CTFAstigmatismAngle = 0M;
        public decimal CTFAstigmatismAngle
        {
            get { return _CTFAstigmatismAngle; }
            set
            {
                if (value != _CTFAstigmatismAngle)
                {
                    _CTFAstigmatismAngle = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _CTFZMin = 0.3M;
        public decimal CTFZMin
        {
            get { return _CTFZMin; }
            set
            {
                if (value != _CTFZMin)
                {
                    _CTFZMin = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _CTFZMax = 3M;
        public decimal CTFZMax
        {
            get { return _CTFZMax; }
            set
            {
                if (value != _CTFZMax)
                {
                    _CTFZMax = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _ProcessParticleCTF = false;
        public bool ProcessParticleCTF
        {
            get { return _ProcessParticleCTF; }
            set { if (value != _ProcessParticleCTF) { _ProcessParticleCTF = value; OnPropertyChanged(); } }
        }

        #endregion

        #region Movement

        private bool _ProcessMovement = true;
        public bool ProcessMovement
        {
            get { return _ProcessMovement; }
            set { if (value != _ProcessMovement) { _ProcessMovement = value; OnPropertyChanged(); } }
        }

        private decimal _MovementRangeMin = 0.002M;

        public decimal MovementRangeMin
        {
            get { return _MovementRangeMin; }
            set
            {
                if (value != _MovementRangeMin)
                {
                    _MovementRangeMin = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _MovementRangeMax = 0.125M;

        public decimal MovementRangeMax
        {
            get { return _MovementRangeMax; }
            set
            {
                if (value != _MovementRangeMax)
                {
                    _MovementRangeMax = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _MovementBfactor = 0;
        public decimal MovementBfactor
        {
            get { return _MovementBfactor; }
            set { if (value != _MovementBfactor) { _MovementBfactor = value; OnPropertyChanged(); } }
        }

        private decimal _MovementGridReduction = 2;
        public decimal MovementGridReduction
        {
            get { return _MovementGridReduction; }
            set { if (value != _MovementGridReduction) { _MovementGridReduction = value; OnPropertyChanged(); } }
        }

        private bool _ProcessParticleShift = false;
        public bool ProcessParticleShift
        {
            get { return _ProcessParticleShift; }
            set { if (value != _ProcessParticleShift) { _ProcessParticleShift = value; OnPropertyChanged(); } }
        }

        #endregion

        #region Grids

        private int _GridCTFX = 1;

        public int GridCTFX
        {
            get { return _GridCTFX; }
            set
            {
                if (value != _GridCTFX)
                {
                    _GridCTFX = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _GridCTFY = 1;

        public int GridCTFY
        {
            get { return _GridCTFY; }
            set
            {
                if (value != _GridCTFY)
                {
                    _GridCTFY = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _GridCTFZ = 1;

        public int GridCTFZ
        {
            get { return _GridCTFZ; }
            set
            {
                if (value != _GridCTFZ)
                {
                    _GridCTFZ = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _GridMoveX = 1;

        public int GridMoveX
        {
            get { return _GridMoveX; }
            set
            {
                if (value != _GridMoveX)
                {
                    _GridMoveX = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _GridMoveY = 1;

        public int GridMoveY
        {
            get { return _GridMoveY; }
            set
            {
                if (value != _GridMoveY)
                {
                    _GridMoveY = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _GridMoveZ = 1;

        public int GridMoveZ
        {
            get { return _GridMoveZ; }
            set
            {
                if (value != _GridMoveZ)
                {
                    _GridMoveZ = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Postprocessing

        private bool _PostAverage = true;
        public bool PostAverage
        {
            get { return _PostAverage; }
            set
            {
                if (value != _PostAverage)
                {
                    _PostAverage = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _PostStack = false;
        public bool PostStack
        {
            get { return _PostStack; }
            set
            {
                if (value != _PostStack)
                {
                    _PostStack = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _PostStackGroupSize = 1;
        public int PostStackGroupSize
        {
            get { return _PostStackGroupSize; }
            set
            {
                if (value != _PostStackGroupSize)
                {
                    _PostStackGroupSize = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Auxiliary

        public decimal BinningMult => (decimal)Math.Pow(2.0, (double)BinTimes);

        public decimal PixelSizeAverage => (PixelMin + PixelMax) * 0.5M;

        public decimal PixelSizeDelta => PixelMax - PixelMin;

        public decimal BinnedPixelSizeAverage => PixelSizeAverage * BinningMult;

        public decimal BinnedPixelSizeDelta => PixelSizeDelta * BinningMult;

        #endregion

        public bool NeedsReprocess(ProcessingOptions r)
        {
            return false;
        }

        public bool NeedsReprocessInput(ProcessingOptions r)
        {
            bool Equal = r.PixelMin == PixelMin &&
                         r.PixelMax == PixelMax &&
                         r.PixelAngle == PixelAngle &&
                         r.DetectorPixel == DetectorPixel &&
                         r.BinTimes == BinTimes;

            return !Equal;
        }

        public bool NeedsReprocessPre(ProcessingOptions r)
        {
            bool Equal = !NeedsReprocessInput(r);

            if (r.CorrectGain)
                Equal = r.CorrectGain == CorrectGain &&
                        r.GainPath == GainPath;

            return !Equal;
        }

        public bool NeedsReprocessParticles(ProcessingOptions r)
        {
            bool Equal = r.DataStarPath == DataStarPath &&
                         r.ModelStarPath == ModelStarPath &&
                         r.ReferencePath == ReferencePath &&
                         r.MaskPath == MaskPath &&
                         r.ProjectionOversample == ProjectionOversample;

            return !Equal;
        }

        public bool NeedsReprocessCTF(ProcessingOptions r)
        {
            bool Equal = !NeedsReprocessInput(r) &&
                         !NeedsReprocessPre(r);

            if (r.ProcessCTF)
            {
                Equal = Equal &&
                        r.CTFWindow == CTFWindow &&
                        r.CTFRangeMin == CTFRangeMin &&
                        r.CTFRangeMax == CTFRangeMax &&
                        r.CTFVoltage == CTFVoltage &&
                        r.CTFCs == CTFCs &&
                        r.CTFAmplitude == CTFAmplitude &&
                        r.CTFDoPhase == CTFDoPhase &&
                        r.CTFAstigmatism == CTFAstigmatism &&
                        r.CTFAstigmatismAngle == CTFAstigmatismAngle &&
                        r.CTFZMin == CTFZMin &&
                        r.CTFZMax == CTFZMax &&
                        r.GridCTFX == GridCTFX &&
                        r.GridCTFY == GridCTFY &&
                        r.GridCTFZ == GridCTFZ;

                if (r.ProcessParticleCTF)
                    Equal = Equal &&
                            r.ProcessParticleCTF == ProcessParticleCTF &&
                            !NeedsReprocessParticles(r);
            }

            return !Equal;
        }

        public bool NeedsReprocessMovement(ProcessingOptions r)
        {
            bool Equal = !NeedsReprocessInput(r) &&
                         !NeedsReprocessPre(r);

            if (r.ProcessMovement)
            {
                Equal = Equal &&
                        r.MovementRangeMin == MovementRangeMin &&
                        r.MovementRangeMax == MovementRangeMax &&
                        r.MovementBfactor == MovementBfactor &&
                        r.GridMoveX == GridMoveX &&
                        r.GridMoveY == GridMoveY &&
                        r.GridMoveZ == GridMoveZ &&
                        r.MovementGridReduction == MovementGridReduction;

                if (r.ProcessParticleShift)
                    Equal = Equal &&
                            r.ProcessParticleShift == ProcessParticleShift &&
                            !NeedsReprocessParticles(r);
            }

            return !Equal;
        }

        public bool NeedsReprocessPost(ProcessingOptions r)
        {
            bool Equal = !NeedsReprocessInput(r) &&
                         !NeedsReprocessPre(r) &&
                         !NeedsReprocessCTF(r) &&
                         !NeedsReprocessMovement(r);

            Equal &= !r.PostAverage || PostAverage;
            Equal &= !r.PostStack || (PostStack && r.PostStackGroupSize == PostStackGroupSize);

            return !Equal;
        }

        public void Save(XmlTextWriter writer)
        {
            XMLHelper.WriteParamNode(writer, "PixelMin", PixelMin);
            XMLHelper.WriteParamNode(writer, "PixelMax", PixelMax);
            XMLHelper.WriteParamNode(writer, "PixelAngle", PixelAngle);
            XMLHelper.WriteParamNode(writer, "DetectorPixel", DetectorPixel);
            XMLHelper.WriteParamNode(writer, "BinTimes", BinTimes);

            XMLHelper.WriteParamNode(writer, "GainPath", GainPath);
            XMLHelper.WriteParamNode(writer, "CorrectGain", CorrectGain);

            XMLHelper.WriteParamNode(writer, "DataStarPath", DataStarPath);
            XMLHelper.WriteParamNode(writer, "ModelStarPath", ModelStarPath);
            XMLHelper.WriteParamNode(writer, "ReferencePath", ReferencePath);
            XMLHelper.WriteParamNode(writer, "MaskPath", MaskPath);
            XMLHelper.WriteParamNode(writer, "ProjectionOversample", ProjectionOversample);

            XMLHelper.WriteParamNode(writer, "ProcessCTF", ProcessCTF);
            XMLHelper.WriteParamNode(writer, "CTFWindow", CTFWindow);
            XMLHelper.WriteParamNode(writer, "CTFRangeMin", CTFRangeMin);
            XMLHelper.WriteParamNode(writer, "CTFRangeMax", CTFRangeMax);
            XMLHelper.WriteParamNode(writer, "CTFMinQuality", CTFMinQuality);
            XMLHelper.WriteParamNode(writer, "CTFVoltage", CTFVoltage);
            XMLHelper.WriteParamNode(writer, "CTFCs", CTFCs);
            XMLHelper.WriteParamNode(writer, "CTFAmplitude", CTFAmplitude);
            XMLHelper.WriteParamNode(writer, "CTFDoPhase", CTFDoPhase);
            XMLHelper.WriteParamNode(writer, "CTFAstigmatism", CTFAstigmatism);
            XMLHelper.WriteParamNode(writer, "CTFAstigmatismAngle", CTFAstigmatismAngle);
            XMLHelper.WriteParamNode(writer, "CTFZMin", CTFZMin);
            XMLHelper.WriteParamNode(writer, "CTFZMax", CTFZMax);
            XMLHelper.WriteParamNode(writer, "ProcessParticleCTF", ProcessParticleCTF);

            XMLHelper.WriteParamNode(writer, "ProcessMovement", ProcessMovement);
            XMLHelper.WriteParamNode(writer, "MovementRangeMin", MovementRangeMin);
            XMLHelper.WriteParamNode(writer, "MovementRangeMax", MovementRangeMax);
            XMLHelper.WriteParamNode(writer, "MovementBfactor", MovementBfactor);
            XMLHelper.WriteParamNode(writer, "MovementGridReduction", MovementGridReduction);
            XMLHelper.WriteParamNode(writer, "ProcessParticleShift", ProcessParticleShift);

            XMLHelper.WriteParamNode(writer, "GridCTFX", GridCTFX);
            XMLHelper.WriteParamNode(writer, "GridCTFY", GridCTFY);
            XMLHelper.WriteParamNode(writer, "GridCTFZ", GridCTFZ);
            XMLHelper.WriteParamNode(writer, "GridMoveX", GridMoveX);
            XMLHelper.WriteParamNode(writer, "GridMoveY", GridMoveY);
            XMLHelper.WriteParamNode(writer, "GridMoveZ", GridMoveZ);

            XMLHelper.WriteParamNode(writer, "PostAverage", PostAverage);
            XMLHelper.WriteParamNode(writer, "PostStack", PostStack);
            XMLHelper.WriteParamNode(writer, "PostStackGroupSize", PostStackGroupSize);
        }

        public void Load(XPathNavigator reader)
        {
            PixelMin = XMLHelper.LoadParamNode(reader, "PixelMin", PixelMin);
            PixelMax = XMLHelper.LoadParamNode(reader, "PixelMax", PixelMax);
            PixelAngle = XMLHelper.LoadParamNode(reader, "PixelAngle", PixelAngle);
            DetectorPixel = XMLHelper.LoadParamNode(reader, "DetectorPixel", DetectorPixel);
            BinTimes = XMLHelper.LoadParamNode(reader, "BinTimes", BinTimes);

            GainPath = XMLHelper.LoadParamNode(reader, "GainPath", "");
            CorrectGain = XMLHelper.LoadParamNode(reader, "CorrectGain", CorrectGain);

            DataStarPath = XMLHelper.LoadParamNode(reader, "DataStarPath", "");
            ModelStarPath = XMLHelper.LoadParamNode(reader, "ModelStarPath", "");
            ReferencePath = XMLHelper.LoadParamNode(reader, "ReferencePath", "");
            MaskPath = XMLHelper.LoadParamNode(reader, "MaskPath", "");
            ProjectionOversample = XMLHelper.LoadParamNode(reader, "ProjectionOversample", ProjectionOversample);

            ProcessCTF = XMLHelper.LoadParamNode(reader, "ProcessCTF", ProcessCTF);
            CTFWindow = XMLHelper.LoadParamNode(reader, "CTFWindow", CTFWindow);
            CTFRangeMin = XMLHelper.LoadParamNode(reader, "CTFRangeMin", CTFRangeMin);
            CTFRangeMax = XMLHelper.LoadParamNode(reader, "CTFRangeMax", CTFRangeMax);
            CTFMinQuality = XMLHelper.LoadParamNode(reader, "CTFMinQuality", CTFMinQuality);
            CTFVoltage = XMLHelper.LoadParamNode(reader, "CTFVoltage", CTFVoltage);
            CTFCs = XMLHelper.LoadParamNode(reader, "CTFCs", CTFCs);
            CTFAmplitude = XMLHelper.LoadParamNode(reader, "CTFAmplitude", CTFAmplitude);
            CTFDoPhase = XMLHelper.LoadParamNode(reader, "CTFDoPhase", CTFDoPhase);
            CTFAstigmatism = XMLHelper.LoadParamNode(reader, "CTFAstigmatism", CTFAstigmatism);
            CTFAstigmatismAngle = XMLHelper.LoadParamNode(reader, "CTFAstigmatismAngle", CTFAstigmatismAngle);
            CTFZMin = XMLHelper.LoadParamNode(reader, "CTFZMin", CTFZMin);
            CTFZMax = XMLHelper.LoadParamNode(reader, "CTFZMax", CTFZMax);
            ProcessParticleCTF = XMLHelper.LoadParamNode(reader, "ProcessParticleCTF", ProcessParticleCTF);

            ProcessMovement = XMLHelper.LoadParamNode(reader, "ProcessMovement", ProcessMovement);
            MovementRangeMin = XMLHelper.LoadParamNode(reader, "MovementRangeMin", MovementRangeMin);
            MovementRangeMax = XMLHelper.LoadParamNode(reader, "MovementRangeMax", MovementRangeMax);
            MovementBfactor = XMLHelper.LoadParamNode(reader, "MovementBfactor", MovementBfactor);
            MovementGridReduction = XMLHelper.LoadParamNode(reader, "MovementGridReduction", MovementGridReduction);
            ProcessParticleShift = XMLHelper.LoadParamNode(reader, "ProcessParticleShift", ProcessParticleShift);

            GridCTFX = XMLHelper.LoadParamNode(reader, "GridCTFX", GridCTFX);
            GridCTFY = XMLHelper.LoadParamNode(reader, "GridCTFY", GridCTFY);
            GridCTFZ = XMLHelper.LoadParamNode(reader, "GridCTFZ", GridCTFZ);
            GridMoveX = XMLHelper.LoadParamNode(reader, "GridMoveX", GridMoveX);
            GridMoveY = XMLHelper.LoadParamNode(reader, "GridMoveY", GridMoveY);
            GridMoveZ = XMLHelper.LoadParamNode(reader, "GridMoveZ", GridMoveZ);

            PostAverage = XMLHelper.LoadParamNode(reader, "PostAverage", PostAverage);
            PostStack = XMLHelper.LoadParamNode(reader, "PostStack", PostStack);
            PostStackGroupSize = XMLHelper.LoadParamNode(reader, "PostStackGroupSize", PostStackGroupSize);
        }
    }
}
