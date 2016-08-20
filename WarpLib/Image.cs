using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using Accord;
using Warp.Headers;
using Warp.Tools;

namespace Warp
{
    public class Image : IDisposable
    {
        private readonly object Sync = new object();
        
        public readonly int3 Dims;
        public int3 DimsFT => new int3(Dims.X / 2 + 1, Dims.Y, Dims.Z);
        public int2 DimsSlice => new int2(Dims.X, Dims.Y);
        public int2 DimsFTSlice => new int2(DimsFT.X, DimsFT.Y);
        public int3 DimsEffective => IsFT ? DimsFT : Dims;

        public readonly bool IsFT;
        public readonly bool IsComplex;
        public readonly bool IsHalf;

        public long ElementsComplex => IsFT ? DimsFT.Elements() : Dims.Elements();
        public long ElementsReal => IsComplex ? ElementsComplex * 2 : ElementsComplex;

        public long ElementsSliceComplex => IsFT ? DimsFTSlice.Elements() : DimsSlice.Elements();
        public long ElementsSliceReal => IsComplex ? ElementsSliceComplex * 2 : ElementsSliceComplex;

        public long ElementsLineComplex => IsFT ? DimsFTSlice.X : DimsSlice.X;
        public long ElementsLineReal => IsComplex ? ElementsLineComplex * 2 : ElementsLineComplex;

        private bool IsDeviceDirty = false;
        private IntPtr _DeviceData = IntPtr.Zero;

        private IntPtr DeviceData
        {
            get
            {
                if (_DeviceData == IntPtr.Zero)
                {
                    _DeviceData = !IsHalf ? GPU.MallocDevice(ElementsReal) : GPU.MallocDeviceHalf(ElementsReal);
                    GPU.OnMemoryChanged();
                }

                return _DeviceData;
            }
        }

        private bool IsHostDirty = false;
        private float[][] _HostData = null;

        private float[][] HostData
        {
            get
            {
                if (_HostData == null)
                {
                    _HostData = new float[Dims.Z][];
                    for (int i = 0; i < Dims.Z; i++)
                        _HostData[i] = new float[ElementsSliceReal];
                }

                return _HostData;
            }
        }

        public Image(float[][] data, int3 dims, bool isft = false, bool iscomplex = false, bool ishalf = false)
        {
            Dims = dims;
            IsFT = isft;
            IsComplex = iscomplex;
            IsHalf = ishalf;

            _HostData = data;
            IsHostDirty = true;
        }

        public Image(float2[][] data, int3 dims, bool isft = false, bool ishalf = false)
        {
            Dims = dims;
            IsFT = isft;
            IsComplex = true;
            IsHalf = ishalf;

            UpdateHostWithComplex(data);
            IsHostDirty = true;
        }

        public Image(float[] data, int3 dims, bool isft = false, bool iscomplex = false, bool ishalf = false)
        {
            Dims = dims;
            IsFT = isft;
            IsComplex = iscomplex;
            IsHalf = ishalf;

            float[][] Slices = new float[dims.Z][];
            int i = 0;
            for (int z = 0; z < dims.Z; z++)
            {
                Slices[z] = new float[ElementsSliceReal];
                for (int j = 0; j < Slices[z].Length; j++)
                    Slices[z][j] = data[i++];
            }

            _HostData = Slices;
            IsHostDirty = true;
        }

        public Image(float2[] data, int3 dims, bool isft = false, bool ishalf = false)
        {
            Dims = dims;
            IsFT = isft;
            IsComplex = true;
            IsHalf = ishalf;

            float[][] Slices = new float[dims.Z][];
            int i = 0;
            for (int z = 0; z < dims.Z; z++)
            {
                Slices[z] = new float[ElementsSliceReal];
                for (int j = 0; j < Slices[z].Length / 2; j++)
                {
                    Slices[z][j * 2] = data[i].X;
                    Slices[z][j * 2 + 1] = data[i].Y;
                    i++;
                }
            }

            _HostData = Slices;
            IsHostDirty = true;
        }

        public Image(float[] data, bool isft = false, bool iscomplex = false, bool ishalf = false) : 
            this(data, new int3(data.Length, 1, 1), isft, iscomplex, ishalf) { }

        public Image(float2[] data, bool isft = false, bool ishalf = false) : 
            this(data, new int3(data.Length, 1, 1), isft, ishalf) { }

        public Image(int3 dims, bool isft = false, bool iscomplex = false, bool ishalf = false)
        {
            Dims = dims;
            IsFT = isft;
            IsComplex = iscomplex;
            IsHalf = ishalf;

            _HostData = HostData; // Initializes new array since _HostData is null
            IsHostDirty = true;
        }

