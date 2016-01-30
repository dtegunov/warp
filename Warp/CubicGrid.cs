using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using System.Xml;
using System.Xml.XPath;
using Warp.Tools;

namespace Warp
{
    public class CubicGrid
    {
        public readonly int3 Dimensions;
        public readonly float3 Spacing;
        public float[,,] Values;

        public float[] FlatValues
        {
            get
            {
                float[] Result = new float[Dimensions.Elements()];

                for (int z = 0; z < Dimensions.Z; z++)
                    for (int y = 0; y < Dimensions.Y; y++)
                        for (int x = 0; x < Dimensions.X; x++)
                            Result[(z * Dimensions.Y + y) * Dimensions.X + x] = Values[z, y, x];

                return Result;
            }
        }

        public CubicGrid(int3 dimensions, float valueMin, float valueMax, Dimension gradientDirection)
        {
            Dimensions = dimensions;
            Values = new float[dimensions.Z, dimensions.Y, dimensions.X];
            float Step = valueMax - valueMin;
            if (gradientDirection == Dimension.X)
                Step /= Math.Max(1, dimensions.X - 1);
            else if (gradientDirection == Dimension.Y)
                Step /= Math.Max(1, dimensions.Y - 1);
            else if (gradientDirection == Dimension.Z)
                Step /= Math.Max(1, dimensions.Z - 1);

            for (int z = 0; z < dimensions.Z; z++)
                for (int y = 0; y < dimensions.Y; y++)
                    for (int x = 0; x < dimensions.X; x++)
                    {
                        float Value = valueMin;
                        if (gradientDirection == Dimension.X)
                            Value += x * Step;
                        if (gradientDirection == Dimension.Y)
                            Value += y * Step;
                        if (gradientDirection == Dimension.Z)
                            Value += z * Step;

                        Values[z, y, x] = Value;
                    }

            Spacing = new float3(1f / Math.Max(1, dimensions.X - 1), 1f / Math.Max(1, dimensions.Y - 1), 1f / Math.Max(1, dimensions.Z - 1));
        }

        public CubicGrid(float[,,] values)
        {
            Values = values;
            Dimensions = new int3(values.GetLength(2), values.GetLength(1), values.GetLength(0));
            Spacing = new float3(1f / Math.Max(1, Dimensions.X - 1), 1f / Math.Max(1, Dimensions.Y - 1), 1f / Math.Max(1, Dimensions.Z - 1));
        }

        public CubicGrid(int3 dimensions)
        {
            Values = new float[dimensions.Z, dimensions.Y, dimensions.X];
            Dimensions = dimensions;
            Spacing = new float3(1f / Math.Max(1, Dimensions.X - 1), 1f / Math.Max(1, Dimensions.Y - 1), 1f / Math.Max(1, Dimensions.Z - 1));
        }

        public CubicGrid(int3 dimensions, float[] values)
        {
            Values = new float[dimensions.Z, dimensions.Y, dimensions.X];
            Dimensions = dimensions;
            Spacing = new float3(1f / Math.Max(1, Dimensions.X - 1), 1f / Math.Max(1, Dimensions.Y - 1), 1f / Math.Max(1, Dimensions.Z - 1));

            for (int z = 0; z < Dimensions.Z; z++)
                for (int y = 0; y < Dimensions.Y; y++)
                    for (int x = 0; x < Dimensions.X; x++)
                        Values[z, y, x] = values[(z * Dimensions.Y + y) * Dimensions.X + x];
        }

