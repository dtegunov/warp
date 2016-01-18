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

        private bool IsDeviceDirty = false;
        private IntPtr _DeviceData = IntPtr.Zero;

        private IntPtr DeviceData
        {
            get
            {
                if (_DeviceData == IntPtr.Zero)
                    _DeviceData = !IsHalf ? GPU.MallocDevice(ElementsReal) : GPU.MallocDeviceHalf(ElementsReal);

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
                    IsDeviceDirty = true;

                return DeviceData;
            }
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
                    IsHostDirty = true;

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

        public void FreeDevice()
        {
            lock (Sync)
            {
                if (_DeviceData != IntPtr.Zero && IsDeviceDirty)
                {
                    for (int z = 0; z < Dims.Z; z++)
                        if (!IsHalf)
                            GPU.CopyDeviceToHost(new IntPtr((long)DeviceData + ElementsSliceReal * z * sizeof(float)), HostData[z], ElementsSliceReal);
                        else
                            GPU.CopyDeviceHalfToHost(new IntPtr((long)DeviceData + ElementsSliceReal * z * sizeof(short)), HostData[z], ElementsSliceReal);
                    GPU.FreeDevice(DeviceData);
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

            float[][] Data = GetHost(Intent.Read);
            float Min = float.MaxValue, Max = -float.MaxValue;
            for (int z = 0; z < Data.Length; z++)
            {
                Min = Math.Min(MathHelper.Min(Data[z]), Min);
                Max = Math.Max(MathHelper.Max(Data[z]), Max);
            }
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
                IntPtr Temp = GPU.MallocDeviceHalf(ElementsReal);
                GPU.SingleToHalf(GetDevice(Intent.Read), Temp, ElementsReal);

                Result = new Image(Temp, Dims, IsFT, IsComplex, true);
                GPU.FreeDevice(Temp);
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
                GPU.HalfToSingle(GetDevice(Intent.Read), Temp, ElementsReal);

                Result = new Image(Temp, Dims, IsFT, IsComplex, false);
                GPU.FreeDevice(Temp);
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

        public void Add(Image summands)
        {
            if (Dims.X != summands.Dims.X ||
                Dims.Y != summands.Dims.Y ||
                Dims.Z != summands.Dims.Z ||
                IsFT != summands.IsFT ||
                IsComplex != summands.IsComplex)
                throw new DimensionMismatchException();

            if (IsHalf && summands.IsHalf)
            {
                GPU.AddHalf(GetDevice(Intent.Read), summands.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);
            }
            else if (!IsHalf && !summands.IsHalf)
            {
                GPU.Add(GetDevice(Intent.Read), summands.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);
            }
            else
            {
                Image ThisSingle = AsSingle();
                Image SummandsSingle = summands.AsSingle();

                GPU.Add(ThisSingle.GetDevice(Intent.Read), SummandsSingle.GetDevice(Intent.Read), ThisSingle.GetDevice(Intent.Write), ElementsReal);

                if (IsHalf)
                    GPU.HalfToSingle(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);
                else
                    GPU.CopyDeviceToDevice(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);

                ThisSingle.Dispose();
                SummandsSingle.Dispose();
            }
        }

        public void AddToSlices(Image summands)
        {
            if (Dims.X != summands.Dims.X ||
                Dims.Y != summands.Dims.Y ||
                IsFT != summands.IsFT ||
                IsComplex != summands.IsComplex)
                throw new DimensionMismatchException();

            if (IsHalf && summands.IsHalf)
            {
                GPU.AddToSlicesHalf(GetDevice(Intent.Read), summands.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsSliceReal, (uint)Dims.Z);
            }
            else if (!IsHalf && !summands.IsHalf)
            {
                GPU.AddToSlices(GetDevice(Intent.Read), summands.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsSliceReal, (uint)Dims.Z);
            }
            else
            {
                Image ThisSingle = AsSingle();
                Image SummandsSingle = summands.AsSingle();

                GPU.AddToSlices(ThisSingle.GetDevice(Intent.Read), SummandsSingle.GetDevice(Intent.Read), ThisSingle.GetDevice(Intent.Write), ElementsSliceReal, (uint)Dims.Z);

                if (IsHalf)
                    GPU.HalfToSingle(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);
                else
                    GPU.CopyDeviceToDevice(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);

                ThisSingle.Dispose();
                SummandsSingle.Dispose();
            }
        }

        public void Subtract(Image subtrahends)
        {
            if (Dims.X != subtrahends.Dims.X ||
                Dims.Y != subtrahends.Dims.Y ||
                Dims.Z != subtrahends.Dims.Z ||
                IsFT != subtrahends.IsFT ||
                IsComplex != subtrahends.IsComplex)
                throw new DimensionMismatchException();

            if (IsHalf && subtrahends.IsHalf)
            {
                GPU.SubtractHalf(GetDevice(Intent.Read), subtrahends.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);
            }
            else if (!IsHalf && !subtrahends.IsHalf)
            {
                GPU.Subtract(GetDevice(Intent.Read), subtrahends.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);
            }
            else
            {
                Image ThisSingle = AsSingle();
                Image SubtrahendsSingle = subtrahends.AsSingle();

                GPU.Subtract(ThisSingle.GetDevice(Intent.Read), SubtrahendsSingle.GetDevice(Intent.Read), ThisSingle.GetDevice(Intent.Write), ElementsReal);

                if (IsHalf)
                    GPU.HalfToSingle(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);
                else
                    GPU.CopyDeviceToDevice(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);

                ThisSingle.Dispose();
                SubtrahendsSingle.Dispose();
            }
        }

        public void SubtractFromSlices(Image subtrahends)
        {
            if (Dims.X != subtrahends.Dims.X ||
                Dims.Y != subtrahends.Dims.Y ||
                IsFT != subtrahends.IsFT ||
                IsComplex != subtrahends.IsComplex)
                throw new DimensionMismatchException();

            if (IsHalf && subtrahends.IsHalf)
            {
                GPU.SubtractFromSlicesHalf(GetDevice(Intent.Read), subtrahends.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsSliceReal, (uint)Dims.Z);
            }
            else if (!IsHalf && !subtrahends.IsHalf)
            {
                GPU.SubtractFromSlices(GetDevice(Intent.Read), subtrahends.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsSliceReal, (uint)Dims.Z);
            }
            else
            {
                Image ThisSingle = AsSingle();
                Image SubtrahendsSingle = subtrahends.AsSingle();

                GPU.SubtractFromSlices(ThisSingle.GetDevice(Intent.Read), SubtrahendsSingle.GetDevice(Intent.Read), ThisSingle.GetDevice(Intent.Write), ElementsSliceReal, (uint)Dims.Z);

                if (IsHalf)
                    GPU.HalfToSingle(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);
                else
                    GPU.CopyDeviceToDevice(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);

                ThisSingle.Dispose();
                SubtrahendsSingle.Dispose();
            }
        }

        public void Multiply(Image multiplicators)
        {
            if (Dims.X != multiplicators.Dims.X ||
                Dims.Y != multiplicators.Dims.Y ||
                Dims.Z != multiplicators.Dims.Z ||
                IsFT != multiplicators.IsFT ||
                IsComplex != multiplicators.IsComplex)
                throw new DimensionMismatchException();

            if (IsHalf && multiplicators.IsHalf)
            {
                GPU.MultiplyHalf(GetDevice(Intent.Read), multiplicators.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);
            }
            else if (!IsHalf && !multiplicators.IsHalf)
            {
                GPU.Multiply(GetDevice(Intent.Read), multiplicators.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);
            }
            else
            {
                Image ThisSingle = AsSingle();
                Image MultiplicatorsSingle = multiplicators.AsSingle();

                GPU.Multiply(ThisSingle.GetDevice(Intent.Read), MultiplicatorsSingle.GetDevice(Intent.Read), ThisSingle.GetDevice(Intent.Write), ElementsReal);

                if (IsHalf)
                    GPU.HalfToSingle(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);
                else
                    GPU.CopyDeviceToDevice(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);

                ThisSingle.Dispose();
                MultiplicatorsSingle.Dispose();
            }
        }

        public void MultiplySlices(Image multiplicators)
        {
            if (Dims.X != multiplicators.Dims.X ||
                Dims.Y != multiplicators.Dims.Y ||
                IsFT != multiplicators.IsFT ||
                IsComplex != multiplicators.IsComplex)
                throw new DimensionMismatchException();

            if (IsHalf && multiplicators.IsHalf)
            {
                GPU.MultiplySlicesHalf(GetDevice(Intent.Read), multiplicators.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsSliceReal, (uint)Dims.Z);
            }
            else if (!IsHalf && !multiplicators.IsHalf)
            {
                GPU.MultiplySlices(GetDevice(Intent.Read), multiplicators.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsSliceReal, (uint)Dims.Z);
            }
            else
            {
                Image ThisSingle = AsSingle();
                Image MultiplicatorsSingle = multiplicators.AsSingle();

                GPU.MultiplySlices(ThisSingle.GetDevice(Intent.Read), MultiplicatorsSingle.GetDevice(Intent.Read), ThisSingle.GetDevice(Intent.Write), ElementsSliceReal, (uint)Dims.Z);

                if (IsHalf)
                    GPU.HalfToSingle(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);
                else
                    GPU.CopyDeviceToDevice(ThisSingle.GetDevice(Intent.Read), GetDevice(Intent.Write), ElementsReal);

                ThisSingle.Dispose();
                MultiplicatorsSingle.Dispose();
            }
        }

        public void ShiftSlices(float3[] shifts)
        {
            if (IsComplex)
                throw new Exception("Cannot shift complex image.");

            IntPtr Data;
            if (!IsHalf)
                Data = GetDevice(Intent.Write);
            else
            {
                Data = GPU.MallocDevice(ElementsReal);
                GPU.HalfToSingle(GetDevice(Intent.Read), Data, ElementsReal);
            }

            GPU.ShiftStack(Data, Data, new int3(DimsEffective.X, DimsEffective.Y, 1), Helper.ToInterleaved(shifts), (uint)Dims.Z);

            if (IsHalf)
            {
                GPU.SingleToHalf(Data, GetDevice(Intent.Write), ElementsReal);
                GPU.FreeDevice(Data);
            }
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
