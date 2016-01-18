#ifndef FUNCTIONS_H
#define FUNCTIONS_H

#include "../../gtom/include/GTOM.cuh"

using namespace std;

#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include <math.h>
#include <iostream>
#include <sstream>
#include <fstream>
#include <vector>
#include <set>

// CTF.cpp:
extern "C" __declspec(dllexport) void CreateSpectra(float* d_frame, 
													int2 dimsframe, 
													int nframes, 
													int3* h_origins, 
													int norigins, 
													int2 dimsregion, 
													int3 ctfgrid,
													int binmin, int binmax,
													float* d_outputall, 
													float* d_outputalltrimmed,
													float* d_outputmean, 
													float* d_outputmeanpolar, 
													float* d_output1d);

extern "C" __declspec(dllexport) gtom::CTFParams CTFFitMean(float* d_ps, 
											  			    float2* d_pscoords, 
														    int2 dims,
														    gtom::CTFParams startparams,
														    gtom::CTFFitParams fp, 
														    bool doastigmatism);

extern "C" __declspec(dllexport) void CTFMakeAverage(float* d_ps, 
													 float2* d_pscoords, 
													 uint length, 
													 uint sidelength, 
													 gtom::CTFParams* h_sourceparams, 
													 gtom::CTFParams targetparams, 
													 uint minbin, 
													 uint maxbin, 
													 int* h_consider,
													 uint batch, 
													 float* d_output);

extern "C" __declspec(dllexport) void CTFCompareToSim(half* d_ps, 
													  half2* d_pscoords,
													  half* d_scale,
													  uint length, 
													  gtom::CTFParams* h_sourceparams, 
													  float* h_scores,
													  uint batch);

extern "C" __declspec(dllexport) void CTFSubtractBackground(float* d_ps, 
															float* d_background, 
															uint length, 
															float* d_output, 
															uint batch);

extern "C" __declspec(dllexport) void CTFNormalize(float* d_ps, 
												   float* d_output, 
												   uint length, 
												   uint batch);

// Cubic.cpp:

extern "C" __declspec(dllexport) void __stdcall CubicInterpOnGrid(int3 dimensions, 
																	float* values, 
																	float3 spacing, 
																	int3 valueGrid, 
																	float3 step, 
																	float3 offset, 
																	float* output);

// Device.cpp:

extern "C" __declspec(dllexport) int __stdcall GetDeviceCount();
extern "C" __declspec(dllexport) void __stdcall SetDevice(int device);
extern "C" __declspec(dllexport) long __stdcall GetFreeMemory();
extern "C" __declspec(dllexport) long __stdcall GetTotalMemory();

// Memory.cpp:

extern "C" __declspec(dllexport) float* __stdcall MallocDevice(long elements);
extern "C" __declspec(dllexport) float* __stdcall MallocDeviceFromHost(float* h_data, long elements);
extern "C" __declspec(dllexport) void* __stdcall MallocDeviceHalf(long elements);
extern "C" __declspec(dllexport) void* __stdcall MallocDeviceHalfFromHost(float* h_data, long elements);

extern "C" __declspec(dllexport) void __stdcall FreeDevice(void* d_data);

extern "C" __declspec(dllexport) void __stdcall CopyDeviceToHost(float* d_source, float* h_dest, long elements);
extern "C" __declspec(dllexport) void __stdcall CopyDeviceHalfToHost(half* d_source, float* h_dest, long elements);
extern "C" __declspec(dllexport) void __stdcall CopyDeviceToDevice(float* d_source, float* d_dest, long elements);
extern "C" __declspec(dllexport) void __stdcall CopyDeviceHalfToDeviceHalf(half* d_source, half* d_dest, long elements);
extern "C" __declspec(dllexport) void __stdcall CopyHostToDevice(float* h_source, float* d_dest, long elements);
extern "C" __declspec(dllexport) void __stdcall CopyHostToDeviceHalf(float* h_source, half* d_dest, long elements);

extern "C" __declspec(dllexport) void __stdcall SingleToHalf(float* d_source, half* d_dest, long elements);
extern "C" __declspec(dllexport) void __stdcall HalfToSingle(half* d_source, float* d_dest, long elements);

// Post.cu:

extern "C" __declspec(dllexport) void GetMotionFilter(float* d_output, 
														int3 dims, 
														float3* h_shifts, 
														uint nshifts, 
														uint batch);