        public Image(IntPtr deviceData, int3 dims, bool isft = false, bool iscomplex = false, bool ishalf = false)
        {
            Dims = dims;
            IsFT = isft;
            IsComplex = iscomplex;
            IsHalf = ishalf;

            _DeviceData = !IsHalf ? GPU.MallocDevice(ElementsReal) : GPU.MallocDeviceHalf(ElementsReal);
            GPU.OnMemoryChanged();
            if (deviceData != IntPtr.Zero)
            {
                if (!IsHalf)
                    GPU.CopyDeviceToDevice(deviceData, _DeviceData, ElementsReal);
                else
                    GPU.CopyDeviceHalfToDeviceHalf(deviceData, _DeviceData, ElementsReal);
            }
            IsDeviceDirty = true;
        }

        public IntPtr GetDevice(Intent intent)
        {
            lock (Sync)
            {
                if ((intent & Intent.Read) > 0 && IsHostDirty)
                {
                    for (int z = 0; z < Dims.Z; z++)
                        if (!IsHalf)
                            GPU.CopyHostToDevice(HostData[z], new IntPtr((long) DeviceData + ElementsSliceReal * z * sizeof (float)), ElementsSliceReal);
                        else
                            GPU.CopyHostToDeviceHalf(HostData[z], new IntPtr((long)DeviceData + ElementsSliceReal * z * sizeof(short)), ElementsSliceReal);

                    IsHostDirty = false;
                }

                if ((intent & Intent.Write) > 0)
                {
                    IsDeviceDirty = true;
                    IsHostDirty = false;
                }

                return DeviceData;
            }
        }

        public IntPtr GetDeviceSlice(int slice, Intent intent)
        {
            IntPtr Start = GetDevice(intent);
            Start = new IntPtr((long)Start + slice * ElementsSliceReal * (IsHalf ? sizeof(short) : sizeof (float)));

            return Start;
        }

        public float[][] GetHost(Intent intent)
        {
            lock (Sync)
            {
                if ((intent & Intent.Read) > 0 && IsDeviceDirty)
                {
                    for (int z = 0; z < Dims.Z; z++)
                        if (!IsHalf)
                            GPU.CopyDeviceToHost(new IntPtr((long)DeviceData + ElementsSliceReal * z * sizeof(float)), HostData[z], ElementsSliceReal);
                        else
                            GPU.CopyDeviceHalfToHost(new IntPtr((long)DeviceData + ElementsSliceReal * z * sizeof(short)), HostData[z], ElementsSliceReal);

                    IsDeviceDirty = false;
                }

                if ((intent & Intent.Write) > 0)
                {
                    IsHostDirty = true;
                    IsDeviceDirty = false;
                }

                return HostData;
            }
        }

        public float2[][] GetHostComplexCopy()
        {
            if (!IsComplex)
                throw new Exception("Data must be of complex type.");

            float[][] Data = GetHost(Intent.Read);
            float2[][] ComplexData = new float2[Dims.Z][];

            for (int z = 0; z < Dims.Z; z++)
            {
                float[] Slice = Data[z];
                float2[] ComplexSlice = new float2[DimsEffective.ElementsSlice()];
                for (int i = 0; i < ComplexSlice.Length; i++)
                    ComplexSlice[i] = new float2(Slice[i * 2], Slice[i * 2 + 1]);

                ComplexData[z] = ComplexSlice;
            }

            return ComplexData;
        }

        public void UpdateHostWithComplex(float2[][] complexData)
        {
            if (complexData.Length != Dims.Z ||
                complexData[0].Length != DimsEffective.ElementsSlice())
                throw new DimensionMismatchException();

            float[][] Data = GetHost(Intent.Write);

            for (int z = 0; z < Dims.Z; z++)
            {
                float[] Slice = Data[z];
                float2[] ComplexSlice = complexData[z];

                for (int i = 0; i < ComplexSlice.Length; i++)
                {
                    Slice[i * 2] = ComplexSlice[i].X;
                    Slice[i * 2 + 1] = ComplexSlice[i].Y;
                }
            }
        }

        public float[] GetHostContinuousCopy()
        {
            float[] Continuous = new float[ElementsReal];
            float[][] Data = GetHost(Intent.Read);
            unsafe
            {
                fixed (float* ContinuousPtr = Continuous)
                {
                    float* ContinuousP = ContinuousPtr;
                    for (int i = 0; i < Data.Length; i++)
                    {
                        fixed (float* DataPtr = Data[i])
                        {
                            float* DataP = DataPtr;
                            for (int j = 0; j < Data[i].Length; j++)
                                *ContinuousP++ = *DataP++;
                        }
                    }
                }
            }

            return Continuous;
        }

