using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Warp.Tools
{
    public static class MathHelper
    {
        public static float Mean(IEnumerable<float> data)
        {
            double Sum = data.Sum(i => i);
            return (float)Sum / data.Count();
        }

        public static float2 Mean(IEnumerable<float2> data)
        {
            float2 Sum = new float2(0, 0);
            foreach (var p in data)
                Sum += p;

            return Sum / data.Count();
        }

        public static float StdDev(IEnumerable<float> data)
        {
            double Sum = 0f, Sum2 = 0f;
            foreach (var i in data)
            {
                Sum += i;
                Sum2 += i * i;
            }

            return (float)Math.Sqrt(data.Count() * Sum2 - Sum * Sum) / data.Count();
        }

        public static float2 MeanAndStd(IEnumerable<float> data)
        {
            double Sum = 0f, Sum2 = 0f;
            foreach (var i in data)
            {
                Sum += i;
                Sum2 += i * i;
            }

            return new float2((float)Sum / data.Count(), (float)Math.Sqrt(data.Count() * Sum2 - Sum * Sum) / data.Count());
        }

        public static float[] Normalize(float[] data)
        {
            double Sum = 0f, Sum2 = 0f;
            foreach (var i in data)
            {
                Sum += i;
                Sum2 += i * i;
            }

            float Std = (float)Math.Sqrt(data.Length * Sum2 - Sum * Sum) / data.Count();
            float Avg = (float) Sum / data.Length;

            float[] Result = new float[data.Length];
            for (int i = 0; i < Result.Length; i++)
                Result[i] = (data[i] - Avg) / Std;

            return Result;
        }

        public static void NormalizeInPlace(float[] data)
        {
            double Sum = 0f, Sum2 = 0f;
            foreach (var i in data)
            {
                Sum += i;
                Sum2 += i * i;
            }

            float Std = (float)Math.Sqrt(data.Length * Sum2 - Sum * Sum) / data.Length;
            float Avg = (float)Sum / data.Length;
            
            for (int i = 0; i < data.Length; i++)
                data[i] = (data[i] - Avg) / Std;
        }

        public static float CrossCorrelate(float[] data1, float[] data2)
        {
            return Mult(data1, data2).Sum() / data1.Length;
        }

        public static float CrossCorrelateNormalized(float[] data1, float[] data2)
        {
            return CrossCorrelate(Normalize(data1), Normalize(data2));
        }

        public static float Min(IEnumerable<float> data)
        {
            float Min = float.MaxValue;
            return data.Aggregate(Min, (start, i) => Math.Min(start, i));
        }

        public static float Max(IEnumerable<float> data)
        {
            float Max = -float.MaxValue;
            return data.Aggregate(Max, (start, i) => Math.Max(start, i));
        }

        public static float[] Plus(float[] data1, float[] data2)
        {
            float[] Result = new float[data1.Length];
            for (int i = 0; i < Result.Length; i++)
                Result[i] = data1[i] + data2[i];

            return Result;
        }

        public static float[] Minus(float[] data1, float[] data2)
        {
            float[] Result = new float[data1.Length];
            for (int i = 0; i < Result.Length; i++)
                Result[i] = data1[i] - data2[i];

            return Result;
        }

        public static float[] Mult(float[] data1, float[] data2)
        {
            float[] Result = new float[data1.Length];
            for (int i = 0; i < Result.Length; i++)
                Result[i] = data1[i] * data2[i];

            return Result;
        }

        public static float[] Div(float[] data1, float[] data2)
        {
            float[] Result = new float[data1.Length];
            for (int i = 0; i < Result.Length; i++)
                Result[i] = data1[i] / data2[i];

            return Result;
        }

        public static float[] Diff(float[] data)
        {
            float[] D = new float[data.Length - 1];
            for (int i = 0; i < data.Length - 1; i++)
                D[i] = data[i + 1] - data[i];

            return D;
        }

        public static double[] Diff(double[] data)
        {
            double[] D = new double[data.Length - 1];
            for (int i = 0; i < data.Length - 1; i++)
                D[i] = data[i + 1] - data[i];

            return D;
        }

        public static int NextMultipleOf(int value, int factor)
        {
            return ((value + factor - 1) / factor) * factor;
        }

        public static float ReduceWeighted(float[] data, float[] weights)
        {
            float Sum = 0f;
            float Weightsum = 0f;
            unsafe
            {
                fixed (float* dataPtr = data)
                fixed (float* weightsPtr = weights)
                {
                    float* dataP = dataPtr;
                    float* weightsP = weightsPtr;

                    for (int i = 0; i < data.Length; i++)
                    {
                        Sum += *dataP++ * *weightsP;
                        Weightsum += *weightsP++;
                    }
                }
            }

            if (Math.Abs(Weightsum) > 1e-6f)
                return Sum;// / Weightsum;
            else
                return Sum;
        }

        public static void UnNaN(float[] data)
        {
            for (int i = 0; i < data.Length; i++)
                if (float.IsNaN(data[i]))
                    data[i] = 0;
        }

        public static void UnNaN(float2[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (float.IsNaN(data[i].X))
                    data[i].X = 0;
                if (float.IsNaN(data[i].Y))
                    data[i].Y = 0;
            }
        }

        public static float ResidualFraction(float value)
        {
            return value - (int)value;
        }

        public static float Median(IEnumerable<float> data)
        {
            List<float> Sorted = new List<float>(data);
            Sorted.Sort();

            return Sorted[Sorted.Count / 2];
        }

        public static float[] WithinNStd(float[] data, float nstd)
        {
            float Mean = MathHelper.Mean(data);
            float Std = StdDev(data) * nstd;

            List<float> Result = data.Where(t => Math.Abs(t - Mean) <= Std).ToList();

            return Result.ToArray();
        }

        public static float[] WithinNStdFromMedian(float[] data, float nstd)
        {
            float Mean = Median(data);
            float Std = StdDev(data) * nstd;

            List<float> Result = data.Where(t => Math.Abs(t - Mean) <= Std).ToList();

            return Result.ToArray();
        }

        public static int[] WithinNStdFromMedianIndices(float[] data, float nstd)
        {
            float Mean = Median(data);
            float Std = StdDev(data) * nstd;

            List<int> Result = new List<int>();

            for (int i = 0; i < data.Length; i++)
                if (Math.Abs(data[i] - Mean) <= Std)
                    Result.Add(i);

            return Result.ToArray();
        }
    }
}
