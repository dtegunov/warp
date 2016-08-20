#include "Functions.h"
using namespace gtom;

__global__ void ScaleNormCorrSumKernel(half2* d_simcoords, half* d_sim, half* d_scale, half* d_target, CTFParamsLean* d_params, float* d_scores, uint length);

/*

Supplied with a stack of frames, and extraction positions for sub-regions, this method 
extracts portions of each frame, computes the FT, and averages the results as follows:

-3D full fitting: d_output contains all individual spectra from each frame
-2D spatial fitting: d_output contains averages for all positions over all frames
-1D temporal fitting: d_output contains averages for all frames over all positions
-0D no fitting: d_output is NULL

*/

__declspec(dllexport) void CreateSpectra(float* d_frame, 
										int2 dimsframe, 
										int nframes, 
										int3* h_origins, 
										int norigins, 
										int2 dimsregion, 
										int3 ctfgrid, 
										float* d_outputall,
										float* d_outputmean)
{

	int3* d_origins = (int3*)CudaMallocFromHostArray(h_origins, norigins * sizeof(int3));
	tfloat* d_tempspectra;
	cudaMalloc((void**)&d_tempspectra, tmax(norigins, nframes) * ElementsFFT2(dimsregion) * sizeof(tfloat));
	tfloat* d_tempaverages;
	cudaMalloc((void**)&d_tempaverages, nframes * ElementsFFT2(dimsregion) * sizeof(tfloat));

	bool ctfspace = ctfgrid.x * ctfgrid.y > 1;
	bool ctftime = ctfgrid.z > 1;
	int nspectra = (ctfspace || ctftime) ? (ctfspace ? norigins : 1) * (ctftime ? ctfgrid.z : 1) : 1;

	int pertimegroup = nframes / ctfgrid.z;

	// Temp spectra will be summed up to be averaged later
	d_ValueFill(d_outputall, ElementsFFT2(dimsregion) * nspectra, 0.0f);

	for (int z = 0; z < nframes; z++)
	{
		// Full precision, just write everything to output which is big enough
		//if (ctfspace && ctftime)
		//{
		//	d_CTFPeriodogram(d_frame + Elements2(dimsframe) * z, dimsframe, d_origins, norigins, dimsregion, dimsregion, d_outputall + ElementsFFT2(dimsregion) * norigins * z, false);			
		//	d_AddScalar(d_outputall + ElementsFFT2(dimsregion) * norigins * z, d_outputall + ElementsFFT2(dimsregion) * norigins * z, ElementsFFT2(dimsregion) * norigins, 1e2f);
		//	d_Log(d_outputall + ElementsFFT2(dimsregion) * norigins * z, d_outputall + ElementsFFT2(dimsregion) * norigins * z, ElementsFFT2(dimsregion) * norigins);
		//}
		//else // Partial or no precision
		{
			int framegroup = z / pertimegroup;
			if (framegroup >= ctfgrid.z)
				break;

			// Write spectra to temp and reduce them to a temporary average spectrum
			d_CTFPeriodogram(d_frame + Elements2(dimsframe) * z, dimsframe, d_origins, norigins, dimsregion, dimsregion, d_tempspectra, false);
			d_AddScalar(d_tempspectra, d_tempspectra, ElementsFFT2(dimsregion) * norigins, 1e2f);
			d_Log(d_tempspectra, d_tempspectra, ElementsFFT2(dimsregion) * norigins);

			//d_WriteMRC(d_tempspectra, toInt3(dimsregion.x / 2 + 1, dimsregion.y, norigins), "d_tempspectra.mrc");

			d_ReduceMean(d_tempspectra, d_tempaverages + ElementsFFT2(dimsregion) * z, ElementsFFT2(dimsregion), norigins);

			// Spatially resolved, add to output which has norigins spectra
			//if (ctfspace)
			{
				d_AddVector(d_outputall + ElementsFFT2(dimsregion) * norigins * framegroup, d_tempspectra, d_outputall + ElementsFFT2(dimsregion) * norigins * framegroup, ElementsFFT2(dimsregion) * norigins);
			}
			// Temporally resolved, each spectrum will be the average of the entire frame's spectra (= temporary average, so just copy)
			//else if (ctftime)
			//{
			//	cudaMemcpy(d_outputall + ElementsFFT2(dimsregion) * z, d_tempaverages + ElementsFFT2(dimsregion) * z, ElementsFFT2(dimsregion) * sizeof(float), cudaMemcpyDeviceToDevice);
			//}
		}
	}

	// Just average over all individual spectra in d_outputall
	//if (ctfspace && ctftime)
		d_DivideByScalar(d_outputall, d_outputall, ElementsFFT2(dimsregion) * Elements(ctfgrid), (tfloat)pertimegroup);
		//d_ReduceMean(d_outputall, d_outputmean, ElementsFFT2(dimsregion), Elements(ctfgrid));
	//else
	//{
	//	// Average output is average of temporary averages
		d_ReduceMean(d_tempaverages, d_outputmean, ElementsFFT2(dimsregion), nframes);
	//
	//	// Those were summed up, so divide by number of summands
	//	if (ctfspace)

	//}
	//d_WriteMRC(d_outputmean, toInt3FFT(dimsregion), "d_outputmean.mrc");
	
	// 0D case, only one average spectrum in outputall
	//if (nspectra == 1)
	//	cudaMemcpy(d_outputall, d_outputmean, ElementsFFT2(dimsregion) * sizeof(float), cudaMemcpyDeviceToDevice);

	cudaFree(d_origins);
	cudaFree(d_tempspectra);
	cudaFree(d_tempaverages);
}