        public void FreeDevice()
        {
            lock (Sync)
            {
                if (_DeviceData != IntPtr.Zero)
                {
                    if (IsDeviceDirty)
                        for (int z = 0; z < Dims.Z; z++)
                            if (!IsHalf)
                                GPU.CopyDeviceToHost(new IntPtr((long)DeviceData + ElementsSliceReal * z * sizeof(float)), HostData[z], ElementsSliceReal);
                            else
                                GPU.CopyDeviceHalfToHost(new IntPtr((long)DeviceData + ElementsSliceReal * z * sizeof(short)), HostData[z], ElementsSliceReal);
                    GPU.FreeDevice(DeviceData);
                    GPU.OnMemoryChanged();
                    _DeviceData = IntPtr.Zero;
                    IsDeviceDirty = false;
                }

                IsHostDirty = true;
            }
        }

        public void WriteMRC(string path, HeaderMRC header = null)
        {
            if (header == null)
                header = new HeaderMRC();
            header.Dimensions = IsFT ? DimsFT : Dims;
            header.Dimensions.X *= IsComplex ? 2 : 1;

            float[][] Data = GetHost(Intent.Read);
            float Min = float.MaxValue, Max = -float.MaxValue;
            Parallel.For(0, Data.Length, z =>
            {
                float LocalMin = MathHelper.Min(Data[z]);
                float LocalMax = MathHelper.Max(Data[z]);
                lock (Data)
                {
                    Min = Math.Min(LocalMin, Min);
                    Max = Math.Max(LocalMax, Max);
                }
            });
            header.MinValue = Min;
            header.MaxValue = Max;

            IOHelper.WriteMapFloat(path, header, GetHost(Intent.Read));
        }

        public void Dispose()
        {
            lock (Sync)
            {
                if (_DeviceData != IntPtr.Zero)
                {
                    GPU.FreeDevice(_DeviceData);
                    GPU.OnMemoryChanged();
                    _DeviceData = IntPtr.Zero;
                    IsDeviceDirty = false;
                }

                _HostData = null;
                IsHostDirty = false;
            }
        }

        public Image AsHalf()
        {
            Image Result;

            if (!IsHalf)
            {
                Result = new Image(IntPtr.Zero, Dims, IsFT, IsComplex, true);
                GPU.SingleToHalf(GetDevice(Intent.Read), Result.GetDevice(Intent.Write), ElementsReal);
            }
            else
            {
                Result = new Image(GetDevice(Intent.Read), Dims, IsFT, IsComplex, true);
            }

            return Result;
        }

        public Image AsSingle()
        {
            Image Result;

            if (IsHalf)
            {
                IntPtr Temp = GPU.MallocDevice(ElementsReal);
                GPU.OnMemoryChanged();
                GPU.HalfToSingle(GetDevice(Intent.Read), Temp, ElementsReal);

                Result = new Image(Temp, Dims, IsFT, IsComplex, false);
                GPU.FreeDevice(Temp);
                GPU.OnMemoryChanged();
            }
            else
            {
                Result = new Image(GetDevice(Intent.Read), Dims, IsFT, IsComplex, false);
            }

            return Result;
        }

        public Image AsRegion(int3 origin, int3 dimensions)
        {
            if (origin.X + dimensions.X >= Dims.X || 
                origin.Y + dimensions.Y >= Dims.Y || 
                origin.Z + dimensions.Z >= Dims.Z)
                throw new IndexOutOfRangeException();

            float[][] Source = GetHost(Intent.Read);
            float[][] Region = new float[dimensions.Z][];

            int3 RealSourceDimensions = DimsEffective;
            if (IsComplex)
                RealSourceDimensions.X *= 2;
            int3 RealDimensions = new int3((IsFT ? dimensions.X / 2 + 1 : dimensions.X) * (IsComplex ? 2 : 1),
                                           dimensions.Y,
                                           dimensions.Z);

            for (int z = 0; z < RealDimensions.Z; z++)
            {
                float[] SourceSlice = Source[z + origin.Z];
                float[] Slice = new float[RealDimensions.ElementsSlice()];

                unsafe
                {
                    fixed (float* SourceSlicePtr = SourceSlice)
                    fixed (float* SlicePtr = Slice)
                        for (int y = 0; y < RealDimensions.Y; y++)
                        {
                            int YOffset = y + origin.Y;
                            for (int x = 0; x < RealDimensions.X; x++)
                                SlicePtr[y * RealDimensions.X + x] = SourceSlicePtr[YOffset * RealSourceDimensions.X + x + origin.X];
                        }
                }

                Region[z] = Slice;
            }

            return new Image(Region, dimensions, IsFT, IsComplex, IsHalf);
        }

