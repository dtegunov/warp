using System;
using System.Runtime.InteropServices;
using Warp.Tools;

namespace Warp
{
    public static class CPU
    {
        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CubicInterpOnGrid")]
        public static extern void CubicInterpOnGrid(int3 dimensions, float[] values, float3 spacing, int3 valueGrid, float3 step, float3 offset, float[] output);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CubicInterpIrregular")]
        public static extern void CubicInterpIrregular(int3 dimensions, float[] values, float[] positions, int npositions, float3 spacing, float[] output);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "InitProjector")]
        public static extern void InitProjector(int3 dims, int oversampling, float[] data, float[] initialized);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "BackprojectorReconstruct")]
        public static extern void BackprojectorReconstruct(int3 dimsori, int oversampling, float[] h_data, float[] h_weights, [MarshalAs(UnmanagedType.AnsiBStr)] string c_symmetry, bool do_reconstruct_ctf, float[] h_reconstruction);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "GetAnglesCount")]
        public static extern int GetAnglesCount(int healpixorder, [MarshalAs(UnmanagedType.AnsiBStr)] string c_symmetry = "C1", float limittilt = -91);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "GetAngles")]
        public static extern void GetAngles(float[] h_angles, int healpixorder, [MarshalAs(UnmanagedType.AnsiBStr)] string c_symmetry = "C1", float limittilt = -91);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "OptimizeWeights")]
        public static extern void OptimizeWeights(int nrecs,
                                                  float[] h_recft,
                                                  float[] h_recweights,
                                                  float[] h_r2,
                                                  int elements,
                                                  int[] h_subsets,
                                                  float[] h_bfacs,
                                                  float[] h_weightfactors,
                                                  float[] h_recsum1,
                                                  float[] h_recsum2,
                                                  float[] h_weightsum1,
                                                  float[] h_weightsum2);
    }
}