__declspec(dllexport) CTFParams CTFFitMean(float* d_ps, float2* d_pscoords, int2 dims, CTFParams startparams, CTFFitParams fp, bool doastigmatism)
{
	std::vector<std::pair<tfloat, CTFParams>> fits;
	tfloat score;
	tfloat scoremean;
	tfloat scorestd;

	d_CTFFit(d_ps, d_pscoords, dims, &startparams, 1, fp, 2, fits, score, scoremean, scorestd);

	CTFParams result;
	for (int i = 0; i < 12; i++)
		((tfloat*)&result)[i] = ((tfloat*)&startparams)[i] + ((tfloat*)&(fits[0].second))[i];

	result.Bfactor = score;

	return result;
}

__declspec(dllexport) void CTFMakeAverage(float* d_ps, float2* d_pscoords, uint length, uint sidelength, CTFParams* h_sourceparams, CTFParams targetparams, uint minbin, uint maxbin, int* h_consider, uint batch, float* d_output)
{
	uint nbins = maxbin - minbin;
	if (batch > 1)
		d_CTFRotationalAverageToTarget((tfloat*)d_ps, d_pscoords, length, sidelength, h_sourceparams, targetparams, d_output, minbin, maxbin, h_consider, batch);
	else
		d_CTFRotationalAverageToTarget((tfloat*)d_ps, d_pscoords, length, sidelength, h_sourceparams, targetparams, d_output, minbin, maxbin, NULL, 1);
}

__declspec(dllexport) void CTFCompareToSim(half* d_ps, half2* d_pscoords, half* d_scale, uint length, CTFParams* h_sourceparams, float* h_scores, uint batch)
{
	half* d_sim;
	cudaMalloc((void**)&d_sim, length * batch * sizeof(float));
	float* d_scores;
	cudaMalloc((void**)&d_scores, batch * sizeof(float));

	CTFParamsLean* h_lean;
	cudaMallocHost((void**)&h_lean, batch * sizeof(CTFParamsLean));
	#pragma omp parallel for
	for (int i = 0; i < batch; i++)
		h_lean[i] = CTFParamsLean(h_sourceparams[i], toInt3(1, 1, 1));	// Sidelength and pixelsize are already included in d_addresses
	CTFParamsLean* d_lean = (CTFParamsLean*)CudaMallocFromHostArray(h_lean, batch * sizeof(CTFParamsLean));
	cudaFreeHost(h_lean);

	//d_CTFSimulate(h_sourceparams, d_pscoords, d_sim, length, true, batch);

	int TpB = 128;
	dim3 grid = dim3(batch, 1, 1);
	ScaleNormCorrSumKernel <<<grid, TpB>>> (d_pscoords, d_sim, d_scale, d_ps, d_lean, d_scores, length);

	//d_MultiplyByVector(d_sim, d_scale, d_sim, length, batch);
	//d_NormMonolithic(d_sim, d_sim, length, T_NORM_MEAN01STD, batch);
	//d_WriteMRC(d_sim, toInt3(207, 512, 1), "d_sim.mrc");
	//d_WriteMRC(d_ps, toInt3(207, 512, 1), "d_ps.mrc");

	//d_MultiplyByVector(d_ps, d_sim, d_sim, length * batch);

	//d_SumMonolithic(d_sim, d_scores, length, batch);

	cudaMemcpy(h_scores, d_scores, batch * sizeof(float), cudaMemcpyDeviceToHost);
	cudaFree(d_lean);
	cudaFree(d_sim);
	cudaFree(d_scores);

	//for (uint i = 0; i < batch; i++)
		//h_scores[i] /= (float)length;
}

__global__ void ScaleNormCorrSumKernel(half2* d_simcoords, half* d_sim, half* d_scale, half* d_target, CTFParamsLean* d_params, float* d_scores, uint length)
{
	__shared__ float s_sums1[128];
	__shared__ float s_sums2[128];
	__shared__ float s_mean, s_stddev;

	d_sim += blockIdx.x * length;
	d_target += blockIdx.x * length;

	CTFParamsLean params = d_params[blockIdx.x];

	float sum1 = 0.0, sum2 = 0.0;
	for (uint i = threadIdx.x; i < length; i += blockDim.x)
	{
		float2 simcoords = __half22float2(d_simcoords[i]);
		float pixelsize = params.pixelsize + params.pixeldelta * __cosf(2.0f * (simcoords.y - params.pixelangle));
		simcoords.x /= pixelsize;

		float val = d_GetCTF<true, false>(simcoords.x, simcoords.y, params) * __half2float(d_scale[i]);
		d_sim[i] = __float2half(val);
		sum1 += val;
		sum2 += val * val;
	}
	s_sums1[threadIdx.x] = sum1;
	s_sums2[threadIdx.x] = sum2;
	__syncthreads();

	if (threadIdx.x == 0)
	{
		for (int i = 1; i < 128; i++)
		{
			sum1 += s_sums1[i];
			sum2 += s_sums2[i];
		}

		s_mean = sum1 / (float)length;
		s_stddev = sqrt(((float)length * sum2 - (sum1 * sum1))) / (float)length;
	}
	__syncthreads();

	float mean = s_mean;
	float stddev = s_stddev > 0.0f ? 1.0f / s_stddev : 0.0f;

	sum1 = 0.0f;
	for (uint i = threadIdx.x; i < length; i += blockDim.x)
		sum1 += (__half2float(d_sim[i]) - mean) * stddev * __half2float(d_target[i]);
	s_sums1[threadIdx.x] = sum1;
	__syncthreads();

	if (threadIdx.x == 0)
	{
		for (int i = 1; i < 128; i++)
			sum1 += s_sums1[i];

		d_scores[blockIdx.x] = sum1 / (float)length;
	}
}