        public Image AsPadded(int2 dimensions)
        {
            if (IsHalf)
                throw new Exception("Half precision not supported for padding.");

            if (IsComplex != IsFT)
                throw new Exception("FT format can only have complex data for padding purposes.");

            if (IsFT && (new int2(Dims) < dimensions) == (new int2(Dims) > dimensions))
                throw new Exception("For FT padding/cropping, both dimensions must be either smaller, or bigger.");

            Image Padded = null;

            if (!IsComplex && !IsFT)
            {
                Padded = new Image(IntPtr.Zero, new int3(dimensions.X, dimensions.Y, Dims.Z), false, false, false);
                GPU.Pad(GetDevice(Intent.Read), Padded.GetDevice(Intent.Write), Dims.Slice(), new int3(dimensions), (uint)Dims.Z);
            }
            else if (IsComplex && IsFT)
            {
                Padded = new Image(IntPtr.Zero, new int3(dimensions.X, dimensions.Y, Dims.Z), true, true, false);
                if (dimensions > new int2(Dims))
                    GPU.PadFT(GetDevice(Intent.Read), Padded.GetDevice(Intent.Write), Dims.Slice(), new int3(dimensions), (uint)Dims.Z);
                else
                    GPU.CropFT(GetDevice(Intent.Read), Padded.GetDevice(Intent.Write), Dims.Slice(), new int3(dimensions), (uint)Dims.Z);
            }

            return Padded;
        }

        public Image AsPadded(int3 dimensions)
        {
            if (IsHalf)
                throw new Exception("Half precision not supported for padding.");

            if (IsComplex != IsFT)
                throw new Exception("FT format can only have complex data for padding purposes.");

            if (IsFT && Dims < dimensions == Dims > dimensions)
                throw new Exception("For FT padding/cropping, both dimensions must be either smaller, or bigger.");

            Image Padded = null;

            if (!IsComplex && !IsFT)
            {
                Padded = new Image(IntPtr.Zero, dimensions, false, false, false);
                GPU.Pad(GetDevice(Intent.Read), Padded.GetDevice(Intent.Write), Dims, dimensions, 1);
            }
            else if (IsComplex && IsFT)
            {
                Padded = new Image(IntPtr.Zero, dimensions, true, true, false);
                if (dimensions > Dims)
                    GPU.PadFT(GetDevice(Intent.Read), Padded.GetDevice(Intent.Write), Dims, dimensions, 1);
                else
                    GPU.CropFT(GetDevice(Intent.Read), Padded.GetDevice(Intent.Write), Dims, dimensions, 1);
            }

            return Padded;
        }

        public Image AsFFT(bool isvolume = false)
        {
            if (IsHalf || IsComplex || IsFT)
                throw new Exception("Data format not supported.");

            Image FFT = new Image(IntPtr.Zero, Dims, true, true, false);
            GPU.FFT(GetDevice(Intent.Read), FFT.GetDevice(Intent.Write), isvolume ? Dims : Dims.Slice(), isvolume ? 1 : (uint)Dims.Z);

            return FFT;
        }

        public Image AsIFFT(bool isvolume = false)
        {
            if (IsHalf || !IsComplex || !IsFT)
                throw new Exception("Data format not supported.");

            Image IFFT = new Image(IntPtr.Zero, Dims, false, false, false);
            GPU.IFFT(GetDevice(Intent.Read), IFFT.GetDevice(Intent.Write), isvolume ? Dims : Dims.Slice(), isvolume ? 1 : (uint)Dims.Z);

            return IFFT;
        }

        public Image AsMultipleRegions(int3[] origins, int2 dimensions)
        {
            Image Extracted = new Image(IntPtr.Zero, new int3(dimensions.X, dimensions.Y, origins.Length), false, IsComplex, IsHalf);

            if (IsHalf)
                GPU.ExtractHalf(GetDevice(Intent.Read),
                                Extracted.GetDevice(Intent.Write),
                                Dims, new int3(dimensions),
                                Helper.ToInterleaved(origins),
                                (uint) origins.Length);
            else
                GPU.Extract(GetDevice(Intent.Read),
                            Extracted.GetDevice(Intent.Write),
                            Dims, new int3(dimensions),
                            Helper.ToInterleaved(origins),
                            (uint) origins.Length);

            return Extracted;
        }

        public Image AsReducedAlongZ()
        {
            Image Reduced = new Image(IntPtr.Zero, new int3(Dims.X, Dims.Y, 1), IsFT, IsComplex, IsHalf);

            if (IsHalf)
                GPU.ReduceMeanHalf(GetDevice(Intent.Read), Reduced.GetDevice(Intent.Write), (uint)ElementsSliceReal, (uint)Dims.Z, 1);
            else
                GPU.ReduceMean(GetDevice(Intent.Read), Reduced.GetDevice(Intent.Write), (uint)ElementsSliceReal, (uint)Dims.Z, 1);

            return Reduced;
        }

