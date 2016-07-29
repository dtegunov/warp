#include "Functions.h"
#include <device_functions.h>
using namespace gtom;

#define SHIFT_THREADS 128

__global__ void ShiftGetAverageKernel(float2* d_phase, float2* d_average, float2* d_shiftfactors, float2* d_shifts, uint length, uint probelength, uint nspectra, uint nframes);
__global__ void ShiftGetDiffKernel(float2* d_phase, float2* d_average, float2* d_shiftfactors, uint length, uint probelength, float2* d_shifts, float* d_diff);
__global__ void ShiftGetGradKernel(float2* d_phase, float2* d_average, float2* d_shiftfactors, uint length, uint probelength, float2* d_shifts, float2* d_grad);

/*

Supplied with a stack of frames, extraction positions for sub-regions, and a mask of relevant pixels in Fspace, 
this method extracts portions of each frame, computes the FT, and returns the relevant pixels.

*/

__declspec(dllexport) void CreateShift(float* d_frame,
										int2 dimsframe,
										int nframes,
										int3* h_origins,
										int norigins,
										int2 dimsregion,
										size_t* h_mask,
										uint masklength,
										float2* d_outputall)
{
	int2 dimsunpadded = toInt2(dimsregion.x / 1, dimsregion.y / 1);

	int3* d_origins = (int3*)CudaMallocFromHostArray(h_origins, norigins * sizeof(int3));
	size_t* d_mask = (size_t*)CudaMallocFromHostArray(h_mask, masklength * sizeof(size_t));
	tfloat* d_temp;
	cudaMalloc((void**)&d_temp, norigins * ElementsFFT2(dimsregion) * sizeof(tcomplex));
	tcomplex* d_tempft;
	cudaMalloc((void**)&d_tempft, norigins * ElementsFFT2(dimsregion) * sizeof(tcomplex));
	tcomplex* d_dense;
	cudaMalloc((void**)&d_dense, norigins * masklength * sizeof(tcomplex));

	for (uint z = 0; z < nframes; z++)
	{
		d_ExtractMany(d_frame + Elements2(dimsframe) * z, d_temp, toInt3(dimsframe), toInt3(dimsregion), d_origins, norigins);
		d_NormMonolithic(d_temp, d_temp, Elements2(dimsregion), T_NORM_MEAN01STD, norigins);
		d_HammingMask(d_temp, d_temp, toInt3(dimsregion), NULL, NULL, norigins);
		//d_WriteMRC(d_temp, toInt3(dimsregion.x, dimsregion.y, norigins), "d_shifttemp.mrc");
		d_FFTR2C(d_temp, d_tempft, 2, toInt3(dimsregion), norigins);
		d_RemapHalfFFT2Half(d_tempft, (tcomplex*)d_temp, toInt3(dimsregion), norigins);
		d_Remap((tcomplex*)d_temp, d_mask, d_outputall + masklength * norigins * z, masklength, ElementsFFT2(dimsregion), make_cuComplex(0.0f, 0.0f), norigins);
		//d_ComplexNormalize(d_dense, d_dense, masklength * norigins);
	}

	cudaFree(d_dense);
	cudaFree(d_tempft);
	cudaFree(d_temp);
	cudaFree(d_mask);
	cudaFree(d_origins);
}

__declspec(dllexport) void ShiftGetAverage(float2* d_phase, 
											float2* d_average, 
											float2* d_shiftfactors,
											uint length,  
											uint probelength,
											float2* d_shifts, 
											uint npositions, 
											uint nframes)
{
	float2* d_shiftshalf;
	cudaMalloc((void**)&d_shiftshalf, npositions * nframes * sizeof(float2));
	d_ConvertTFloatTo((float*)d_shifts, (float*)d_shiftshalf, npositions * nframes * 2);
	
	int TpB = tmin(SHIFT_THREADS, NextMultipleOf(length, 32));
	dim3 grid = dim3((length + TpB - 1) / TpB, npositions, 1);
	ShiftGetAverageKernel <<<grid, TpB>>> (d_phase, d_average, d_shiftfactors, d_shiftshalf, length, probelength, npositions, nframes);

	/*float2* d_averagef;
	cudaMalloc((void**)&d_averagef, length * npositions * sizeof(float2));
	d_ConvertToTFloat((half*)d_average, (float*)d_averagef, npositions * length * 2);
	float2* h_averagef = (float2*)MallocFromDeviceArray(d_averagef, length * npositions * sizeof(float2));
	cudaFree(d_averagef);
	
	float2* d_phasef;
	cudaMalloc((void**)&d_phasef, length * npositions * nframes * sizeof(float2));
	d_ConvertToTFloat((half*)d_phase, (float*)d_phasef, npositions * nframes * length * 2);
	float2* h_phasef = (float2*)MallocFromDeviceArray(d_phasef, length * npositions * nframes * sizeof(float2));
	cudaFree(d_phasef);

	free(h_averagef);
	free(h_phasef);*/

	cudaFree(d_shiftshalf);
}

