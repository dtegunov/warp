#include "Functions.h"
#include "../../gtom/include/CubicInterp.cuh"
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

__declspec(dllexport) void RemapFullToFTFloat(float* d_input, float* d_output, int3 dims, uint batch)
{
    d_RemapFullFFT2Full(d_input, d_output, dims, batch);
}

__declspec(dllexport) void RemapFullFromFTFloat(float* d_input, float* d_output, int3 dims, uint batch)
{
    d_RemapFull2FullFFT(d_input, d_output, dims, batch);
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

__declspec(dllexport) void NormalizeMasked(float* d_ps, float* d_output, float* d_mask, uint length, uint batch)
{
	d_NormMonolithic(d_ps, d_output, length, d_mask, T_NORM_MEAN01STD, batch);
}

__declspec(dllexport) void SphereMask(float* d_input, float* d_output, int3 dims, float radius, float sigma, uint batch)
{
	d_SphereMask(d_input, d_output, dims, &radius, sigma, NULL, batch);
}

__declspec(dllexport) void CreateCTF(float* d_output, float2* d_coords, uint length, CTFParams* h_params, bool amplitudesquared, uint batch)
{
	d_CTFSimulate(h_params, d_coords, d_output, length, amplitudesquared, false, batch);
}

__declspec(dllexport) void Resize(float* d_input, int3 dimsinput, float* d_output, int3 dimsoutput, uint batch)
{
	d_Scale(d_input, d_output, dimsinput, dimsoutput, T_INTERP_FOURIER, NULL, NULL, batch);
}

__declspec(dllexport) void ShiftStack(float* d_input, float* d_output, int3 dims, float* h_shifts, uint batch)
{
	d_Shift(d_input, d_output, dims, (tfloat3*)h_shifts, NULL, NULL, NULL, batch);
}

__declspec(dllexport) void ShiftStackMassive(float* d_input, float* d_output, int3 dims, float* h_shifts, uint batch)
{
	cufftHandle planforw = d_FFTR2CGetPlan(DimensionCount(dims), dims);
	cufftHandle planback = d_IFFTC2RGetPlan(DimensionCount(dims), dims);
	float2* d_intermediate;
	cudaMalloc((void**)&d_intermediate, ElementsFFT(dims) * sizeof(float2));

	for (int b = 0; b < batch; b++)
		d_Shift(d_input + Elements(dims) * b, d_output + Elements(dims) * b, dims, (tfloat3*)h_shifts + b, &planforw, &planback, d_intermediate);

	cufftDestroy(planforw);
	cufftDestroy(planback);
	cudaFree(d_intermediate);
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

__declspec(dllexport) void Sum(float* d_input, float* d_output, uint length, uint batch)
{
    d_SumMonolithic(d_input, d_output, length, batch);
}

__declspec(dllexport) void Abs(float* d_input, float* d_output, size_t length)
{
    d_Abs(d_input, d_output, length);
}

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
	d_DivideSafeByVector(d_input, d_divisors, d_output, sliceelements, slices);
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
	d_ComplexDivideSafeByVector(d_input, d_multiplicators, d_output, sliceelements, slices);
}

__declspec(dllexport) void Scale(float* d_input, float* d_output, int3 dimsinput, int3 dimsoutput, uint batch)
{
	d_Scale(d_input, d_output, dimsinput, dimsoutput, T_INTERP_FOURIER, NULL, NULL, batch);
}

__declspec(dllexport) void ProjectForward(float2* d_inputft, float2* d_outputft, int3 dimsinput, int2 dimsoutput, float3* h_angles, float supersample, uint batch)
{
    d_rlnProject(d_inputft, dimsinput, d_outputft, toInt3(dimsoutput), (tfloat3*)h_angles, supersample, batch);
}

__declspec(dllexport) void ProjectBackward(float2* d_volumeft, float* d_volumeweights, int3 dimsvolume, float2* d_projft, float* d_projweights, int2 dimsproj, int rmax, float3* h_angles, float supersample, uint batch)
{
	/*tfloat* d_amps = CudaMallocValueFilled(ElementsFFT(dimsvolume), (tfloat)0);
	d_Abs(d_projft, d_amps, ElementsFFT2(dimsproj));
	d_WriteMRC(d_amps, toInt3FFT(dimsproj), "d_amps.mrc");

	d_WriteMRC(d_projweights, toInt3(dimsproj.x / 2 + 1, dimsproj.y, batch), "d_projweights.mrc");

	tfloat* d_dummy = CudaMallocValueFilled(ElementsFFT2(dimsproj) * batch * 2, 1.0f);*/

    d_rlnBackproject(d_volumeft, d_volumeweights, dimsvolume, d_projft, d_projweights, toInt3(dimsproj), rmax, (tfloat3*)h_angles, supersample, batch);

	/*d_Abs(d_volumeft, d_amps, ElementsFFT(dimsvolume));
	d_WriteMRC(d_amps, toInt3FFT(dimsvolume), "d_volamps.mrc");*/
}

__declspec(dllexport) void Bandpass(float* d_input, float* d_output, int3 dims, float nyquistlow, float nyquisthigh, uint batch)
{
    d_BandpassNonCubic(d_input, d_output, dims, nyquistlow, nyquisthigh, batch);
}

__declspec(dllexport) void Rotate2D(float* d_input, float* d_output, int2 dims, float* h_angles, int oversample, uint batch)
{
	if (oversample <= 1)
	{
		d_Rotate2D(d_input, d_output, dims, h_angles, T_INTERP_CUBIC, true, batch);
	}
	else
	{
		int2 dimspadded = dims * oversample;
	    float* d_temp;
		cudaMalloc((void**)&d_temp, Elements2(dimspadded) * sizeof(float));

		for (int b = 0; b < batch; b++)
		{
		    d_Scale(d_input + Elements2(dims) * b, d_temp, toInt3(dims), toInt3(dimspadded), T_INTERP_FOURIER);
			d_Rotate2D(d_temp, d_temp, dimspadded, h_angles + b, T_INTERP_CUBIC, true, 1);
			d_Scale(d_temp, d_output + Elements2(dims) * b, toInt3(dimspadded), toInt3(dims), T_INTERP_FOURIER);
		}

		cudaFree(d_temp);
	}
}

__global__ void ShiftAndRotate2DKernel(float* d_input, float* d_output, int2 dims, int2 dimsori, glm::mat3* d_transforms)
{
	uint idx = blockIdx.x * blockDim.x + threadIdx.x;
	if (idx >= dims.x)
		return;
	uint idy = blockIdx.y * blockDim.y + threadIdx.y;
	if (idy >= dims.y)
		return;

	d_input += Elements2(dimsori) * blockIdx.z;

	int x, y;
	x = idx;
	y = idy;

	glm::vec3 pos = d_transforms[blockIdx.z] * glm::vec3(x - dims.x / 2, y - dims.y / 2, 1.0f) + glm::vec3(dimsori.x / 2, dimsori.y / 2, 0.0f);
	
	float val = 0;
	if (pos.x >= 0 && pos.x < dims.x && pos.y >= 0 && pos.y < dims.y)
	{
	    int x0 = floor(pos.x);
		int x1 = tmin(x0 + 1, dims.x - 1);
		pos.x -= x0;

		int y0 = floor(pos.y);
		int y1 = tmin(y0 + 1, dims.y - 1);
		pos.y -= y0;

		float d000 = d_input[y0 * dimsori.x + x0];
		float d001 = d_input[y0 * dimsori.x + x1];
		float d010 = d_input[y1 * dimsori.x + x0];
		float d011 = d_input[y1 * dimsori.x + x1];

		float dx00 = lerp(d000, d001, pos.x);
		float dx01 = lerp(d010, d011, pos.x);

		val = lerp(dx00, dx01, pos.y);
	}

	d_output[(blockIdx.z * dims.y + idy) * dims.x + idx] = val;
}

__declspec(dllexport) void ShiftAndRotate2D(float* d_input, float* d_output, int2 dims, float2* h_shifts, float* h_angles, uint batch)
{
	glm::mat3* h_transforms = (glm::mat3*)malloc(batch * sizeof(glm::mat3));
	for (uint b = 0; b < batch; b++)
		h_transforms[b] = Matrix3RotationZ(-h_angles[b]) * Matrix3Translation(tfloat2(-h_shifts[b].x, -h_shifts[b].y));
	glm::mat3* d_transforms = (glm::mat3*)CudaMallocFromHostArray(h_transforms, batch * sizeof(glm::mat3));
	free(h_transforms);

	dim3 TpB = dim3(16, 16);
	dim3 grid = dim3((dims.x + 15) / 16, (dims.y + 15) / 16, batch);

	ShiftAndRotate2DKernel << <grid, TpB >> > (d_input, d_output, dims, dims * 1, d_transforms);

	cudaFree(d_transforms);
}

__declspec(dllexport) int CreateFFTPlan(int3 dims, uint batch)
{
    return d_FFTR2CGetPlan(DimensionCount(dims), dims, batch);
}

__declspec(dllexport) int CreateIFFTPlan(int3 dims, uint batch)
{
    return d_IFFTC2RGetPlan(DimensionCount(dims), dims, batch);
}

__declspec(dllexport) void DestroyFFTPlan(cufftHandle plan)
{
    cufftDestroy(plan);
}