        public Image AsReducedAlongY()
        {
            Image Reduced = new Image(IntPtr.Zero, new int3(Dims.X, 1, Dims.Z), IsFT, IsComplex, IsHalf);

            if (IsHalf)
                GPU.ReduceMeanHalf(GetDevice(Intent.Read), Reduced.GetDevice(Intent.Write), (uint)(DimsEffective.X * (IsComplex ? 2 : 1)), (uint)Dims.Y, (uint)Dims.Z);
            else
                GPU.ReduceMean(GetDevice(Intent.Read), Reduced.GetDevice(Intent.Write), (uint)(DimsEffective.X * (IsComplex ? 2 : 1)), (uint)Dims.Y, (uint)Dims.Z);

            return Reduced;
        }

        public Image AsPolar(uint innerradius = 0, uint exclusiveouterradius = 0)
        {
            if (IsHalf || IsComplex)
                throw new Exception("Cannot transform fp16 or complex image.");

            if (exclusiveouterradius == 0)
                exclusiveouterradius = (uint)Dims.X / 2;
            exclusiveouterradius = (uint)Math.Min(Dims.X / 2, (int)exclusiveouterradius);
            uint R = exclusiveouterradius - innerradius;

            if (IsFT)
            {
                Image Result = new Image(IntPtr.Zero, new int3((int)R, Dims.Y, Dims.Z));
                GPU.Cart2PolarFFT(GetDevice(Intent.Read), Result.GetDevice(Intent.Write), DimsSlice, innerradius, exclusiveouterradius, (uint) Dims.Z);
                return Result;
            }
            else
            {
                Image Result = new Image(IntPtr.Zero, new int3((int)R, Dims.Y * 2, Dims.Z));
                GPU.Cart2Polar(GetDevice(Intent.Read), Result.GetDevice(Intent.Write), DimsSlice, innerradius, exclusiveouterradius, (uint)Dims.Z);
                return Result;
            }
        }

        public Image AsAmplitudes()
        {
            if (IsHalf || !IsComplex)
                throw new Exception("Data type not supported.");

            Image Amplitudes = new Image(IntPtr.Zero, Dims, IsFT, false, false);
            GPU.Amplitudes(GetDevice(Intent.Read), Amplitudes.GetDevice(Intent.Write), ElementsComplex);

            return Amplitudes;
        }

        public Image AsReal()
        {
            if (!IsComplex)
                throw new Exception("Data must be complex.");

            float[][] Real = new float[Dims.Z][];
            float[][] Complex = GetHost(Intent.Read);
            for (int z = 0; z < Real.Length; z++)
            {
                float[] ComplexSlice = Complex[z];
                float[] RealSlice = new float[ComplexSlice.Length / 2];
                for (int i = 0; i < RealSlice.Length; i++)
                    RealSlice[i] = ComplexSlice[i * 2];

                Real[z] = RealSlice;
            }

            return new Image(Real, Dims, IsFT, false, IsHalf);
        }

        public Image AsImaginary()
        {
            if (!IsComplex)
                throw new Exception("Data must be complex.");

            float[][] Imaginary = new float[Dims.Z][];
            float[][] Complex = GetHost(Intent.Read);
            for (int z = 0; z < Imaginary.Length; z++)
            {
                float[] ComplexSlice = Complex[z];
                float[] ImaginarySlice = new float[ComplexSlice.Length / 2];
                for (int i = 0; i < ImaginarySlice.Length; i++)
                    ImaginarySlice[i] = ComplexSlice[i * 2 + 1];

                Imaginary[z] = ImaginarySlice;
            }

            return new Image(Imaginary, Dims, IsFT, false, IsHalf);
        }

        public Image AsScaledMassive(int2 newSliceDims)
        {
            int3 Scaled = new int3(newSliceDims.X, newSliceDims.Y, Dims.Z);
            Image Output = new Image(Scaled);
            IntPtr OutputDevice = Output.GetDevice(Intent.Write);

            float[][] OriginalHost = GetHost(Intent.Read);
            for (int z = 0; z < Dims.Z; z++)
            {
                Image Slice = new Image(OriginalHost[z]);
                GPU.Scale(Slice.GetDevice(Intent.Read),
                          new IntPtr((long)OutputDevice + newSliceDims.Elements() * sizeof(float) * z),
                          new int3(DimsSlice),
                          new int3(newSliceDims), 
                          1);
                Slice.Dispose();
            }

            return Output;
        }

