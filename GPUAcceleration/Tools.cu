#include "Functions.h"
using namespace gtom;

__declspec(dllexport) void FFT(float* d_input, float2* d_output, int3 dims, uint batch)
{
    d_FFTR2C(d_input, d_output, DimensionCount(dims), dims, batch);
}

__declspec(dllexport) void IFFT(float2* d_input, float* d_output, int3 dims, uint batch)
{
    d_IFFTC2R(d_input, d_output, DimensionCount(dims), dims, batch);
}

__declspec(dllexport) void Pad(float* d_input, float* d_output, int3 olddims, int3 newdims, uint batch)
{
    d_Pad(d_input, d_output, olddims, newdims, T_PAD_VALUE, 0.0f, batch);
}

__declspec(dllexport) void PadFT(float2* d_input, float2* d_output, int3 olddims, int3 newdims, uint batch)
{
    d_FFTPad(d_input, d_output, olddims, newdims, batch);
}

__declspec(dllexport) void CropFT(float2* d_input, float2* d_output, int3 olddims, int3 newdims, uint batch)
{
    d_FFTCrop(d_input, d_output, olddims, newdims, batch);
}

__declspec(dllexport) void RemapToFTComplex(float2* d_input, float2* d_output, int3 dims, uint batch)
{
    d_RemapHalfFFT2Half(d_input, d_output, dims, batch);
}

__declspec(dllexport) void RemapToFTFloat(float* d_input, float* d_output, int3 dims, uint batch)
{
    d_RemapHalfFFT2Half(d_input, d_output, dims, batch);
}

__declspec(dllexport) void RemapFromFTComplex(float2* d_input, float2* d_output, int3 dims, uint batch)
{
    d_RemapHalf2HalfFFT(d_input, d_output, dims, batch);
}

__declspec(dllexport) void RemapFromFTFloat(float* d_input, float* d_output, int3 dims, uint batch)
{
    d_RemapHalf2HalfFFT(d_input, d_output, dims, batch);
}

__declspec(dllexport) void Extract(float* d_input, float* d_output, int3 dims, int3 dimsregion, int3* h_origins, uint batch)
{
	int3* d_origins = (int3*)CudaMallocFromHostArray(h_origins, batch * sizeof(int3));

	d_ExtractMany(d_input, d_output, dims, dimsregion, d_origins, batch);

	cudaFree(d_origins);
}

__declspec(dllexport) void ExtractHalf(half* d_input, half* d_output, int3 dims, int3 dimsregion, int3* h_origins, uint batch)
{
	int3* d_origins = (int3*)CudaMallocFromHostArray(h_origins, batch * sizeof(int3));

	d_ExtractMany(d_input, d_output, dims, dimsregion, d_origins, batch);

	cudaFree(d_origins);
}

__declspec(dllexport) void ReduceMean(float* d_input, float* d_output, uint vectorlength, uint nvectors, uint batch)
{
	d_ReduceMean(d_input, d_output, vectorlength, nvectors, batch);
}

__declspec(dllexport) void ReduceMeanHalf(half* d_input, half* d_output, uint vectorlength, uint nvectors, uint batch)
{
	d_ReduceMean(d_input, d_output, vectorlength, nvectors, batch);
}

__declspec(dllexport) void Normalize(float* d_ps, float* d_output, uint length, uint batch)
{
	d_NormMonolithic(d_ps, d_output, length, T_NORM_MEAN01STD, batch);
}

__declspec(dllexport) void CreateCTF(float* d_output, float2* d_coords, uint length, CTFParams* h_params, bool amplitudesquared, uint batch)
{
	d_CTFSimulate(h_params, d_coords, d_output, length, amplitudesquared, batch);
}

__declspec(dllexport) void Resize(float* d_input, int3 dimsinput, float* d_output, int3 dimsoutput, uint batch)
{
	d_Scale(d_input, d_output, dimsinput, dimsoutput, T_INTERP_FOURIER, NULL, NULL, batch);
}

__declspec(dllexport) void ShiftStack(float* d_input, float* d_output, int3 dims, float3* h_shifts, uint batch)
{
	d_Shift(d_input, d_output, dims, (tfloat3*)h_shifts, NULL, NULL, NULL, batch);
}

__declspec(dllexport) void Cart2Polar(float* d_input, float* d_output, int2 dims, uint innerradius, uint exclusiveouterradius, uint batch)
{
	d_Cart2Polar(d_input, d_output, dims, T_INTERP_LINEAR, innerradius, exclusiveouterradius, batch);
}

__declspec(dllexport) void Cart2PolarFFT(float* d_input, float* d_output, int2 dims, uint innerradius, uint exclusiveouterradius, uint batch)
{
	d_Cart2PolarFFT(d_input, d_output, dims, T_INTERP_LINEAR, innerradius, exclusiveouterradius, batch);
}

__declspec(dllexport) void Xray(float* d_input, float* d_output, float ndevs, int2 dims, uint batch)
{
    d_Xray(d_input, d_output, toInt3(dims), ndevs, 5, batch);
}

// Arithmetics:

__declspec(dllexport) void Amplitudes(float2* d_input, float* d_output, size_t length)
{
    d_Abs(d_input, d_output, length);
}

__declspec(dllexport) void Sign(float* d_input, float* d_output, size_t length)
{
    d_Sign(d_input, d_output, length);
}

__declspec(dllexport) void AddToSlices(float* d_input, float* d_summands, float* d_output, size_t sliceelements, uint slices)
{
	d_AddVector(d_input, d_summands, d_output, sliceelements, slices);
}

__declspec(dllexport) void SubtractFromSlices(float* d_input, float* d_subtrahends, float* d_output, size_t sliceelements, uint slices)
{
	d_SubtractVector(d_input, d_subtrahends, d_output, sliceelements, slices);
}

__declspec(dllexport) void MultiplySlices(float* d_input, float* d_multiplicators, float* d_output, size_t sliceelements, uint slices)
{
	d_MultiplyByVector(d_input, d_multiplicators, d_output, sliceelements, slices);
}

__declspec(dllexport) void DivideSlices(float* d_input, float* d_divisors, float* d_output, size_t sliceelements, uint slices)
{
	d_DivideByVector(d_input, d_divisors, d_output, sliceelements, slices);
}

__declspec(dllexport) void AddToSlicesHalf(half* d_input, half* d_summands, half* d_output, size_t sliceelements, uint slices)
{
	d_AddVector(d_input, d_summands, d_output, sliceelements, slices);
}

__declspec(dllexport) void SubtractFromSlicesHalf(half* d_input, half* d_subtrahends, half* d_output, size_t sliceelements, uint slices)
{
	d_SubtractVector(d_input, d_subtrahends, d_output, sliceelements, slices);
}

__declspec(dllexport) void MultiplySlicesHalf(half* d_input, half* d_multiplicators, half* d_output, size_t sliceelements, uint slices)
{
	d_MultiplyByVector(d_input, d_multiplicators, d_output, sliceelements, slices);
}

__declspec(dllexport) void MultiplyComplexSlicesByScalar(float2* d_input, float* d_multiplicators, float2* d_output, size_t sliceelements, uint slices)
{
	d_ComplexMultiplyByVector(d_input, d_multiplicators, d_output, sliceelements, slices);
}

__declspec(dllexport) void DivideComplexSlicesByScalar(float2* d_input, float* d_multiplicators, float2* d_output, size_t sliceelements, uint slices)
{
	d_ComplexDivideByVector(d_input, d_multiplicators, d_output, sliceelements, slices);
}