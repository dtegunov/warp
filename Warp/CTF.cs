using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using Warp.Tools;

namespace Warp
{
    public class CTF : DataBase
    {
        private decimal _PixelSize = 1.0M;
        /// <summary>
        /// Pixel size in Angstrom
        /// </summary>
        public decimal PixelSize
        {
            get { return _PixelSize; }
            set { if (value != _PixelSize) { _PixelSize = value; OnPropertyChanged(); } }
        }

        private decimal _PixelSizeDelta = 0M;
        /// <summary>
        /// Pixel size anisotropy delta in Angstrom
        /// </summary>
        public decimal PixelSizeDelta
        {
            get { return _PixelSizeDelta; }
            set { if (value != _PixelSizeDelta) { _PixelSizeDelta = value; OnPropertyChanged(); } }
        }

        private decimal _PixelSizeAngle = 0M;
        /// <summary>
        /// Pixel size anisotropy angle in radians
        /// </summary>
        public decimal PixelSizeAngle
        {
            get { return _PixelSizeAngle; }
            set
            {
                value = (decimal)Accord.Math.Tools.Mod((double)value + 180.0, 180.0);
                if (value != _PixelSizeAngle)
                {
                    _PixelSizeAngle = value;
                    OnPropertyChanged();
                }
            }
        }

        private decimal _Cs = 2.1M;
        /// <summary>
        /// Spherical aberration in mm
        /// </summary>
        public decimal Cs
        {
            get { return _Cs; }
            set { if (value != _Cs) { _Cs = value; OnPropertyChanged(); } }
        }

        private decimal _Voltage = 300.0M;
        /// <summary>
        /// Voltage in kV
        /// </summary>
        public decimal Voltage
        {
            get { return _Voltage; }
            set { if (value != _Voltage) { _Voltage = value; OnPropertyChanged(); } }
        }

        private decimal _Defocus = 1.0M;
        /// <summary>
        /// Defocus in um, underfocus (first peak positive) is positive
        /// </summary>
        public decimal Defocus
        {
            get { return _Defocus; }
            set { if (value != _Defocus) { _Defocus = value; OnPropertyChanged(); } }
        }

        private decimal _DefocusDelta = 0M;
        /// <summary>
        /// Astigmatism delta defocus in um
        /// </summary>
        public decimal DefocusDelta
        {
            get { return _DefocusDelta; }
            set { if (value != _DefocusDelta) { _DefocusDelta = value; OnPropertyChanged(); } }
        }

        private decimal _DefocusAngle = 0M;
        /// <summary>
        /// Astigmatism angle in radians
        /// </summary>
        public decimal DefocusAngle
        {
            get { return _DefocusAngle; }
            set
            {
                //value = (decimal) Accord.Math.Tools.Mod((double)value + 180.0, 180.0);
                if (value != _DefocusAngle)
                {
                    _DefocusAngle = value; OnPropertyChanged();
                }
            }
        }

        private decimal _Amplitude = 0.07M;
        /// <summary>
        /// Amplitude contrast
        /// </summary>
        public decimal Amplitude
        {
            get { return _Amplitude; }
            set { if (value != _Amplitude) { _Amplitude = value; OnPropertyChanged(); } }
        }

        private decimal _Bfactor = 0M;
        /// <summary>
        /// B factor in Angstrom^2
        /// </summary>
        public decimal Bfactor
        {
            get { return _Bfactor; }
            set { if (value != _Bfactor) { _Bfactor = value; OnPropertyChanged(); } }
        }

        private decimal _Scale = 1.0M;
        /// <summary>
        /// Scale, i. e. CTF oscillates within [-Scale; +Scale]
        /// </summary>
        public decimal Scale
        {
            get { return _Scale; }
            set { if (value != _Scale) { _Scale = value; OnPropertyChanged(); } }
        }

        private decimal _PhaseShift = 0M;
        /// <summary>
        /// Phase shift in Pi
        /// </summary>
        public decimal PhaseShift
        {
            get { return _PhaseShift; }
            set { if (value != _PhaseShift) { _PhaseShift = value; OnPropertyChanged(); } }
        }

