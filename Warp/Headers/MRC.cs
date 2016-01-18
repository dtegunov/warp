﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Warp.Tools;

namespace Warp.Headers
{
    public enum MRCDataType
    {
        Byte = 0,
        Short = 1,
        Float = 2,
        ShortComplex = 3,
        FloatComplex = 4,
        UnsignedShort = 6,
        RGB = 16
    }

    public class HeaderMRC : MapHeader
    {
        public MRCDataType Mode = MRCDataType.Float;
        public int3 StartSubImage = new int3(0, 0, 0);
        public int3 Griddimensions = new int3(1, 1, 1);
        public float3 Pixelsize = new float3(1f, 1f, 1f);
        public float3 Angles;
        public int3 MapOrder = new int3(1, 2, 3);

        public float MinValue;
        public float MaxValue;
        public float MeanValue;
        public int SpaceGroup;

        public int ExtendedBytes;
        public short CreatorID;

        public byte[] ExtraData1 = new byte[30];

        public short NInt;
        public short NReal;

        public byte[] ExtraData2 = new byte[28];

        public short IDType;
        public short Lens;
        public short ND1;
        public short ND2;
        public short VD1;
        public short VD2;

        public float3 TiltOriginal;
        public float3 TiltCurrent;
        public float3 Origin;

        public byte[] CMap = new byte[] { (byte)'M', (byte)'A', (byte)'P', (byte)' ' };
        public byte[] Stamp = new byte[] { 67, 65, 0, 0 };

        public float StdDevValue;

        public int NumLabels;
        public byte[][] Labels = new byte[10][];

        public byte[] Extended;

        public HeaderMRC()
        {
            for (int i = 0; i < Labels.Length; i++)
                Labels[i] = new byte[80];
        }

        public HeaderMRC(BinaryReader reader)
        {
            Dimensions = new int3(reader.ReadBytes(3 * sizeof(int)));
            Mode = (MRCDataType)reader.ReadInt32();
            StartSubImage = new int3(reader.ReadBytes(3 * sizeof(int)));
            Griddimensions = new int3(reader.ReadBytes(3 * sizeof(int)));
            Pixelsize = new float3(reader.ReadBytes(3 * sizeof(float)));
            Angles = new float3(reader.ReadBytes(3 * sizeof(float)));
            MapOrder = new int3(reader.ReadBytes(3 * sizeof(int)));

            MinValue = reader.ReadSingle();
            MaxValue = reader.ReadSingle();
            MeanValue = reader.ReadSingle();
            SpaceGroup = reader.ReadInt32();

            ExtendedBytes = reader.ReadInt32();
            CreatorID = reader.ReadInt16();

            ExtraData1 = reader.ReadBytes(30);

            NInt = reader.ReadInt16();
            NReal = reader.ReadInt16();

            ExtraData2 = reader.ReadBytes(28);

            IDType = reader.ReadInt16();
            Lens = reader.ReadInt16();
            ND1 = reader.ReadInt16();
            ND2 = reader.ReadInt16();
            VD1 = reader.ReadInt16();
            VD2 = reader.ReadInt16();

            TiltOriginal = new float3(reader.ReadBytes(3 * sizeof(float)));
            TiltCurrent = new float3(reader.ReadBytes(3 * sizeof(float)));
            Origin = new float3(reader.ReadBytes(3 * sizeof(float)));

            CMap = reader.ReadBytes(4);
            Stamp = reader.ReadBytes(4);

            StdDevValue = reader.ReadSingle();

            NumLabels = reader.ReadInt32();
            for (int i = 0; i < 10; i++)
                Labels[i] = reader.ReadBytes(80);

            Extended = reader.ReadBytes(ExtendedBytes);
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(Dimensions);
            writer.Write((int)Mode);
            writer.Write(StartSubImage);
            writer.Write(Griddimensions);
            writer.Write(Pixelsize);
            writer.Write(Angles);
            writer.Write(MapOrder);

            writer.Write(MinValue);
            writer.Write(MaxValue);
            writer.Write(MeanValue);
            writer.Write(SpaceGroup);

            if (Extended != null)
                writer.Write(Extended.Length);
            else
                writer.Write(0);
            writer.Write(CreatorID);

            writer.Write(ExtraData1);

            writer.Write(NInt);
            writer.Write(NReal);

            writer.Write(ExtraData2);

            writer.Write(IDType);
            writer.Write(Lens);
            writer.Write(ND1);
            writer.Write(ND2);
            writer.Write(VD1);
            writer.Write(VD2);

            writer.Write(TiltOriginal);
            writer.Write(TiltCurrent);
            writer.Write(Origin);

            writer.Write(CMap);
            writer.Write(Stamp);

            writer.Write(StdDevValue);

            writer.Write(NumLabels);
            for (int i = 0; i < Labels.Length; i++)
                writer.Write(Labels[i]);

            if (Extended != null)
                writer.Write(Extended);
        }

        public override Type GetValueType()
        {
            switch (Mode)
            {
                case MRCDataType.Byte:
                    return typeof(byte);
                case MRCDataType.Float:
                    return typeof(float);
                case MRCDataType.FloatComplex:
                    return typeof(float);
                case MRCDataType.RGB:
                    return typeof(byte);
                case MRCDataType.Short:
                    return typeof(short);
                case MRCDataType.ShortComplex:
                    return typeof(short);
                case MRCDataType.UnsignedShort:
                    return typeof(ushort);
            }

            throw new Exception("Unknown data type.");
        }

        public override void SetValueType(Type t)
        {
            if (t == typeof(byte))
                Mode = MRCDataType.Byte;
            else if (t == typeof(float))
                Mode = MRCDataType.Float;
            else if (t == typeof(short))
                Mode = MRCDataType.Short;
            else if (t == typeof(ushort))
                Mode = MRCDataType.UnsignedShort;
            else
                throw new Exception("Unknown data type.");
        }
    }
}