// Shift.cpp:

extern "C" __declspec(dllexport) void CreateShift(float* d_frame,
													int2 dimsframe,
													int nframes,
													int3* h_origins,
													int norigins,
													int2 dimsregion,
													size_t* h_mask,
													uint masklength,
													half2* d_outputall);

extern "C" __declspec(dllexport) void ShiftGetAverage(half2* d_phase,
														half2* d_average,
														half2* d_shiftfactors,
														uint length,
														uint probelength,
														float2* d_shifts,
														uint nspectra,
														uint nframes);

extern "C" __declspec(dllexport) void ShiftGetDiff(half2* d_phase,
													half2* d_average,
													half2* d_shiftfactors,
													half* d_weights,
													uint length,
													uint probelength,
													float2* d_shifts,
													float* h_diff,
													uint npositions,
													uint nframes);

extern "C" __declspec(dllexport) void ShiftGetGrad(half2* d_phase,
													half2* d_average,
													half2* d_shiftfactors,
													half* d_weights,
													uint length,
													uint probelength,
													float2* d_shifts,
													float2* h_grad,
													uint npositions,
													uint nframes);

// Tools.cu:

extern "C" __declspec(dllexport) void Extract(float* d_input,
												float* d_output,
												int3 dims,
												int3 dimsregion,
												int3* h_origins,
												uint batch);

extern "C" __declspec(dllexport) void ExtractHalf(float* d_input,
													float* d_output,
													int3 dims,
													int3 dimsregion,
													int3* h_origins,
													uint batch);

extern "C" __declspec(dllexport) void ReduceMean(float* d_input, 
													float* d_output, 
													uint vectorlength, 
													uint nvectors, 
													uint batch);

extern "C" __declspec(dllexport) void ReduceMeanHalf(half* d_input, half* d_output, uint vectorlength, uint nvectors, uint batch);

extern "C" __declspec(dllexport) void CreateCTF(float* d_output,
												float2* d_coords,
												uint length,
												gtom::CTFParams* h_params,
												bool amplitudesquared,
												uint batch);

extern "C" __declspec(dllexport) void Resize(float* d_input,
											int3 dimsinput,
											float* d_output,
											int3 dimsoutput,
											uint batch);

extern "C" __declspec(dllexport) void ShiftStack(float* d_input,
												float* d_output,
												int3 dims,
												float3* h_shifts,
												uint batch);



extern "C" __declspec(dllexport) void Add(float* d_input, float* d_summands, float* d_output, size_t elements);

extern "C" __declspec(dllexport) void AddToSlices(float* d_input, float* d_summands, float* d_output, size_t sliceelements, uint slices);

extern "C" __declspec(dllexport) void Subtract(float* d_input, float* d_subtrahends, float* d_output, size_t elements);

extern "C" __declspec(dllexport) void SubtractFromSlices(float* d_input, float* d_subtrahends, float* d_output, size_t sliceelements, uint slices);

extern "C" __declspec(dllexport) void Multiply(float* d_input, float* d_multiplicators, float* d_output, size_t elements);

extern "C" __declspec(dllexport) void MultiplySlices(float* d_input, float* d_multiplicators, float* d_output, size_t sliceelements, uint slices);

extern "C" __declspec(dllexport) void Divide(float* d_input, float* d_divisors, float* d_output, size_t elements);

extern "C" __declspec(dllexport) void DivideSlices(float* d_input, float* d_divisors, float* d_output, size_t sliceelements, uint slices);

extern "C" __declspec(dllexport) void AddHalf(half* d_input, half* d_summands, half* d_output, size_t elements);

extern "C" __declspec(dllexport) void AddToSlicesHalf(half* d_input, half* d_summands, half* d_output, size_t sliceelements, uint slices);

extern "C" __declspec(dllexport) void SubtractHalf(half* d_input, half* d_subtrahends, half* d_output, size_t elements);

extern "C" __declspec(dllexport) void SubtractFromSlicesHalf(half* d_input, half* d_subtrahends, half* d_output, size_t sliceelements, uint slices);

extern "C" __declspec(dllexport) void MultiplyHalf(half* d_input, half* d_multiplicators, half* d_output, size_t elements);

extern "C" __declspec(dllexport) void MultiplySlicesHalf(half* d_input, half* d_multiplicators, half* d_output, size_t sliceelements, uint slices);

#endif