        public void FromStruct(CTFStruct s)
        {
            _PixelSize = (decimal) s.PixelSize * 1e10M;
            _PixelSizeDelta = (decimal) s.PixelSizeDelta * 1e10M;
            _PixelSizeAngle = (decimal) s.PixelSizeAngle * (180M / (decimal) Math.PI);

            _Cs = (decimal) s.Cs * 1e3M;
            _Voltage = (decimal) s.Voltage * 1e-3M;
            _Amplitude = (decimal) s.Amplitude;

            _Defocus = (decimal) -s.Defocus * 1e6M;
            _DefocusDelta = (decimal) -s.DefocusDelta * 1e6M;
            _DefocusAngle = (decimal) s.AstigmatismAngle * (180M / (decimal)Math.PI);

            //_Bfactor = (decimal) s.Bfactor * 1e20M;
            _Scale = (decimal) s.Scale;

            _PhaseShift = (decimal) s.PhaseShift / (decimal) Math.PI;

            OnPropertyChanged("");
        }

        public CTFStruct ToStruct()
        {
            CTFStruct Result = new CTFStruct();

            Result.PixelSize = (float)(PixelSize * 1e-10M);
            Result.PixelSizeDelta = (float)(PixelSizeDelta * 1e-10M);
            Result.PixelSizeAngle = (float)PixelSizeAngle / (float)(180.0 / Math.PI);

            Result.Cs = (float)(Cs * 1e-3M);
            Result.Voltage = (float)(Voltage * 1e3M);
            Result.Amplitude = (float)Amplitude;

            Result.Defocus = (float)(-Defocus * 1e-6M);
            Result.DefocusDelta = (float)(-DefocusDelta * 1e-6M);
            Result.AstigmatismAngle = (float)DefocusAngle / (float)(180.0 / Math.PI);

            Result.Bfactor = (float)(Bfactor * 1e-20M);
            Result.Scale = (float)Scale;

            Result.PhaseShift = (float)(PhaseShift * (decimal)Math.PI);

            return Result;
        }

        public float[] Get1D(int width, bool ampsquared, bool ignorebfactor = false, bool ignorescale = false)
        {
            float[] Output = new float[width];

            double ny = 0.5 / (double)PixelSize / width;

            for (int i = 0; i < width; i++)
                Output[i] = Get1D(i * (float)ny, ampsquared, ignorebfactor, ignorescale);

            return Output;
        }

        public float Get1D(float freq, bool ampsquared, bool ignorebfactor = false, bool ignorescale = false)
        {
            double voltage = (double) Voltage * 1e3;
            double lambda = 12.2643247 / Math.Sqrt(voltage * (1.0 + voltage * 0.978466e-6));
            double defocus = -(double) Defocus * 1e4;
            double cs = (double) Cs * 1e7;
            double amplitude = (double) Amplitude;
            double scale = (double) Scale;
            double phaseshift = (double) PhaseShift * Math.PI;
            double K1 = Math.PI * lambda;
            double K2 = Math.PI * 0.5f * cs * lambda * lambda * lambda;
            double K3 = Math.Sqrt(1f - amplitude * amplitude);
            double K4 = (double)Bfactor * 0.25f;

            double r2 = freq * freq;
            double r4 = r2 * r2;

            double deltaf = defocus;
            double argument = K1 * deltaf * r2 + K2 * r4 - phaseshift;
            double retval = amplitude * Math.Cos(argument) - K3 * Math.Sin(argument);

            if (K4 != 0)
                retval *= Math.Exp(K4 * r2);

            if (ampsquared)
                retval = Math.Abs(retval * retval);

            return (float)(scale * retval);
        }

        public double[] Get1DDouble(int width, bool ampsquared, bool ignorebfactor = false, bool ignorescale = false)
        {
            double[] Output = new double[width];

            double ny = 0.5 / (double)PixelSize / width;

            for (int i = 0; i < width; i++)
                Output[i] = Get1DDouble(i * ny, ampsquared, ignorebfactor, ignorescale);

            return Output;
        }