        public float GetInterpolated(float3 coords)
        {
            coords /= Spacing;  // from [0, 1] to [0, dim - 1]
            
            float3 coord_grid = coords;
            float3 index = coord_grid.Floor();
            
            float result = 0.0f;

            int MinX = Math.Max(0, (int)index.X - 1), MaxX = Math.Min((int)index.X + 2, Dimensions.X - 1);
            int MinY = Math.Max(0, (int)index.Y - 1), MaxY = Math.Min((int)index.Y + 2, Dimensions.Y - 1);
            int MinZ = Math.Max(0, (int)index.Z - 1), MaxZ = Math.Min((int)index.Z + 2, Dimensions.Z - 1);

            float[,] InterpX = new float[MaxZ - MinZ + 1, MaxY - MinY + 1];
            for (int z = MinZ; z <= MaxZ; z++)
            {
                for (int y = MinY; y <= MaxY; y++)
                {
                    float2[] Points = new float2[MaxX - MinX + 1];
                    if (Points.Length == 1)
                        InterpX[z - MinZ, y - MinY] = Values[z, y, 0];
                    else
                    {
                        for (int x = MinX; x <= MaxX; x++)
                            Points[x - MinX] = new float2(x, Values[z, y, x]);
                        Cubic1DShort Spline = Cubic1DShort.GetInterpolator(Points);
                        InterpX[z - MinZ, y - MinY] = Spline.Interp(coords.X);
                    }
                }
            }

            float[] InterpXY = new float[MaxZ - MinZ + 1];
            for (int z = MinZ; z <= MaxZ; z++)
            {
                float2[] Points = new float2[MaxY - MinY + 1];
                if (Points.Length == 1)
                    InterpXY[z - MinZ] = InterpX[z - MinZ, 0];
                else
                { 
                    for (int y = MinY; y <= MaxY; y++)
                        Points[y - MinY] = new float2(y, InterpX[z - MinZ, y - MinY]);
                    Cubic1DShort Spline = Cubic1DShort.GetInterpolator(Points);
                    InterpXY[z - MinZ] = Spline.Interp(coords.Y);
                }
            }

            {
                float2[] Points = new float2[MaxZ - MinZ + 1];
                if (Points.Length == 1)
                    result = InterpXY[0];
                else
                {
                    for (int z = MinZ; z <= MaxZ; z++)
                        Points[z - MinZ] = new float2(z, InterpXY[z - MinZ]);
                    Cubic1DShort Spline = Cubic1DShort.GetInterpolator(Points);
                    result = Spline.Interp(coords.Z);
                }
            }

            return result;
        }

        public float[] GetInterpolated(int3 valueGrid, float3 overlapFraction)
        {
            overlapFraction = new float3(1f - overlapFraction.X, 1f - overlapFraction.Y, 1f - overlapFraction.Z);
            float[] Result = new float[valueGrid.Elements()];

            float OverallStepsX = 1f + (valueGrid.X - 1) / overlapFraction.X;
            float StepX = 1f / OverallStepsX;
            float OffsetX = StepX / overlapFraction.X * 0.5f;

            float OverallStepsY = 1f + (valueGrid.Y - 1) / overlapFraction.Y;
            float StepY = 1f / OverallStepsY;
            float OffsetY = StepY / overlapFraction.Y * 0.5f;

            float StepZ = 1f / Math.Max(valueGrid.Z - 1, 1);
            float OffsetZ = valueGrid.Z == 1 ? 0.5f : 0f;
            
            for (int z = 0, i = 0; z < valueGrid.Z; z++)
                for (int y = 0; y < valueGrid.Y; y++)
                    for (int x = 0; x < valueGrid.X; x++, i++)
                        Result[i] = GetInterpolated(new float3(x * StepX + OffsetX, y * StepY + OffsetY, z * StepZ + OffsetZ));

            return Result;
        }

        public float[] GetInterpolatedNative(int3 valueGrid, float3 overlapFraction)
        {
            overlapFraction = new float3(1f - overlapFraction.X, 1f - overlapFraction.Y, 1f - overlapFraction.Z);
            float[] Result = new float[valueGrid.Elements()];

            float OverallStepsX = 1f + (valueGrid.X - 1) / overlapFraction.X;
            float StepX = 1f / OverallStepsX;
            float OffsetX = StepX / overlapFraction.X * 0.5f;

            float OverallStepsY = 1f + (valueGrid.Y - 1) / overlapFraction.Y;
            float StepY = 1f / OverallStepsY;
            float OffsetY = StepY / overlapFraction.Y * 0.5f;
            
            float StepZ = 1f / Math.Max(valueGrid.Z - 1, 1);
            float OffsetZ = valueGrid.Z == 1 ? 0.5f : 0f;

            CPU.CubicInterpOnGrid(Dimensions, FlatValues, Spacing, valueGrid, new float3(StepX, StepY, StepZ), new float3(OffsetX, OffsetY, OffsetZ), Result);

            return Result;
        }

        public CubicGrid Resize(int3 newSize)
        {
            float[] Result = new float[newSize.Elements()];

            float StepX = 1f / Math.Max(1, newSize.X - 1);
            float StepY = 1f / Math.Max(1, newSize.Y - 1);
            float StepZ = 1f / Math.Max(1, newSize.Z - 1);

            for (int z = 0, i = 0; z < newSize.Z; z++)
                for (int y = 0; y < newSize.Y; y++)
                    for (int x = 0; x < newSize.X; x++, i++)
                        Result[i] = GetInterpolated(new float3(x * StepX, y * StepY, z * StepZ));

            return new CubicGrid(newSize, Result);
        }

