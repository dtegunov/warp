#include "Functions.h"
using namespace gtom;


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

__declspec(dllexport) void Add(float* d_input, float* d_summands, float* d_output, size_t elements)
{
	d_AddVector(d_input, d_summands, d_output, elements);
}

__declspec(dllexport) void AddToSlices(float* d_input, float* d_summands, float* d_output, size_t sliceelements, uint slices)
{
	d_AddVector(d_input, d_summands, d_output, sliceelements, slices);
}

__declspec(dllexport) void Subtract(float* d_input, float* d_subtrahends, float* d_output, size_t elements)
{
	d_SubtractVector(d_input, d_subtrahends, d_output, elements);
}

__declspec(dllexport) void SubtractFromSlices(float* d_input, float* d_subtrahends, float* d_output, size_t sliceelements, uint slices)
{
	d_SubtractVector(d_input, d_subtrahends, d_output, sliceelements, slices);
}

__declspec(dllexport) void Multiply(float* d_input, float* d_multiplicators, float* d_output, size_t elements)
{
	d_MultiplyByVector(d_input, d_multiplicators, d_output, elements);
}

__declspec(dllexport) void MultiplySlices(float* d_input, float* d_multiplicators, float* d_output, size_t sliceelements, uint slices)
{
	d_MultiplyByVector(d_input, d_multiplicators, d_output, sliceelements, slices);
}

__declspec(dllexport) void Divide(float* d_input, float* d_divisors, float* d_output, size_t elements)
{
	d_MultiplyByVector(d_input, d_divisors, d_output, elements);
}

__declspec(dllexport) void DivideSlices(float* d_input, float* d_divisors, float* d_output, size_t sliceelements, uint slices)
{
	d_MultiplyByVector(d_input, d_divisors, d_output, sliceelements, slices);
}

__declspec(dllexport) void AddHalf(half* d_input, half* d_summands, half* d_output, size_t elements)
{
	d_AddVector(d_input, d_summands, d_output, elements);
}

__declspec(dllexport) void AddToSlicesHalf(half* d_input, half* d_summands, half* d_output, size_t sliceelements, uint slices)
{
	d_AddVector(d_input, d_summands, d_output, sliceelements, slices);
}

__declspec(dllexport) void SubtractHalf(half* d_input, half* d_subtrahends, half* d_output, size_t elements)
{
	d_SubtractVector(d_input, d_subtrahends, d_output, elements);
}

__declspec(dllexport) void SubtractFromSlicesHalf(half* d_input, half* d_subtrahends, half* d_output, size_t sliceelements, uint slices)
{
	d_SubtractVector(d_input, d_subtrahends, d_output, sliceelements, slices);
}

__declspec(dllexport) void MultiplyHalf(half* d_input, half* d_multiplicators, half* d_output, size_t elements)
{
	d_MultiplyByVector(d_input, d_multiplicators, d_output, elements);
}

__declspec(dllexport) void MultiplySlicesHalf(half* d_input, half* d_multiplicators, half* d_output, size_t sliceelements, uint slices)
{
	d_MultiplyByVector(d_input, d_multiplicators, d_output, sliceelements, slices);
}