        public double Get1DDouble(double freq, bool ampsquared, bool ignorebfactor = false, bool ignorescale = false)
        {
            double voltage = (double)Voltage * 1e3;
            double lambda = 12.2643247 / Math.Sqrt(voltage * (1.0 + voltage * 0.978466e-6));
            double defocus = -(double)Defocus * 1e4;
            double cs = (double)Cs * 1e7;
            double amplitude = (double)Amplitude;
            double scale = (double)Scale;
            double phaseshift = (double)PhaseShift * Math.PI;
            double K1 = Math.PI * lambda;
            double K2 = Math.PI * 0.5f * cs * lambda * lambda * lambda;
            double K3 = Math.Sqrt(1f - amplitude * amplitude);
            double K4 = (double)Bfactor * 0.25f;

            double r2 = freq * freq;
            double r4 = r2 * r2;

            double deltaf = defocus;
            double argument = K1 * deltaf * r2 + K2 * r4 - phaseshift;
            double retval = amplitude * Math.Cos(argument) - K3 * Math.Sin(argument);

            if (K4 != 0)
                retval *= Math.Exp(K4 * r2);

            if (ampsquared)
                retval = Math.Abs(retval * retval);

            return scale * retval;
        }

        public float[] Get2D(float2[] coordinates, bool ampsquared, bool ignorebfactor = false, bool ignorescale = false)
        {
            float[] Output = new float[coordinates.Length];
            
            float pixelsize = (float) PixelSize;
            float pixeldelta = (float) PixelSizeDelta;
            float pixelangle = (float) PixelSizeAngle / (float)(180.0 / Math.PI);
            float voltage = (float)Voltage * 1e3f;
            float lambda = 12.2643247f / (float)Math.Sqrt(voltage * (1.0f + voltage * 0.978466e-6f));
            float defocus = -(float)Defocus * 1e4f;
            float defocusdelta = -(float)DefocusDelta * 1e4f * 0.5f;
            float astigmatismangle = (float) DefocusAngle / (float)(180.0 / Math.PI);
            float cs = (float)Cs * 1e7f;
            float amplitude = (float)Amplitude;
            float scale = (float)Scale;
            float phaseshift = (float)PhaseShift * (float)Math.PI;
            float K1 = (float)Math.PI * lambda;
            float K2 = (float)Math.PI * 0.5f * cs * lambda * lambda * lambda;
            float K3 = (float)Math.Sqrt(1f - amplitude * amplitude);
            float K4 = (float)Bfactor * 0.25f;

            Parallel.For(0, coordinates.Length, i =>
            {
                float angle = coordinates[i].Y;
                float r = coordinates[i].X / (pixelsize + pixeldelta * (float) Math.Cos(2.0 * (angle - pixelangle)));
                float r2 = r * r;
                float r4 = r2 * r2;

                float deltaf = defocus + defocusdelta * (float) Math.Cos(2.0 * (angle - astigmatismangle));
                float argument = K1 * deltaf * r2 + K2 * r4 - phaseshift;
                float retval = amplitude * (float) Math.Cos(argument) - K3 * (float) Math.Sin(argument);

                if (!ignorebfactor && K4 != 0)
                    retval *= (float) Math.Exp(K4 * r2);

                if (ampsquared)
                    retval = Math.Abs(retval);// * retval);

                if (!ignorescale)
                    Output[i] = scale * retval;
                else
                    Output[i] = retval;
            });

            return Output;
        }

        public float GetFalloff(float freq)
        {
            double K4 = -(double)Bfactor * 0.25f;
            double r2 = freq * freq;

            return (float)Math.Exp(K4 * r2);
        }

        public float[] GetPeaks()
        {
            List<float> Result = new List<float>();

            double[] Values = Get1DDouble(1 << 12, false);
            double[] dValues = MathHelper.Diff(Values);

            for (int i = 0; i < dValues.Length - 1; i++)
                if (Math.Sign(dValues[i]) != Math.Sign(dValues[i + 1]))
                    Result.Add(0.5f * i / Values.Length);
            
            return Result.ToArray();
        }