__global__ void ShiftGetAverageKernel(float2* d_phase, float2* d_average, float2* d_shiftfactors, float2* d_shifts, uint length, uint probelength, uint npositions, uint nframes)
{
	d_phase += blockIdx.y * length;
	d_average += blockIdx.y * probelength;
	d_shifts += blockIdx.y;

	__shared__ float2 s_shifts[256];	// 256 frames should be enough for everyone
	for (uint i = threadIdx.x; i < nframes; i += blockDim.x)
		s_shifts[i] = d_shifts[npositions * i];
	__syncthreads();


	for (uint id = blockIdx.x * blockDim.x + threadIdx.x; 
		 id < probelength; 
		 id += gridDim.x * blockDim.x)
	{
		float2 shiftfactors = d_shiftfactors[id];
		float2 sum = make_float2(0.0f, 0.0f);

		for (uint frame = 0; frame < nframes; frame++)
		{
			float2 shift = s_shifts[frame];
			float phase = shiftfactors.x * shift.x + shiftfactors.y * shift.y;
			float2 change = make_float2(__cosf(phase), __sinf(phase));

			float2 value = d_phase[length * npositions * frame + id];
			value = cuCmulf(value, change);

			sum += value;
		}
		
		sum /= nframes;

		d_average[id] = sum;
	}
}

__declspec(dllexport) void ShiftGetDiff(float2* d_phase, 
											float2* d_average, 
											float2* d_shiftfactors, 
											uint length, 
											uint probelength,
											float2* d_shifts,
											float* h_diff, 
											uint npositions, 
											uint nframes)
{
	int TpB = tmin(SHIFT_THREADS, NextMultipleOf(probelength, 32));
	dim3 grid = dim3(npositions, nframes, 1);

	float* d_diff;
	cudaMalloc((void**)&d_diff, npositions * nframes * grid.x * sizeof(float));
	float* d_diffreduced;
	cudaMalloc((void**)&d_diffreduced, npositions * nframes * sizeof(float));

	ShiftGetDiffKernel <<<grid, TpB>>> (d_phase, d_average, d_shiftfactors, length, probelength, d_shifts, d_diff);

	//d_SumMonolithic(d_diff, d_diffreduced, grid.x, npositions * nframes);
	cudaMemcpy(h_diff, d_diff, npositions * nframes * sizeof(float), cudaMemcpyDeviceToHost);
	
	cudaFree(d_diffreduced);
	cudaFree(d_diff);
}

__global__ void ShiftGetDiffKernel(float2* d_phase, float2* d_average, float2* d_shiftfactors, uint length, uint probelength, float2* d_shifts, float* d_diff)
{
	__shared__ float s_diff[SHIFT_THREADS];
	s_diff[threadIdx.x] = 0.0f;
	__shared__ float s_ampsum[SHIFT_THREADS];
	s_ampsum[threadIdx.x] = 0.0f;

	uint specid = blockIdx.y * gridDim.x + blockIdx.x;
	d_phase += specid * length;
	d_average += blockIdx.x * probelength;

	float2 shift = d_shifts[specid];
	float diffsum = 0.0f;
	float ampsum = 0.0f;

	for (uint id = threadIdx.x; id < probelength; id += blockDim.x)
	{
		float2 value = d_phase[id];
		float2 average = d_average[id];

		float2 shiftfactors = d_shiftfactors[id];

		float phase = shiftfactors.x * shift.x + shiftfactors.y * shift.y;
		float2 change = make_float2(__cosf(phase), __sinf(phase));

		value = cuCmulf(value, change);

		float2 valuenorm = value / tmax(1e-10f, sqrt(value.x * value.x + value.y * value.y));
		float avgamp = tmax(1e-10f, sqrt(average.x * average.x + average.y * average.y));
		average /= avgamp;

		float diff = acos(tmax(-1.0f, tmin(valuenorm.x * average.x + valuenorm.y * average.y, 1.0f))) * avgamp;
		//float diff = dotp2(value, average);
		diffsum += diff;
		ampsum += avgamp;
	}

	s_diff[threadIdx.x] = diffsum;
	s_ampsum[threadIdx.x] = ampsum;
	__syncthreads();

	if (threadIdx.x == 0)
	{
		for (uint id = 1; id < blockDim.x; id++)
		{
			diffsum += s_diff[id];
			ampsum += s_ampsum[id];
		}

		d_diff[specid] = diffsum / ampsum;
	}
}