        public Image AsProjections(float3[] angles, int2 dimsprojection, float supersample)
        {
            if (Dims.X != Dims.Y || Dims.Y != Dims.Z)
                throw new Exception("Volume must be a cube.");

            Image Projections = new Image(IntPtr.Zero, new int3(dimsprojection.X, dimsprojection.Y, angles.Length), true, true);

            GPU.ProjectForward(GetDevice(Intent.Read),
                               Projections.GetDevice(Intent.Write),
                               Dims,
                               dimsprojection,
                               Helper.ToInterleaved(angles),
                               supersample,
                               (uint)angles.Length);

            return Projections;
        }

        public Image AsAnisotropyCorrected(int2 dimsscaled, float majorpixel, float minorpixel, float majorangle, uint supersample)
        {
            Image Corrected = new Image(IntPtr.Zero, new int3(dimsscaled.X, dimsscaled.Y, Dims.Z));

            GPU.CorrectMagAnisotropy(GetDevice(Intent.Read),
                                     DimsSlice,
                                     Corrected.GetDevice(Intent.Write),
                                     dimsscaled,
                                     majorpixel,
                                     minorpixel,
                                     majorangle,
                                     supersample,
                                     (uint)Dims.Z);

            return Corrected;
        }

        public void RemapToFT(bool isvolume = false)
        {
            if (!IsFT && IsComplex)
                throw new Exception("Complex remap only supported for FT layout.");

            int3 WorkDims = isvolume ? Dims : Dims.Slice();
            uint WorkBatch = isvolume ? 1 : (uint)Dims.Z;

            if (IsComplex)
                GPU.RemapToFTComplex(GetDevice(Intent.Read), GetDevice(Intent.Write), WorkDims, WorkBatch);
            else
            {
                if (IsFT)
                    GPU.RemapToFTFloat(GetDevice(Intent.Read), GetDevice(Intent.Write), WorkDims, WorkBatch);
                else
                    GPU.RemapFullToFTFloat(GetDevice(Intent.Read), GetDevice(Intent.Write), WorkDims, WorkBatch);
            }
        }

        public void RemapFromFT(bool isvolume = false)
        {
            if (!IsFT && IsComplex)
                throw new Exception("Complex remap only supported for FT layout.");

            int3 WorkDims = isvolume ? Dims : Dims.Slice();
            uint WorkBatch = isvolume ? 1 : (uint)Dims.Z;

            if (IsComplex)
                GPU.RemapFromFTComplex(GetDevice(Intent.Read), GetDevice(Intent.Write), WorkDims, WorkBatch);
            else
            {
                if (IsFT)
                    GPU.RemapFromFTFloat(GetDevice(Intent.Read), GetDevice(Intent.Write), WorkDims, WorkBatch);
                else
                    GPU.RemapFullFromFTFloat(GetDevice(Intent.Read), GetDevice(Intent.Write), WorkDims, WorkBatch);
            }
        }

        public void Xray(float ndevs)
        {
            if (IsComplex || IsHalf)
                throw new Exception("Complex and half are not supported.");

            for (int i = 0; i < Dims.Z; i++)
                GPU.Xray(new IntPtr((long)GetDevice(Intent.Read) + DimsEffective.ElementsSlice() * i * sizeof (float)),
                         new IntPtr((long)GetDevice(Intent.Write) + DimsEffective.ElementsSlice() * i * sizeof(float)),
                         ndevs,
                         new int2(DimsEffective),
                         1);
        }

        public void Fill(float val)
        {
            float[][] ToFill = GetHost(Intent.Write);
            foreach (float[] Slice in ToFill)
                for (int i = 0; i < Slice.Length; i++)
                    Slice[i] = val;
        }

        public void Sign()
        {
            if (IsHalf)
                throw new Exception("Does not work for fp16.");

            GPU.Sign(GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);
        }

        public void Abs()
        {
            if (IsHalf)
                throw new Exception("Does not work for fp16.");

            GPU.Abs(GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);
        }

        private void Add(Image summands, uint elements, uint batch)
        {
            if (ElementsReal != elements * batch ||
                summands.ElementsReal != elements ||
                IsFT != summands.IsFT ||
                IsComplex != summands.IsComplex)
                throw new DimensionMismatchException();

            if (IsHalf && summands.IsHalf)
            {
                GPU.AddToSlicesHalf(GetDevice(Intent.Read), summands.GetDevice(Intent.Read), GetDevice(Intent.Write), elements, batch);
            }
            else if (!IsHalf && !summands.IsHalf)
            {
                GPU.AddToSlices(GetDevice(Intent.Read), summands.GetDevice(Intent.Read), GetDevice(Intent.Write), elements, batch);
            }
            else
            {
                Image ThisSingle = AsSingle();
                Image SummandsSingle = summands.AsSingle();

                GPU.AddToSlices(ThisSingle.GetDevice(Intent.Read), SummandsSingle.GetDevice(Intent.Read), ThisSingle.GetDevice(Intent.Write), elements, batch);

                if (IsHalf)
                    GPU.HalfToSingle(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), elements * batch);
                else
                    GPU.CopyDeviceToDevice(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), elements * batch);

