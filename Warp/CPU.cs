﻿using System;
using System.Runtime.InteropServices;
using Warp.Tools;

namespace Warp
{
    public static class CPU
    {
        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CubicInterpOnGrid")]
        public static extern void CubicInterpOnGrid(int3 dimensions, float[] values, float3 spacing, int3 valueGrid, float3 step, float3 offset, float[] output);
    }
}