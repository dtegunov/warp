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

// Comparison.cu:

extern "C" __declspec(dllexport) void CompareParticles(float* d_particles,
                                                        float* d_masks,
                                                        float* d_projections,
                                                        int2 dims,
                                                        float2* d_ctfcoords,
                                                        gtom::CTFParams* h_ctfparams,
                                                        float highpass,
                                                        float lowpass,
                                                        float* d_scores,
                                                        uint nparticles);

// CTF.cpp:
extern "C" __declspec(dllexport) void CreateSpectra(float* d_frame,
													int2 dimsframe,
													int nframes,
													int3* h_origins,
													int norigins,
													int2 dimsregion,
													int3 ctfgrid,
													float* d_outputall,
													float* d_outputmean);

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

// ParticleCTF.cpp:
extern "C" __declspec(dllexport) void CreateParticleSpectra(float* d_frame,
                                                            int2 dimsframe,
                                                            int nframes,
                                                            int3* h_origins,
                                                            int norigins,
                                                            float* d_masks,
                                                            int2 dimsregion,
                                                            bool ctftime,
                                                            int framegroupsize,
                                                            float majorpixel,
                                                            float minorpixel,
                                                            float majorangle,
                                                            float2* d_outputall);

extern "C" __declspec(dllexport) void ParticleCTFMakeAverage(float2* d_ps,
                                                            float2* d_pscoords, 
                                                            uint length, 
                                                            uint sidelength, 
                                                            gtom::CTFParams* h_sourceparams, 
                                                            gtom::CTFParams targetparams, 
                                                            uint minbin, 
                                                            uint maxbin, 
                                                            uint batch, 
                                                            float* d_output);

extern "C" __declspec(dllexport) void ParticleCTFCompareToSim(float2* d_ps, 
                                                                float2* d_pscoords, 
                                                                float2* d_ref, 
                                                                float* d_invsigma,
                                                                uint length, gtom::CTFParams* h_sourceparams, 
                                                                float* h_scores, 
                                                                uint nframes,
                                                                uint batch);

// Cubic.cpp:

extern "C" __declspec(dllexport) void __stdcall CubicInterpOnGrid(int3 dimensions, 
																	float* values, 
																	float3 spacing, 
																	int3 valueGrid, 
																	float3 step, 
																	float3 offset, 
																	float* output);

extern "C" __declspec(dllexport) void __stdcall CubicInterpIrregular(int3 dimensions, 
                                                                    float* values, 
                                                                    float3* positions, 
                                                                    int npositions, 
                                                                    float3 spacing, 
                                                                    float* output);

// Device.cpp:

extern "C" __declspec(dllexport) int __stdcall GetDeviceCount();
extern "C" __declspec(dllexport) void __stdcall SetDevice(int device);
extern "C" __declspec(dllexport) int __stdcall GetDevice();
extern "C" __declspec(dllexport) long __stdcall GetFreeMemory(int device);
extern "C" __declspec(dllexport) long __stdcall GetTotalMemory(int device);

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

extern "C" __declspec(dllexport) void CorrectMagAnisotropy(float* d_image, 
                                                            int2 dimsimage, 
                                                            float* d_scaled, 
                                                            int2 dimsscaled, 
                                                            float majorpixel, 
                                                            float minorpixel, 
                                                            float majorangle, 
                                                            uint supersample, 
                                                            uint batch);

extern "C" __declspec(dllexport) void DoseWeighting(float* d_freq,
                                                    float* d_output,
                                                    uint length,
                                                    float* h_dose,
                                                    float3 nikoconst,
                                                    uint batch);

extern "C" __declspec(dllexport) void NormParticles(float* d_input, float* d_output, int3 dims, uint particleradius, bool flipsign, uint batch);

// Shift.cpp:

extern "C" __declspec(dllexport) void CreateShift(float* d_frame,
													int2 dimsframe,
													int nframes,
													int3* h_origins,
													int norigins,
													int2 dimsregion,
													size_t* h_mask,
													uint masklength,
                                                    float2* d_outputall);

extern "C" __declspec(dllexport) void ShiftGetAverage(float2* d_phase,
                                                        float2* d_average,
                                                        float2* d_shiftfactors,
														uint length,
														uint probelength,
														float2* d_shifts,
														uint nspectra,
														uint nframes);

extern "C" __declspec(dllexport) void ShiftGetDiff(float2* d_phase,
                                                    float2* d_average,
                                                    float2* d_shiftfactors,
													uint length,
													uint probelength,
													float2* d_shifts,
													float* h_diff,
													uint npositions,
													uint nframes);

extern "C" __declspec(dllexport) void ShiftGetGrad(float2* d_phase,
                                                    float2* d_average,
                                                    float2* d_shiftfactors,
													uint length,
													uint probelength,
													float2* d_shifts,
													float2* h_grad,
													uint npositions,
													uint nframes);

extern "C" __declspec(dllexport) void CreateMotionBlur(float* d_output, 
                                                       int3 dims, 
                                                       float* h_shifts, 
                                                       uint nshifts, 
                                                       uint batch);

// ParticleShift.cu:
extern "C" __declspec(dllexport) void CreateParticleShift(float* d_frame,
                                                            int2 dimsframe,
                                                            int nframes,
                                                            float2* h_positions,
                                                            float2* h_shifts,
                                                            int npositions,
                                                            int2 dimsregion,
                                                            size_t* h_indices,
                                                            uint indiceslength,
                                                            float* d_masks,
                                                            float2* d_projections,
                                                            gtom::CTFParams* h_ctfparams,
                                                            float2* d_ctfcoords,
                                                            float* d_invsigma,
                                                            float pixelmajor,
                                                            float pixelminor,
                                                            float pixelangle,
                                                            float2* d_outputparticles,
                                                            float2* d_outputprojections,
                                                            float* d_outputinvsigma);

