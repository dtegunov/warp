using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Xml;
using System.Xml.XPath;
using Warp.Headers;

namespace Warp.Tools
{
    public static class Helper
    {
        public static IFormatProvider NativeFormat = CultureInfo.InvariantCulture.NumberFormat;
        public static IFormatProvider NativeDateTimeFormat = CultureInfo.InvariantCulture.DateTimeFormat;

        public static float ToRad = (float)Math.PI / 180.0f;
        public static float ToDeg = 180.0f / (float)Math.PI;

        public static void Swap<T>(ref T lhs, ref T rhs)
        {
            T temp;
            temp = lhs;
            lhs = rhs;
            rhs = temp;
        }

        public static float BSpline(float t)
        {
            t = Math.Abs(t);
            float a = 2.0f - t;

            if (t < 1.0f)
                return 2.0f / 3.0f - 0.5f * t * t * a;
            if (t < 2.0f)
                return a * a * a / 6.0f;

            return 0.0f;
        }

        public static float Sinc(float x)
        {
            if (Math.Abs(x) > 1e-8f)
                return (float) (Math.Sin(Math.PI * x) / (Math.PI * x));

            return 1f;
        }

        public static int3[] GetEqualGridSpacing(int2 dimsImage, int2 dimsRegion, float overlapFraction, out int2 dimsGrid)
        {
            int2 dimsoverlap = new int2((int)(dimsRegion.X * (1.0f - overlapFraction)), (int)(dimsRegion.Y * (1.0f - overlapFraction)));
            dimsGrid = new int2(MathHelper.NextMultipleOf(dimsImage.X - (dimsRegion.X - dimsoverlap.X), dimsoverlap.X) / dimsoverlap.X, 
                                MathHelper.NextMultipleOf(dimsImage.Y - (dimsRegion.Y - dimsoverlap.Y), dimsoverlap.Y) / dimsoverlap.Y);

            int2 shift;
            shift.X = dimsGrid.X > 1 ? (int)((dimsImage.X - dimsRegion.X) / (float)(dimsGrid.X - 1)) : (dimsImage.X - dimsRegion.X) / 2;
            shift.Y = dimsGrid.Y > 1 ? (int)((dimsImage.Y - dimsRegion.Y) / (float)(dimsGrid.Y - 1)) : (dimsImage.Y - dimsRegion.Y) / 2;
            int2 offset = new int2((dimsImage.X - shift.X * (dimsGrid.X - 1) - dimsRegion.X) / 2,
                                   (dimsImage.Y - shift.Y * (dimsGrid.Y - 1) - dimsRegion.Y) / 2);

            int3[] h_origins = new int3[dimsGrid.Elements()];

            for (int y = 0; y < dimsGrid.Y; y++)
                for (int x = 0; x < dimsGrid.X; x++)
                    h_origins[y * dimsGrid.X + x] = new int3(x * shift.X + offset.X, y * shift.Y + offset.Y, 0);

            return h_origins;
        }

        public static float[] ToInterleaved(float2[] array)
        {
            float[] Interleaved = new float[array.Length * 2];
            for (int i = 0; i < array.Length; i++)
            {
                Interleaved[i * 2] = array[i].X;
                Interleaved[i * 2 + 1] = array[i].Y;
            }

            return Interleaved;
        }

        public static float[] ToInterleaved(float3[] array)
        {
            float[] Interleaved = new float[array.Length * 3];
            for (int i = 0; i < array.Length; i++)
            {
                Interleaved[i * 3] = array[i].X;
                Interleaved[i * 3 + 1] = array[i].Y;
                Interleaved[i * 3 + 2] = array[i].Z;
            }

            return Interleaved;
        }

        public static int[] ToInterleaved(int2[] array)
        {
            int[] Interleaved = new int[array.Length * 2];
            for (int i = 0; i < array.Length; i++)
            {
                Interleaved[i * 2] = array[i].X;
                Interleaved[i * 2 + 1] = array[i].Y;
            }

            return Interleaved;
        }

        public static int[] ToInterleaved(int3[] array)
        {
            int[] Interleaved = new int[array.Length * 3];
            for (int i = 0; i < array.Length; i++)
            {
                Interleaved[i * 3] = array[i].X;
                Interleaved[i * 3 + 1] = array[i].Y;
                Interleaved[i * 3 + 2] = array[i].Z;
            }

            return Interleaved;
        }

        public static float2[] FromInterleaved2(float[] array)
        {
            float2[] Tuples = new float2[array.Length / 2];
            for (int i = 0; i < Tuples.Length; i++)
                Tuples[i] = new float2(array[i * 2], array[i * 2 + 1]);

            return Tuples;
        }