        public float[] GetZeros()
        {
            List<float> Result = new List<float>();

            double[] Values = Get1DDouble(1 << 12, false);

            for (int i = 0; i < Values.Length - 1; i++)
                if (Math.Sign(Values[i]) != Math.Sign(Values[i + 1]))
                    Result.Add(0.5f * i / Values.Length);

            return Result.ToArray();
        }

        public void Save(XmlTextWriter writer)
        {
            XMLHelper.WriteParamNode(writer, "PixelSize", PixelSize);
            XMLHelper.WriteParamNode(writer, "PixelSizeDelta", PixelSizeDelta);
            XMLHelper.WriteParamNode(writer, "PixelSizeAngle", PixelSizeAngle);
            XMLHelper.WriteParamNode(writer, "Cs", Cs);
            XMLHelper.WriteParamNode(writer, "Voltage", Voltage);
            XMLHelper.WriteParamNode(writer, "Defocus", Defocus);
            XMLHelper.WriteParamNode(writer, "DefocusDelta", DefocusDelta);
            XMLHelper.WriteParamNode(writer, "DefocusAngle", DefocusAngle);
            XMLHelper.WriteParamNode(writer, "Amplitude", Amplitude);
            XMLHelper.WriteParamNode(writer, "Bfactor", Bfactor);
            XMLHelper.WriteParamNode(writer, "Scale", Scale);
            XMLHelper.WriteParamNode(writer, "PhaseShift", PhaseShift);
        }

        public void Load(XPathNavigator nav)
        {
            PixelSize = XMLHelper.LoadParamNode(nav, "PixelSize", 1M);
            PixelSizeDelta = XMLHelper.LoadParamNode(nav, "PixelSizeDelta", 0M);
            PixelSizeAngle = XMLHelper.LoadParamNode(nav, "PixelSizeAngle", 0M);
            Cs = XMLHelper.LoadParamNode(nav, "Cs", 2.1M);
            Voltage = XMLHelper.LoadParamNode(nav, "Voltage", 300M);
            Defocus = XMLHelper.LoadParamNode(nav, "Defocus", 1M);
            DefocusDelta = XMLHelper.LoadParamNode(nav, "DefocusDelta", 0M);
            DefocusAngle = XMLHelper.LoadParamNode(nav, "DefocusAngle", 0M);
            Amplitude = XMLHelper.LoadParamNode(nav, "Amplitude", 0.07M);
            Bfactor = XMLHelper.LoadParamNode(nav, "Bfactor", 0M);
            Scale = XMLHelper.LoadParamNode(nav, "Scale", 1M);
            PhaseShift = XMLHelper.LoadParamNode(nav, "PhaseShift", 0M);
        }

        public CTF GetCopy()
        {
            return new CTF
            {
                _Amplitude = Amplitude,
                _Bfactor = Bfactor,
                _Cs = Cs,
                _Defocus = Defocus,
                _DefocusAngle = DefocusAngle,
                _DefocusDelta = DefocusDelta,
                _PhaseShift = PhaseShift,
                _PixelSize = PixelSize,
                _PixelSizeAngle = PixelSizeAngle,
                _PixelSizeDelta = PixelSizeDelta,
                _Scale = Scale,
                _Voltage = Voltage
            };
        }
    }

    /// <summary>
    /// Everything is in SI units
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CTFStruct
    {
        public float PixelSize;
        public float PixelSizeDelta;
        public float PixelSizeAngle;
        public float Cs;
        public float Voltage;
        public float Defocus;
        public float AstigmatismAngle;
        public float DefocusDelta;
        public float Amplitude;
        public float Bfactor;
        public float Scale;
        public float PhaseShift;
    }

    /// <summary>
    /// Everything is in SI units
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CTFFitStruct
    {
        public float3 Pixelsize;
        public float3 Pixeldelta;
        public float3 Pixelangle;
        public float3 Cs;
        public float3 Voltage;
        public float3 Defocus;
        public float3 Astigmatismangle;
        public float3 Defocusdelta;
        public float3 Amplitude;
        public float3 Bfactor;
        public float3 Scale;
        public float3 Phaseshift;

        public int2 DimsPeriodogram;
        public int MaskInnerRadius;
        public int MaskOuterRadius;
    }
}