                ThisSingle.Dispose();
                SummandsSingle.Dispose();
            }
        }

        public void Add(Image summands)
        {
            Add(summands, (uint) ElementsReal, 1);
        }

        public void AddToSlices(Image summands)
        {
            Add(summands, (uint) ElementsSliceReal, (uint) Dims.Z);
        }

        public void AddToLines(Image summands)
        {
            Add(summands, (uint) ElementsLineReal, (uint) (Dims.Y * Dims.Z));
        }

        private void Subtract(Image subtrahends, uint elements, uint batch)
        {
            if (ElementsReal != elements * batch ||
                subtrahends.ElementsReal != elements ||
                IsFT != subtrahends.IsFT ||
                IsComplex != subtrahends.IsComplex)
                throw new DimensionMismatchException();

            if (IsHalf && subtrahends.IsHalf)
            {
                GPU.SubtractFromSlicesHalf(GetDevice(Intent.Read), subtrahends.GetDevice(Intent.Read), GetDevice(Intent.Write), elements, batch);
            }
            else if (!IsHalf && !subtrahends.IsHalf)
            {
                GPU.SubtractFromSlices(GetDevice(Intent.Read), subtrahends.GetDevice(Intent.Read), GetDevice(Intent.Write), elements, batch);
            }
            else
            {
                Image ThisSingle = AsSingle();
                Image SubtrahendsSingle = subtrahends.AsSingle();

                GPU.SubtractFromSlices(ThisSingle.GetDevice(Intent.Read), SubtrahendsSingle.GetDevice(Intent.Read), ThisSingle.GetDevice(Intent.Write), elements, batch);

                if (IsHalf)
                    GPU.HalfToSingle(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), elements * batch);
                else
                    GPU.CopyDeviceToDevice(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), elements * batch);

                ThisSingle.Dispose();
                SubtrahendsSingle.Dispose();
            }
        }

        public void Subtract(Image subtrahends)
        {
            Subtract(subtrahends, (uint) ElementsReal, 1);
        }

        public void SubtractFromSlices(Image subtrahends)
        {
            Subtract(subtrahends, (uint) ElementsSliceReal, (uint) Dims.Z);
        }

        public void SubtractFromLines(Image subtrahends)
        {
            Subtract(subtrahends, (uint) ElementsLineReal, (uint) (Dims.Y * Dims.Z));
        }

        private void Multiply(Image multiplicators, uint elements, uint batch)
        {
            if (ElementsComplex != elements * batch ||
                multiplicators.ElementsComplex != elements ||
                IsFT != multiplicators.IsFT ||
                multiplicators.IsComplex)
                throw new DimensionMismatchException();

            if (!IsComplex)
            {
                if (IsHalf && multiplicators.IsHalf)
                {
                    GPU.MultiplySlicesHalf(GetDevice(Intent.Read), multiplicators.GetDevice(Intent.Read), GetDevice(Intent.Write), elements, batch);
                }
                else if (!IsHalf && !multiplicators.IsHalf)
                {
                    GPU.MultiplySlices(GetDevice(Intent.Read), multiplicators.GetDevice(Intent.Read), GetDevice(Intent.Write), elements, batch);
                }
                else
                {
                    Image ThisSingle = AsSingle();
                    Image MultiplicatorsSingle = multiplicators.AsSingle();

                    GPU.MultiplySlices(ThisSingle.GetDevice(Intent.Read), MultiplicatorsSingle.GetDevice(Intent.Read), ThisSingle.GetDevice(Intent.Write), elements, batch);

                    if (IsHalf)
                        GPU.HalfToSingle(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), elements * batch);
                    else
                        GPU.CopyDeviceToDevice(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), elements * batch);

                    ThisSingle.Dispose();
                    MultiplicatorsSingle.Dispose();
                }
            }
            else
            {
                if (IsHalf)
                    throw new Exception("Complex multiplication not supported for fp16.");
                GPU.MultiplyComplexSlicesByScalar(GetDevice(Intent.Read), multiplicators.GetDevice(Intent.Read), GetDevice(Intent.Write), elements, batch);
            }
        }

        public void Multiply(Image multiplicators)
        {
            Multiply(multiplicators, (uint) ElementsComplex, 1);
        }

        public void MultiplySlices(Image multiplicators)
        {
            Multiply(multiplicators, (uint) ElementsSliceComplex, (uint) Dims.Z);
        }

        public void MultiplyLines(Image multiplicators)
        {
            Multiply(multiplicators, (uint) ElementsLineComplex, (uint) (Dims.Y * Dims.Z));
        }

        private void Divide(Image divisors, uint elements, uint batch)
        {
            if (ElementsComplex != elements * batch ||
                divisors.ElementsComplex != elements ||
                IsFT != divisors.IsFT ||
                divisors.IsComplex)
                throw new DimensionMismatchException();

            if (!IsComplex)
            {
                if (!IsHalf && !divisors.IsHalf)
                {
                    GPU.DivideSlices(GetDevice(Intent.Read), divisors.GetDevice(Intent.Read), GetDevice(Intent.Write), elements, batch);
                }
                else
                {
                    Image ThisSingle = AsSingle();
                    Image DivisorsSingle = divisors.AsSingle();

                    GPU.DivideSlices(ThisSingle.GetDevice(Intent.Read), DivisorsSingle.GetDevice(Intent.Read), ThisSingle.GetDevice(Intent.Write), elements, batch);

                    if (IsHalf)
                        GPU.HalfToSingle(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), elements * batch);
                    else
                        GPU.CopyDeviceToDevice(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), elements * batch);

                    ThisSingle.Dispose();
                    DivisorsSingle.Dispose();
                }
            }
            else
            {
                if (IsHalf)
                    throw new Exception("Complex division not supported for fp16.");
                GPU.DivideComplexSlicesByScalar(GetDevice(Intent.Read), divisors.GetDevice(Intent.Read), GetDevice(Intent.Write), elements, batch);
            }
        }

        public void Divide(Image divisors)
        {
            Divide(divisors, (uint)ElementsComplex, 1);
        }

        public void DivideSlices(Image divisors)
        {
            Divide(divisors, (uint)ElementsSliceComplex, (uint)Dims.Z);
        }

        public void DivideLines(Image divisors)
        {
            Divide(divisors, (uint)ElementsLineComplex, (uint)(Dims.Y * Dims.Z));
        }

        public void ShiftSlices(float3[] shifts)
        {
            if (IsComplex)
                throw new Exception("Cannot shift complex image.");

            IntPtr Data;
            if (!IsHalf)
                Data = GetDevice(Intent.ReadWrite);
            else
            {
                Data = GPU.MallocDevice(ElementsReal);
                GPU.OnMemoryChanged();
                GPU.HalfToSingle(GetDevice(Intent.Read), Data, ElementsReal);
            }
            
            GPU.ShiftStack(Data,
                            Data,
                            DimsEffective.Slice(),
                            Helper.ToInterleaved(shifts), 
                            (uint)Dims.Z);

            if (IsHalf)
            {
                GPU.SingleToHalf(Data, GetDevice(Intent.Write), ElementsReal);
                GPU.FreeDevice(Data);
                GPU.OnMemoryChanged();
            }
        }

        public void ShiftSlicesMassive(float3[] shifts)
        {
            if (IsComplex)
                throw new Exception("Cannot shift complex image.");

            IntPtr Data;
            if (!IsHalf)
                Data = GetDevice(Intent.ReadWrite);
            else
            {
                Data = GPU.MallocDevice(ElementsReal);
                GPU.OnMemoryChanged();
                GPU.HalfToSingle(GetDevice(Intent.Read), Data, ElementsReal);
            }

            for (int b = 0; b < Dims.Z; b++)
                GPU.ShiftStack(new IntPtr((long)Data + ElementsSliceReal * b * sizeof(float)),
                               new IntPtr((long)Data + ElementsSliceReal * b * sizeof(float)),
                               DimsEffective.Slice(),
                               new[] { shifts[b].X, shifts[b].Y, shifts[b].Z },
                               1);

            if (IsHalf)
            {
                GPU.SingleToHalf(Data, GetDevice(Intent.Write), ElementsReal);
                GPU.FreeDevice(Data);
                GPU.OnMemoryChanged();
            }
        }

        public void Bandpass(float nyquistLow, float nyquistHigh, bool isVolume)
        {
            if (IsComplex || IsHalf || IsFT)
                throw new Exception("Bandpass only works on single precision, real data");

            GPU.Bandpass(GetDevice(Intent.Read), GetDevice(Intent.Write), isVolume ? Dims : Dims.Slice(), nyquistLow, nyquistHigh, isVolume ? 1 : (uint)Dims.Z);
        }
    }
    
    [Flags]
    public enum Intent
    {
        Read = 1 << 0,
        Write = 1 << 1,
        ReadWrite = Read | Write
    }
}