extern "C" __declspec(dllexport) void ParticleShiftGetDiff(float2* d_phase,
                                                            float2* d_average,
                                                            float2* d_shiftfactors,
                                                            float* d_invsigma,
                                                            uint length,
                                                            uint probelength,
                                                            float2* d_shifts,
                                                            float* h_diff,
                                                            uint npositions,
                                                            uint nframes);

extern "C" __declspec(dllexport) void ParticleShiftGetGrad(float2* d_phase,
                                                            float2* d_average,
                                                            float2* d_shiftfactors,
                                                            float* d_invsigma,
                                                            uint length,
                                                            uint probelength,
                                                            float2* d_shifts,
                                                            float2* h_grad,
                                                            uint npositions,
                                                            uint nframes);

// Polishing.cu:
extern "C" __declspec(dllexport) void CreatePolishing(float* d_particles, float2* d_particlesft, float* d_masks, int2 dims, int2 dimscropped, int nparticles, int nframes);

extern "C" __declspec(dllexport) void PolishingGetDiff(float2* d_phase,
                                                        float2* d_average,
                                                        float2* d_shiftfactors,
                                                        float2* d_ctfcoords,
                                                        gtom::CTFParams* h_ctfparams,
                                                        float* d_invsigma,
                                                        int2 dims,                                                        
                                                        float2* d_shifts,
                                                        float* h_diff,
                                                        float* h_diffall,
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

extern "C" __declspec(dllexport) void Normalize(float* d_ps,
												float* d_output,
												uint length,
												uint batch);

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

extern "C" __declspec(dllexport) void FFT(float* d_input, float2* d_output, int3 dims, uint batch);

extern "C" __declspec(dllexport) void IFFT(float2* d_input, float* d_output, int3 dims, uint batch);

extern "C" __declspec(dllexport) void Pad(float* d_input, float* d_output, int3 olddims, int3 newdims, uint batch);

extern "C" __declspec(dllexport) void PadFT(float2* d_input, float2* d_output, int3 olddims, int3 newdims, uint batch);

extern "C" __declspec(dllexport) void CropFT(float2* d_input, float2* d_output, int3 olddims, int3 newdims, uint batch);

extern "C" __declspec(dllexport) void RemapToFTComplex(float2* d_input, float2* d_output, int3 dims, uint batch);

extern "C" __declspec(dllexport) void RemapToFTFloat(float* d_input, float* d_output, int3 dims, uint batch);

extern "C" __declspec(dllexport) void RemapFromFTComplex(float2* d_input, float2* d_output, int3 dims, uint batch);

extern "C" __declspec(dllexport) void RemapFromFTFloat(float* d_input, float* d_output, int3 dims, uint batch);

extern "C" __declspec(dllexport) void RemapFullToFTFloat(float* d_input, float* d_output, int3 dims, uint batch);

extern "C" __declspec(dllexport) void RemapFullFromFTFloat(float* d_input, float* d_output, int3 dims, uint batch);

extern "C" __declspec(dllexport) void Cart2Polar(float* d_input, float* d_output, int2 dims, uint innerradius, uint exclusiveouterradius, uint batch);

extern "C" __declspec(dllexport) void Cart2PolarFFT(float* d_input, float* d_output, int2 dims, uint innerradius, uint exclusiveouterradius, uint batch);

extern "C" __declspec(dllexport) void Xray(float* d_input, float* d_output, float ndevs, int2 dims, uint batch);

extern "C" __declspec(dllexport) void Abs(float* d_input, float* d_output, size_t length);

extern "C" __declspec(dllexport) void Amplitudes(float2* d_input, float* d_output, size_t length);

extern "C" __declspec(dllexport) void Sign(float* d_input, float* d_output, size_t length);

extern "C" __declspec(dllexport) void AddToSlices(float* d_input, float* d_summands, float* d_output, size_t sliceelements, uint slices);

extern "C" __declspec(dllexport) void SubtractFromSlices(float* d_input, float* d_subtrahends, float* d_output, size_t sliceelements, uint slices);

extern "C" __declspec(dllexport) void MultiplySlices(float* d_input, float* d_multiplicators, float* d_output, size_t sliceelements, uint slices);

extern "C" __declspec(dllexport) void DivideSlices(float* d_input, float* d_divisors, float* d_output, size_t sliceelements, uint slices);

extern "C" __declspec(dllexport) void AddToSlicesHalf(half* d_input, half* d_summands, half* d_output, size_t sliceelements, uint slices);

extern "C" __declspec(dllexport) void SubtractFromSlicesHalf(half* d_input, half* d_subtrahends, half* d_output, size_t sliceelements, uint slices);

extern "C" __declspec(dllexport) void MultiplySlicesHalf(half* d_input, half* d_multiplicators, half* d_output, size_t sliceelements, uint slices);

extern "C" __declspec(dllexport) void MultiplyComplexSlicesByScalar(float2* d_input, float* d_multiplicators, float2* d_output, size_t sliceelements, uint slices);

extern "C" __declspec(dllexport) void DivideComplexSlicesByScalar(float2* d_input, float* d_divisors, float2* d_output, size_t sliceelements, uint slices);

extern "C" __declspec(dllexport) void Scale(float* d_input, float* d_output, int3 dimsinput, int3 dimsoutput, uint batch);

extern "C" __declspec(dllexport) void ProjectForward(float2* d_inputft, float2* d_outputft, int3 dimsinput, int2 dimsoutput, float3* h_angles, float supersample, uint batch);

#endif