        public CubicGrid CollapseXY()
        {
            float[] Collapsed = new float[Dimensions.Z];
            for (int z = 0; z < Collapsed.Length; z++)
            {
                float Mean = 0;
                for (int y = 0; y < Dimensions.Y; y++)
                    for (int x = 0; x < Dimensions.X; x++)
                        Mean += Values[z, y, x];

                Mean /= Dimensions.ElementsSlice();
                Collapsed[z] = Mean;
            }

            return new CubicGrid(new int3(1, 1, Dimensions.Z), Collapsed);
        }

        public CubicGrid CollapseZ()
        {
            float[] Collapsed = new float[Dimensions.ElementsSlice()];
            for (int y = 0; y < Dimensions.Y; y++)
            {
                for (int x = 0; x < Dimensions.X; x++)
                {
                    float Mean = 0;
                    for (int z = 0; z < Dimensions.Z; z++)
                        Mean += Values[z, y, x];

                    Mean /= Dimensions.Z;
                    Collapsed[y * Dimensions.X + x] = Mean;
                }
            }

            return new CubicGrid(Dimensions.Slice(), Collapsed);
        }

        public float[][] GetWiggleWeights(int3 valueGrid, float3 overlapFraction)
        {
            float[][] Result = new float[Dimensions.Elements()][];

            for (int i = 0; i < Result.Length; i++)
            {
                float[] PlusValues = new float[Dimensions.Elements()];
                PlusValues[i] = 1f;
                CubicGrid PlusGrid = new CubicGrid(Dimensions, PlusValues);

                Result[i] = PlusGrid.GetInterpolatedNative(valueGrid, overlapFraction);
            }

            return Result;
        }

        public float[] GetSliceXY(int z)
        {
            float[] Result = new float[Dimensions.X * Dimensions.Y];
            for (int y = 0; y < Dimensions.Y; y++)
                for (int x = 0; x < Dimensions.X; x++)
                    Result[y * Dimensions.X + x] = Values[z, y, x];

            return Result;
        }

        public float[] GetSliceXZ(int y)
        {
            float[] Result = new float[Dimensions.X * Dimensions.Z];
            for (int z = 0; z < Dimensions.Z; z++)
                for (int x = 0; x < Dimensions.X; x++)
                    Result[z * Dimensions.X + x] = Values[z, y, x];

            return Result;
        }

        public float[] GetSliceYZ(int x)
        {
            float[] Result = new float[Dimensions.Y * Dimensions.Z];
            for (int z = 0; z < Dimensions.Z; z++)
                for (int y = 0; y < Dimensions.Y; y++)
                    Result[z * Dimensions.Y + y] = Values[z, y, x];

            return Result;
        }

        public void Save(XmlTextWriter writer)
        {
            XMLHelper.WriteAttribute(writer, "Width", Dimensions.X.ToString());
            XMLHelper.WriteAttribute(writer, "Height", Dimensions.Y.ToString());
            XMLHelper.WriteAttribute(writer, "Depth", Dimensions.Z.ToString());

            for (int z = 0; z < Dimensions.Z; z++)
                for (int y = 0; y < Dimensions.Y; y++)
                    for (int x = 0; x < Dimensions.X; x++)
                    {
                        writer.WriteStartElement("Node");
                        XMLHelper.WriteAttribute(writer, "X", x.ToString());
                        XMLHelper.WriteAttribute(writer, "Y", y.ToString());
                        XMLHelper.WriteAttribute(writer, "Z", z.ToString());
                        XMLHelper.WriteAttribute(writer, "Value", Values[z, y, x].ToString(CultureInfo.InvariantCulture));
                        writer.WriteEndElement();
                    }
        }

        public static CubicGrid Load(XPathNavigator nav)
        {
            int3 Dimensions = new int3(int.Parse(nav.GetAttribute("Width", ""), CultureInfo.InvariantCulture),
                                       int.Parse(nav.GetAttribute("Height", ""), CultureInfo.InvariantCulture),
                                       int.Parse(nav.GetAttribute("Depth", ""), CultureInfo.InvariantCulture));

            float[,,] Values = new float[Dimensions.Z, Dimensions.Y, Dimensions.X];

            foreach (XPathNavigator nodeNav in nav.SelectChildren("Node", ""))
            {
                //try
                {
                    int X = int.Parse(nodeNav.GetAttribute("X", ""), CultureInfo.InvariantCulture);
                    int Y = int.Parse(nodeNav.GetAttribute("Y", ""), CultureInfo.InvariantCulture);
                    int Z = int.Parse(nodeNav.GetAttribute("Z", ""), CultureInfo.InvariantCulture);
                    float Value = float.Parse(nodeNav.GetAttribute("Value", ""), CultureInfo.InvariantCulture);

                    Values[Z, Y, X] = Value;
                }
                //catch { }
            }

            return new CubicGrid(Values);
        }
    }

    public enum Dimension
    {
        X,
        Y,
        Z
    }
}
