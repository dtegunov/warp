using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using Warp.Tools;

namespace Warp
{
    public class Options : DataBase
    {
        #region IO

        private string _InputFolder = "";
        public string InputFolder
        {
            get { return _InputFolder; }
            set
            {
                if (value != _InputFolder)
                {
                    _InputFolder = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _InputExtension = "*.mrc";
        public string InputExtension
        {
            get { return _InputExtension; }
            set
            {
                if (value != _InputExtension)
                {
                    _InputExtension = value;
                    OnPropertyChanged();
                    InputExtensionMRC = value == "*.mrc";
                    InputExtensionMRCS = value == "*.mrcs";
                    InputExtensionEM = value == "*.em";
                    InputExtensionDAT = value == "*.dat";
                }
            }
        }

        private bool _InputExtensionMRC = true;
        public bool InputExtensionMRC
        {
            get { return _InputExtensionMRC; }
            set
            {
                if (value != _InputExtensionMRC)
                {
                    _InputExtensionMRC = value; OnPropertyChanged();
                    if (value)
                        InputExtension = "*.mrc";
                }
            }
        }

        private bool _InputExtensionMRCS = false;
        public bool InputExtensionMRCS
        {
            get { return _InputExtensionMRCS; }
            set
            {
                if (value != _InputExtensionMRCS)
                {
                    _InputExtensionMRCS = value; OnPropertyChanged();
                    if (value)
                        InputExtension = "*.mrcs";
                }
            }
        }

        private bool _InputExtensionEM = false;
        public bool InputExtensionEM
        {
            get { return _InputExtensionEM; }
            set
            {
                if (value != _InputExtensionEM)
                {
                    _InputExtensionEM = value; OnPropertyChanged();
                    if (value)
                        InputExtension = "*.em";
                }
            }
        }

        private bool _InputExtensionDAT = false;
        public bool InputExtensionDAT
        {
            get { return _InputExtensionDAT; }
            set
            {
                if (value != _InputExtensionDAT)
                {
                    _InputExtensionDAT = value; OnPropertyChanged();
                    if (value)
                        InputExtension = "*.dat";
                }
            }
        }

        private int _InputDatWidth = 7676;
        public int InputDatWidth
        {
            get { return _InputDatWidth; }
            set { if (value != _InputDatWidth) { _InputDatWidth = value; OnPropertyChanged(); } }
        }

        private int _InputDatHeight = 7420;
        public int InputDatHeight
        {
            get { return _InputDatHeight; }
            set { if (value != _InputDatHeight) { _InputDatHeight = value; OnPropertyChanged(); } }
        }

        private string _InputDatType = "int8";
        public string InputDatType
        {
            get { return _InputDatType; }
            set { if (value != _InputDatType) { _InputDatType = value; OnPropertyChanged(); } }
        }

        private long _InputDatOffset = 0;
        public long InputDatOffset
        {
            get { return _InputDatOffset; }
            set { if (value != _InputDatOffset) { _InputDatOffset = value; OnPropertyChanged(); } }
        }

        public ObservableCollection<string> _InputDatTypes = new ObservableCollection<string>
        {
            "int8", "int16", "int32", "int64", "float32", "float64"
        };

        public ObservableCollection<string> InputDatTypes
        {
            get { return _InputDatTypes; }
        } 

        private string _OutputFolder = "";
        public string OutputFolder
        {
            get { return _OutputFolder; }
            set
            {
                if (value != _OutputFolder)
                {
                    _OutputFolder = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _OutputExtension = "*.mrc";
        public string OutputExtension
        {
            get { return _OutputExtension; }
            set
            {
                if (value != _OutputExtension)
                {
                    _OutputExtension = value;
                    OnPropertyChanged();
                    OutputExtensionMRC = value == "*.mrc";
                    OutputExtensionEM = value == "*.em";
                }
            }
        }

        private bool _OutputExtensionMRC = true;
        public bool OutputExtensionMRC
        {
            get { return _OutputExtensionMRC; }
            set
            {
                if (value != _OutputExtensionMRC)
                {
                    _OutputExtensionMRC = value; OnPropertyChanged();
                    if (value)
                        OutputExtension = "*.mrc";
                }
            }
        }

        private bool _OutputExtensionEM = false;
        public bool OutputExtensionEM
        {
            get { return _OutputExtensionEM; }
            set
            {
                if (value != _OutputExtensionEM)
                {
                    _OutputExtensionEM = value; OnPropertyChanged();
                    if (value)
                        OutputExtension = "*.em";
                }
            }
        }

        private string _ArchiveOperation = "Compress";
        public string ArchiveOperation
        {
            get { return _ArchiveOperation; }
            set
            {
                if (value != _ArchiveOperation)
                {
                    _ArchiveOperation = value;
                    OnPropertyChanged();
                    ArchiveOperationCompress = value == "Compress";
                    ArchiveOperationMove = value == "Move";
                }
            }
        }

        private bool _ArchiveOperationCompress = true;
        public bool ArchiveOperationCompress
        {
            get { return _ArchiveOperationCompress; }
            set
            {
                if (value != _ArchiveOperationCompress)
                {
                    _ArchiveOperationCompress = value; OnPropertyChanged();
                    if (value)
                        ArchiveOperation = "Compress";
                }
            }
        }

        private bool _ArchiveOperationMove = false;
        public bool ArchiveOperationMove
        {
            get { return _ArchiveOperationMove; }
            set
            {
                if (value != _ArchiveOperationMove)
                {
                    _ArchiveOperationMove = value; OnPropertyChanged();
                    if (value)
                        ArchiveOperation = "Move";
                }
            }
        }

        private string _ArchiveFolder = "";
        public string ArchiveFolder
        {
            get { return _ArchiveFolder; }
            set
            {
                if (value != _ArchiveFolder)
                {
                    _ArchiveFolder = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _ExpectTiltSeries = false;
        public bool ExpectTiltSeries
        {
            get { return _ExpectTiltSeries; }
            set
            {
                if (value != _ExpectTiltSeries)
                {
                    _ExpectTiltSeries = value;
                    OnPropertyChanged();
                }
            }
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

        private bool _CorrectBlackRectangles = true;

        public bool CorrectBlackRectangles
        {
            get { return _CorrectBlackRectangles; }
            set
            {
                if (value != _CorrectBlackRectangles)
                {
                    _CorrectBlackRectangles = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _CorrectDoseWeighting = true;

        public bool CorrectDoseWeighting
        {
            get { return _CorrectDoseWeighting; }
            set
            {
                if (value != _CorrectDoseWeighting)
                {
                    _CorrectDoseWeighting = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _CorrectDosePerFrame = 1.0M;
        public decimal CorrectDosePerFrame
        {
            get { return _CorrectDosePerFrame; }
            set { if (value != _CorrectDosePerFrame) { _CorrectDosePerFrame = value; OnPropertyChanged(); } }
        }

        #endregion

        #region CTF

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

        private decimal _CTFPixelMin = 1.35M;

        public decimal CTFPixelMin
        {
            get { return _CTFPixelMin; }
            set
            {
                if (value != _CTFPixelMin)
                {
                    _CTFPixelMin = value;
                    OnPropertyChanged();
                    RecalcBinnedPixelSize();
                }
            }
        }

        private decimal _CTFPixelMax = 1.35M;

        public decimal CTFPixelMax
        {
            get { return _CTFPixelMax; }
            set
            {
                if (value != _CTFPixelMax)
                {
                    _CTFPixelMax = value;
                    OnPropertyChanged();
                    RecalcBinnedPixelSize();
                }
            }
        }

        private decimal _CTFPixelAngle = 0M;

        public decimal CTFPixelAngle
        {
            get { return _CTFPixelAngle; }
            set
            {
                if (value != _CTFPixelAngle)
                {
                    _CTFPixelAngle = value;
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

        private decimal _CTFZMin = 0M;

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

        private decimal _CTFZMax = 5M;

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

        #endregion

        #region Movement

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

        private decimal _PostBinTimes = 0;

        public decimal PostBinTimes
        {
            get { return _PostBinTimes; }
            set
            {
                if (value != _PostBinTimes)
                {
                    _PostBinTimes = value;
                    OnPropertyChanged();
                    RecalcBinnedPixelSize();
                }
            }
        }

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

        #region Runtime

        private int _DeviceCount = 0;
        public int DeviceCount
        {
            get { return _DeviceCount; }
            set { if (value != _DeviceCount) { _DeviceCount = value; OnPropertyChanged(); } }
        }

        private ObservableCollection<Movie> _Movies = new ObservableCollection<Movie>();

        public ObservableCollection<Movie> Movies
        {
            get { return _Movies; }
            set
            {
                if (value != _Movies)
                {
                    _Movies = value;
                    OnPropertyChanged();
                }
            }
        }

        private Movie _DisplayedMovie = null;
        public Movie DisplayedMovie
        {
            get { return _DisplayedMovie; }
            set
            {
                if (value != _DisplayedMovie)
                {
                    _DisplayedMovie = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _BinnedPixelSize = 1M;
        public decimal BinnedPixelSize
        {
            get { return _BinnedPixelSize; }
            set { if (value != _BinnedPixelSize) { _BinnedPixelSize = value; OnPropertyChanged(); } }
        }
        private void RecalcBinnedPixelSize()
        {
            decimal Pixelsize = (CTFPixelMin + CTFPixelMax) * 0.5M;
            BinnedPixelSize = Pixelsize * (decimal)Math.Pow(2.0, (double) PostBinTimes);
        }

        #endregion

        public void Save(string path)
        {
            XmlTextWriter Writer = new XmlTextWriter(File.Create(path), Encoding.Unicode);
            Writer.Formatting = Formatting.Indented;
            Writer.IndentChar = '\t';
            Writer.Indentation = 1;
            Writer.WriteStartDocument();
            Writer.WriteStartElement("Settings");

            XMLHelper.WriteParamNode(Writer, "InputFolder", InputFolder);
            XMLHelper.WriteParamNode(Writer, "InputExtension", InputExtension);
            XMLHelper.WriteParamNode(Writer, "InputDatWidth", InputDatWidth);
            XMLHelper.WriteParamNode(Writer, "InputDatHeight", InputDatHeight);
            XMLHelper.WriteParamNode(Writer, "InputDatType", InputDatType);
            XMLHelper.WriteParamNode(Writer, "InputDatOffset", InputDatOffset);
            XMLHelper.WriteParamNode(Writer, "OutputFolder", OutputFolder);
            XMLHelper.WriteParamNode(Writer, "OutputExtension", OutputExtension);
            XMLHelper.WriteParamNode(Writer, "ArchiveOperation", ArchiveOperation);
            XMLHelper.WriteParamNode(Writer, "ArchiveFolder", ArchiveFolder);
            XMLHelper.WriteParamNode(Writer, "ExpectTiltSeries", ExpectTiltSeries);

            XMLHelper.WriteParamNode(Writer, "GainPath", GainPath);
            XMLHelper.WriteParamNode(Writer, "CorrectGain", CorrectGain);
            XMLHelper.WriteParamNode(Writer, "CorrectBlackRectangles", CorrectBlackRectangles);
            XMLHelper.WriteParamNode(Writer, "CorrectDoseWeighting", CorrectDoseWeighting);
            XMLHelper.WriteParamNode(Writer, "CorrectDosePerFrame", CorrectDosePerFrame);

            XMLHelper.WriteParamNode(Writer, "CTFWindow", CTFWindow);
            XMLHelper.WriteParamNode(Writer, "CTFRangeMin", CTFRangeMin);
            XMLHelper.WriteParamNode(Writer, "CTFRangeMax", CTFRangeMax);
            XMLHelper.WriteParamNode(Writer, "CTFMinQuality", CTFMinQuality);
            XMLHelper.WriteParamNode(Writer, "CTFVoltage", CTFVoltage);
            XMLHelper.WriteParamNode(Writer, "CTFCs", CTFCs);
            XMLHelper.WriteParamNode(Writer, "CTFAmplitude", CTFAmplitude);
            XMLHelper.WriteParamNode(Writer, "CTFPixelMin", CTFPixelMin);
            XMLHelper.WriteParamNode(Writer, "CTFPixelMax", CTFPixelMax);
            XMLHelper.WriteParamNode(Writer, "CTFPixelAngle", CTFPixelAngle);
            XMLHelper.WriteParamNode(Writer, "CTFDoPhase", CTFDoPhase);
            XMLHelper.WriteParamNode(Writer, "CTFAstigmatism", CTFAstigmatism);
            XMLHelper.WriteParamNode(Writer, "CTFAstigmatismAngle", CTFAstigmatismAngle);
            XMLHelper.WriteParamNode(Writer, "CTFZMin", CTFZMin);
            XMLHelper.WriteParamNode(Writer, "CTFZMax", CTFZMax);

            XMLHelper.WriteParamNode(Writer, "MovementRangeMin", MovementRangeMin);
            XMLHelper.WriteParamNode(Writer, "MovementRangeMax", MovementRangeMax);

            XMLHelper.WriteParamNode(Writer, "GridCTFX", GridCTFX);
            XMLHelper.WriteParamNode(Writer, "GridCTFY", GridCTFY);
            XMLHelper.WriteParamNode(Writer, "GridCTFZ", GridCTFZ);
            XMLHelper.WriteParamNode(Writer, "GridMoveX", GridMoveX);
            XMLHelper.WriteParamNode(Writer, "GridMoveY", GridMoveY);
            XMLHelper.WriteParamNode(Writer, "GridMoveZ", GridMoveZ);

            XMLHelper.WriteParamNode(Writer, "PostBinTimes", PostBinTimes);
            XMLHelper.WriteParamNode(Writer, "PostAverage", PostAverage);
            XMLHelper.WriteParamNode(Writer, "PostStack", PostStack);
            XMLHelper.WriteParamNode(Writer, "PostStackGroupSize", PostStackGroupSize);

            Writer.WriteEndElement();
            Writer.WriteEndDocument();
            Writer.Flush();
            Writer.Close();
        }

        public void Load(string path)
        {
            using (Stream SettingsStream = File.OpenRead(path))
            {
                XPathDocument Doc = new XPathDocument(SettingsStream);
                XPathNavigator Reader = Doc.CreateNavigator();
                Reader.MoveToRoot();

                InputFolder = XMLHelper.LoadParamNode(Reader, "InputFolder", "");
                InputExtension = XMLHelper.LoadParamNode(Reader, "InputExtension", "*.mrc");
                InputDatWidth = XMLHelper.LoadParamNode(Reader, "InputDatWidth", 7676);
                InputDatHeight = XMLHelper.LoadParamNode(Reader, "InputDatHeight", 7420);
                InputDatType = XMLHelper.LoadParamNode(Reader, "InputDatType", "int8");
                InputDatOffset = XMLHelper.LoadParamNode(Reader, "InputDatOffset", 0);
                OutputFolder = XMLHelper.LoadParamNode(Reader, "OutputFolder", "");
                OutputExtension = XMLHelper.LoadParamNode(Reader, "OutputExtension", "*.mrc");
                ArchiveOperation = XMLHelper.LoadParamNode(Reader, "ArchiveOperation", "Compress");
                ArchiveFolder = XMLHelper.LoadParamNode(Reader, "ArchiveFolder", "");
                ExpectTiltSeries = XMLHelper.LoadParamNode(Reader, "ExpectTiltSeries", false);

                GainPath = XMLHelper.LoadParamNode(Reader, "GainPath", "");
                CorrectGain = XMLHelper.LoadParamNode(Reader, "CorrectGain", false);
                CorrectBlackRectangles = XMLHelper.LoadParamNode(Reader, "CorrectBlackRectangles", true);
                CorrectDoseWeighting = XMLHelper.LoadParamNode(Reader, "CorrectDoseWeighting", true);
                CorrectDosePerFrame = XMLHelper.LoadParamNode(Reader, "CorrectDosePerFrame", 1M);

                CTFWindow = XMLHelper.LoadParamNode(Reader, "CTFWindow", CTFWindow);
                CTFRangeMin = XMLHelper.LoadParamNode(Reader, "CTFRangeMin", CTFRangeMin);
                CTFRangeMax = XMLHelper.LoadParamNode(Reader, "CTFRangeMax", CTFRangeMax);
                CTFMinQuality = XMLHelper.LoadParamNode(Reader, "CTFMinQuality", CTFMinQuality);
                CTFVoltage = XMLHelper.LoadParamNode(Reader, "CTFVoltage", CTFVoltage);
                CTFCs = XMLHelper.LoadParamNode(Reader, "CTFCs", CTFCs);
                CTFAmplitude = XMLHelper.LoadParamNode(Reader, "CTFAmplitude", CTFAmplitude);
                CTFPixelMin = XMLHelper.LoadParamNode(Reader, "CTFPixelMin", CTFPixelMin);
                CTFPixelMax = XMLHelper.LoadParamNode(Reader, "CTFPixelMax", CTFPixelMax);
                CTFPixelAngle = XMLHelper.LoadParamNode(Reader, "CTFPixelAngle", CTFPixelAngle);
                CTFDoPhase = XMLHelper.LoadParamNode(Reader, "CTFDoPhase", CTFDoPhase);
                CTFAstigmatism = XMLHelper.LoadParamNode(Reader, "CTFAstigmatism", CTFAstigmatism);
                CTFAstigmatismAngle = XMLHelper.LoadParamNode(Reader, "CTFAstigmatismAngle", CTFAstigmatismAngle);
                CTFZMin = XMLHelper.LoadParamNode(Reader, "CTFZMin", CTFZMin);
                CTFZMax = XMLHelper.LoadParamNode(Reader, "CTFZMax", CTFZMax);

                MovementRangeMin = XMLHelper.LoadParamNode(Reader, "MovementRangeMin", MovementRangeMin);
                MovementRangeMax = XMLHelper.LoadParamNode(Reader, "MovementRangeMax", MovementRangeMax);

                GridCTFX = XMLHelper.LoadParamNode(Reader, "GridCTFX", GridCTFX);
                GridCTFY = XMLHelper.LoadParamNode(Reader, "GridCTFY", GridCTFY);
                GridCTFZ = XMLHelper.LoadParamNode(Reader, "GridCTFZ", GridCTFZ);
                GridMoveX = XMLHelper.LoadParamNode(Reader, "GridMoveX", GridMoveX);
                GridMoveY = XMLHelper.LoadParamNode(Reader, "GridMoveY", GridMoveY);
                GridMoveZ = XMLHelper.LoadParamNode(Reader, "GridMoveZ", GridMoveZ);

                PostBinTimes = XMLHelper.LoadParamNode(Reader, "PostBinTimes", PostBinTimes);
                RecalcBinnedPixelSize();
                PostAverage = XMLHelper.LoadParamNode(Reader, "PostAverage", PostAverage);
                PostStack = XMLHelper.LoadParamNode(Reader, "PostStack", PostStack);
                PostStackGroupSize = XMLHelper.LoadParamNode(Reader, "PostStackGroupSize", PostStackGroupSize);
            }
        }
    }
}