        public static float3[] FromInterleaved3(float[] array)
        {
            float3[] Tuples = new float3[array.Length / 3];
            for (int i = 0; i < Tuples.Length; i++)
                Tuples[i] = new float3(array[i * 3], array[i * 3 + 1], array[i * 3 + 2]);

            return Tuples;
        }

        public static int2[] FromInterleaved2(int[] array)
        {
            int2[] Tuples = new int2[array.Length / 2];
            for (int i = 0; i < Tuples.Length; i++)
                Tuples[i] = new int2(array[i * 2], array[i * 2 + 1]);

            return Tuples;
        }

        public static int3[] FromInterleaved3(int[] array)
        {
            int3[] Tuples = new int3[array.Length / 3];
            for (int i = 0; i < Tuples.Length; i++)
                Tuples[i] = new int3(array[i * 3], array[i * 3 + 1], array[i * 3 + 2]);

            return Tuples;
        }

        public static void Unzip(float2[] array, out float[] out1, out float[] out2)
        {
            out1 = new float[array.Length];
            out2 = new float[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                out1[i] = array[i].X;
                out2[i] = array[i].Y;
            }
        }

        public static void Unzip(float3[] array, out float[] out1, out float[] out2, out float[] out3)
        {
            out1 = new float[array.Length];
            out2 = new float[array.Length];
            out3 = new float[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                out1[i] = array[i].X;
                out2[i] = array[i].Y;
                out3[i] = array[i].Z;
            }
        }

        public static float2[] Zip(float[] in1, float[] in2)
        {
            float2[] Zipped = new float2[in1.Length];
            for (int i = 0; i < Zipped.Length; i++)
                Zipped[i] = new float2(in1[i], in2[i]);

            return Zipped;
        }

        public static float3[] Zip(float[] in1, float[] in2, float[] in3)
        {
            float3[] Zipped = new float3[in1.Length];
            for (int i = 0; i < Zipped.Length; i++)
                Zipped[i] = new float3(in1[i], in2[i], in3[i]);

            return Zipped;
        }

        public static void Reorder<T>(IList<T> list, int[] indices)
        {
            List<T> OldOrder = new List<T>(list.Count);
            for (int i = 0; i < list.Count; i++)
                OldOrder.Add(list[i]);

            for (int i = 0; i < list.Count; i++)
                list[i] = OldOrder[indices[i]];
        }

        public static void Reorder<T>(T[] array, int[] indices)
        {
            List<T> OldOrder = new List<T>(array.Length);
            for (int i = 0; i < OldOrder.Count; i++)
                OldOrder[i] = array[i];

            for (int i = 0; i < array.Length; i++)
                array[i] = OldOrder[indices[i]];
        }

        public static void ForEachElement(int2 dims, Action<int, int> action)
        {
            for (int y = 0; y < dims.Y; y++)
                for (int x = 0; x < dims.X; x++)
                    action(x, y);
        }

        public static void ForEachElement(int2 dims, Action<int, int, int, int> action)
        {
            for (int y = 0; y < dims.Y; y++)
            {
                int yy = y - dims.Y / 2;

                for (int x = 0; x < dims.X; x++)
                {
                    int xx = x - dims.X / 2;

                    action(x, y, xx, yy);
                }
            }
        }

        public static void ForEachElement(int2 dims, Action<int, int, int, int, float, float> action)
        {
            for (int y = 0; y < dims.Y; y++)
            {
                int yy = y - dims.Y / 2;

                for (int x = 0; x < dims.X; x++)
                {
                    int xx = x - dims.X / 2;

                    action(x, y, xx, yy, (float)Math.Sqrt(xx * xx + yy * yy), (float)Math.Atan2(yy, xx));
                }
            }
        }

        public static void ForEachElementFT(int2 dims, Action<int, int> action)
        {
            for (int y = 0; y < dims.Y; y++)
                for (int x = 0; x < dims.X / 2 + 1; x++)
                    action(x, y);
        }

        public static void ForEachElementFT(int2 dims, Action<int, int, int, int> action)
        {
            for (int y = 0; y < dims.Y; y++)
            {
                int yy = y - dims.Y / 2;

                for (int x = 0; x < dims.X / 2 + 1; x++)
                {
                    int xx = x - dims.X / 2;

                    action(x, y, xx, yy);
                }
            }
        }

        public static void ForEachElementFT(int2 dims, Action<int, int, int, int, float, float> action)
        {
            for (int y = 0; y < dims.Y; y++)
            {
                int yy = y - dims.Y / 2;

                for (int x = 0; x < dims.X / 2 + 1; x++)
                {
                    int xx = x - dims.X / 2;

                    action(x, y, xx, yy, (float)Math.Sqrt(xx * xx + yy * yy), (float)Math.Atan2(yy, xx));
                }
            }
        }
    }
}