__declspec(dllexport) void ShiftGetGrad(float2* d_phase, 
										float2* d_average, 
										float2* d_shiftfactors, 
										uint length, 
										uint probelength,
										float2* d_shifts,
										float2* h_grad, 
										uint npositions, 
										uint nframes)
{
	int TpB = tmin(SHIFT_THREADS, NextMultipleOf(probelength, 32));
	dim3 grid = dim3(npositions, nframes, 1);

	float2* d_grad;
	cudaMalloc((void**)&d_grad, npositions * nframes * grid.x * sizeof(float2));
	float2* d_gradreduced;
	cudaMalloc((void**)&d_gradreduced, npositions * nframes * sizeof(float2));

	ShiftGetGradKernel <<<grid, TpB>>> (d_phase, d_average, d_shiftfactors, length, probelength, d_shifts, d_grad);

	float2* h_grad2 = (float2*)MallocFromDeviceArray(d_grad, npositions * nframes * grid.x * sizeof(float2));
	free(h_grad2);

	//d_SumMonolithic(d_grad, d_gradreduced, grid.x, npositions * nframes);
	cudaMemcpy(h_grad, d_grad, npositions * nframes * sizeof(float2), cudaMemcpyDeviceToHost);
	
	cudaFree(d_gradreduced);
	cudaFree(d_grad);
}

__global__ void ShiftGetGradKernel(float2* d_phase, 
									float2* d_average, 
									float2* d_shiftfactors, 
									uint length, 
									uint probelength, 
									float2* d_shifts, 
									float2* d_grad)
{
	__shared__ float2 s_grad[SHIFT_THREADS];
	s_grad[threadIdx.x] = make_float2(0.0f, 0.0f);
	__shared__ float s_ampsum[SHIFT_THREADS];
	s_ampsum[threadIdx.x] = 0.0f;

	uint specid = blockIdx.y * gridDim.x + blockIdx.x;
	d_phase += specid * length;
	d_average += blockIdx.x * probelength;

	float2 shift = d_shifts[specid];
	float2 gradsum = make_float2(0.0f, 0.0f);
	float ampsum = 0.0f;

	for (uint id = threadIdx.x; id < probelength; id += blockDim.x)
	{
		float2 value = d_phase[id];
		float2 average = d_average[id];

		float2 shiftfactors = d_shiftfactors[id];
		float weight = tmax(1e-10f, sqrt(average.x * average.x + average.y * average.y));// __half2float(d_weights[id]);

		float phase = shiftfactors.x * shift.x + shiftfactors.y * shift.y;
		float2 change = make_float2(__cosf(phase), __sinf(phase));
		float2 altvalue = cmul(value, change);
		
		gradsum.x += -sgn(altvalue.x * average.y - altvalue.y * average.x) * shiftfactors.x * weight;
		gradsum.y += -sgn(altvalue.x * average.y - altvalue.y * average.x) * shiftfactors.y * weight;
		ampsum += weight;
	}

	s_grad[threadIdx.x] = gradsum;
	s_ampsum[threadIdx.x] = ampsum;
	__syncthreads();

	if (threadIdx.x == 0)
	{
		for (uint id = 1; id < blockDim.x; id++)
		{
			gradsum = gradsum + s_grad[id];
			ampsum = ampsum + s_ampsum[id];
		}

		d_grad[specid] = gradsum / ampsum;
	}
}

__declspec(dllexport) void CreateMotionBlur(float* d_output, int3 dims, float* h_shifts, uint nshifts, uint batch)
{
    d_MotionBlur(d_output, dims, (float3*)h_shifts, nshifts, false, batch);
}