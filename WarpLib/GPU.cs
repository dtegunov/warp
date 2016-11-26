using System;
using System.Runtime.InteropServices;
using Warp.Tools;

namespace Warp
{
    public static class GPU
    {
        public static readonly object Sync = new object();

        public static event Action MemoryChanged;

        public static void OnMemoryChanged()
        {
            MemoryChanged?.Invoke();
        }

        // Memory.cpp:

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "GetDeviceCount")]
        public static extern int GetDeviceCount();

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "SetDevice")]
        public static extern void SetDevice(int id);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "GetDevice")]
        public static extern int GetDevice();

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "GetFreeMemory")]
        public static extern long GetFreeMemory(int device);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "GetTotalMemory")]
        public static extern long GetTotalMemory(int device);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "MallocDevice")]
        public static extern IntPtr MallocDevice(long elements);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "MallocDeviceFromHost")]
        public static extern IntPtr MallocDeviceFromHost(float[] h_data, long elements);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "MallocDeviceHalf")]
        public static extern IntPtr MallocDeviceHalf(long elements);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "MallocDeviceHalfFromHost")]
        public static extern IntPtr MallocDeviceHalfFromHost(float[] h_data, long elements);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "FreeDevice")]
        public static extern void FreeDevice(IntPtr d_data);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CopyDeviceToHost")]
        public static extern void CopyDeviceToHost(IntPtr d_source, float[] h_dest, long elements);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CopyDeviceHalfToHost")]
        public static extern void CopyDeviceHalfToHost(IntPtr d_source, float[] h_dest, long elements);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CopyDeviceToDevice")]
        public static extern void CopyDeviceToDevice(IntPtr d_source, IntPtr d_dest, long elements);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CopyDeviceHalfToDeviceHalf")]
        public static extern void CopyDeviceHalfToDeviceHalf(IntPtr d_source, IntPtr d_dest, long elements);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CopyHostToDevice")]
        public static extern void CopyHostToDevice(float[] h_source, IntPtr d_dest, long elements);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CopyHostToDeviceHalf")]
        public static extern void CopyHostToDeviceHalf(float[] h_source, IntPtr d_dest, long elements);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "SingleToHalf")]
        public static extern void SingleToHalf(IntPtr d_source, IntPtr d_dest, long elements);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "HalfToSingle")]
        public static extern void HalfToSingle(IntPtr d_source, IntPtr d_dest, long elements);

        // Comparison.cu:
        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CompareParticles")]
        public static extern void CompareParticles(IntPtr d_particles,
                                                   IntPtr d_masks,
                                                   IntPtr d_projections,
                                                   int2 dims,
                                                   IntPtr d_ctfcoords,
                                                   CTFStruct[] h_ctfparams,
                                                   float highpass,
                                                   float lowpass,
                                                   IntPtr d_scores,
                                                   uint nparticles);

        // Correlation.cpp:
        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CorrelateSubTomos")]
        public static extern void CorrelateSubTomos(IntPtr d_projectordata,
                                                    float projectoroversample,
                                                    int3 dimsprojector,
                                                    IntPtr d_experimentalft,
                                                    IntPtr d_ctf,
                                                    int3 dimsvolume,
                                                    uint nvolumes,
                                                    float[] h_angles,
                                                    uint nangles,
                                                    float maskradius,
                                                    IntPtr d_bestcorrelation,
                                                    IntPtr d_bestrot,
                                                    IntPtr d_besttilt,
                                                    IntPtr d_bestpsi);

        // CTF.cu:

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateSpectra")]
        public static extern void CreateSpectra(IntPtr d_frame, 
                                                int2 dimsframe, 
                                                int nframes, 
                                                int3[] h_origins, 
                                                int norigins, 
                                                int2 dimsregion, 
                                                int3 ctfgrid,
                                                IntPtr d_outputall, 
                                                IntPtr d_outputmean);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CTFFitMean")]
        public static extern CTFStruct CTFFitMean(IntPtr d_ps, 
                                                  IntPtr d_pscoords, 
                                                  int2 dims, 
                                                  CTFStruct startparams, 
                                                  CTFFitStruct fp, 
                                                  bool doastigmatism);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CTFMakeAverage")]
        public static extern void CTFMakeAverage(IntPtr d_ps, 
                                                 IntPtr d_pscoords, 
                                                 uint length, 
                                                 uint sidelength, 
                                                 CTFStruct[] h_sourceparams, 
                                                 CTFStruct targetparams, 
                                                 uint minbin, 
                                                 uint maxbin, 
                                                 int[] h_consider,
                                                 uint batch, 
                                                 IntPtr d_output);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CTFCompareToSim")]
        public static extern void CTFCompareToSim(IntPtr d_ps, 
                                                  IntPtr d_pscoords, 
                                                  IntPtr d_scale, 
                                                  uint length, 
                                                  CTFStruct[] h_sourceparams, 
                                                  float[] h_scores,
                                                  uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CTFSubtractBackground")]
        public static extern void CTFSubtractBackground(IntPtr d_ps, 
                                                        IntPtr d_background, 
                                                        uint length, 
                                                        IntPtr d_output, 
                                                        uint batch);

        // ParticleCTF.cu:
        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateParticleSpectra")]
        public static extern void CreateParticleSpectra(IntPtr d_frame,
                                                        int2 dimsframe,
                                                        int nframes,
                                                        int3[] h_origins,
                                                        int norigins,
                                                        IntPtr d_masks,
                                                        int2 dimsregion,
                                                        bool ctftime,
                                                        int framegroupsize,
                                                        float majorpixel,
                                                        float minorpixel,
                                                        float majorangle,
                                                        IntPtr d_outputall);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "ParticleCTFMakeAverage")]
        public static extern void ParticleCTFMakeAverage(IntPtr d_ps,
                                                         IntPtr d_pscoords,
                                                         uint length,
                                                         uint sidelength,
                                                         CTFStruct[] h_sourceparams,
                                                         CTFStruct targetparams,
                                                         uint minbin,
                                                         uint maxbin,
                                                         uint batch,
                                                         IntPtr d_output);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "ParticleCTFCompareToSim")]
        public static extern void ParticleCTFCompareToSim(IntPtr d_ps,
                                                          IntPtr d_pscoords,
                                                          IntPtr d_ref,
                                                          IntPtr d_invsigma,
                                                          uint length,
                                                          CTFStruct[] h_sourceparams,
                                                          float[] h_scores,
                                                          uint nframes,
                                                          uint batch);


        // Post.cu:

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "DoseWeighting")]
        public static extern void DoseWeighting(IntPtr d_freq, IntPtr d_output, uint length, float[] h_dose, float3 nikoconst, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CorrectMagAnisotropy")]
        public static extern void CorrectMagAnisotropy(IntPtr d_image,
                                                       int2 dimsimage,
                                                       IntPtr d_scaled,
                                                       int2 dimsscaled,
                                                       float majorpixel,
                                                       float minorpixel,
                                                       float majorangle,
                                                       uint supersample,
                                                       uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "NormParticles")]
        public static extern void NormParticles(IntPtr d_input, IntPtr d_output, int3 dims, uint particleradius, bool flipsign, uint batch);

        // Shift.cu:

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateShift")]
        public static extern void CreateShift(IntPtr d_frame,
                                              int2 dimsframe,
                                              int nframes,
                                              int3[] h_origins,
                                              int norigins,
                                              int2 dimsregion,
                                              long[] h_mask,
                                              uint masklength,
                                              IntPtr d_outputall);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "ShiftGetAverage")]
        public static extern void ShiftGetAverage(IntPtr d_phase,
                                                  IntPtr d_average,
                                                  IntPtr d_shiftfactors,
                                                  uint length,
                                                  uint probelength,
                                                  IntPtr d_shifts,
                                                  uint nspectra,
                                                  uint nframes);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "ShiftGetDiff")]
        public static extern void ShiftGetDiff(IntPtr d_phase,
                                               IntPtr d_average,
                                               IntPtr d_shiftfactors,
                                               uint length,
                                               uint probelength,
                                               IntPtr d_shifts,
                                               float[] h_diff,
                                               uint npositions,
                                               uint nframes);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "ShiftGetGrad")]
        public static extern void ShiftGetGrad(IntPtr d_phase,
                                               IntPtr d_average,
                                               IntPtr d_shiftfactors,
                                               uint length,
                                               uint probelength,
                                               IntPtr d_shifts,
                                               float[] h_grad,
                                               uint npositions,
                                               uint nframes);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateMotionBlur")]
        public static extern void CreateMotionBlur(IntPtr d_output, int3 dims, float[] h_shifts, uint nshifts, uint batch);

        // ParticleShift.cu:
        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateParticleShift")]
        public static extern void CreateParticleShift(IntPtr d_frame,
                                                      int2 dimsframe,
                                                      int nframes,
                                                      float[] h_positions,
                                                      float[] h_shifts,
                                                      int npositions,
                                                      int2 dimsregion,
                                                      long[] h_indices,
                                                      uint indiceslength,
                                                      IntPtr d_masks,
                                                      IntPtr d_projections,
                                                      CTFStruct[] h_ctfparams,
                                                      IntPtr d_ctfcoords,
                                                      IntPtr d_invsigma,
                                                      float pixelmajor,
                                                      float pixelminor,
                                                      float pixelangle,
                                                      IntPtr d_outputparticles,
                                                      IntPtr d_outputprojections,
                                                      IntPtr d_outputinvsigma);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "ParticleShiftGetDiff")]
        public static extern void ParticleShiftGetDiff(IntPtr d_phase,
                                                       IntPtr d_projections,
                                                       IntPtr d_shiftfactors,
                                                       IntPtr d_invsigma,
                                                       uint length,
                                                       uint probelength,
                                                       IntPtr d_shifts,
                                                       float[] h_diff,
                                                       uint npositions,
                                                       uint nframes);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "ParticleShiftGetGrad")]
        public static extern void ParticleShiftGetGrad(IntPtr d_phase,
                                                       IntPtr d_average,
                                                       IntPtr d_shiftfactors,
                                                       IntPtr d_invsigma,
                                                       uint length,
                                                       uint probelength,
                                                       IntPtr d_shifts,
                                                       float[] h_grad,
                                                       uint npositions,
                                                       uint nframes);

        // Polishing.cu:
        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CreatePolishing")]
        public static extern void CreatePolishing(IntPtr d_particles, IntPtr d_particlesft, IntPtr d_masks, int2 dims, int2 dimscropped, int nparticles, int nframes);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "PolishingGetDiff")]
        public static extern void PolishingGetDiff(IntPtr d_phase,
                                                   IntPtr d_average,
                                                   IntPtr d_shiftfactors,
                                                   IntPtr d_ctfcoords,
                                                   CTFStruct[] h_ctfparams,
                                                   IntPtr d_invsigma,
                                                   int2 dims,
                                                   IntPtr d_shifts,
                                                   float[] h_diff,
                                                   float[] h_diffall,
                                                   uint npositions,
                                                   uint nframes);

        // TomoRefine.cu:
        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "TomoRefineGetDiff")]
        public static extern void TomoRefineGetDiff(IntPtr d_experimental,
                                                    IntPtr d_reference,
                                                    IntPtr d_shiftfactors,
                                                    IntPtr d_ctf,
                                                    IntPtr d_weights,
                                                    int2 dims,
                                                    float[] h_shifts,
                                                    float[] h_diff,
                                                    uint nparticles);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "TomoRealspaceCorrelate")]
        public static extern void TomoRealspaceCorrelate(IntPtr d_projectionsft,
                                                         int2 dims,
                                                         uint nprojections,
                                                         uint ntilts,
                                                         IntPtr d_experimental,
                                                         IntPtr d_ctf,
                                                         IntPtr d_mask,
                                                         IntPtr d_weights,
                                                         float[] h_shifts,
                                                         float[] h_result);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "TomoGlobalAlign")]
        public static extern void TomoGlobalAlign(IntPtr d_experimental,
                                                  IntPtr d_shiftfactors,
                                                  IntPtr d_ctf,
                                                  IntPtr d_weights,
                                                  int2 dims,
                                                  IntPtr d_ref,
                                                  int3 dimsref,
                                                  int refsupersample,
                                                  float[] h_angles,
                                                  uint nangles,
                                                  float[] h_shifts,
                                                  uint nshifts,
                                                  uint nparticles,
                                                  uint ntilts,
                                                  int[] h_bestangles,
                                                  int[] h_bestshifts,
                                                  float[] h_bestscores);

        // Tools.cu:

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "FFT")]
        public static extern void FFT(IntPtr d_input, IntPtr d_output, int3 dims, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "IFFT")]
        public static extern void IFFT(IntPtr d_input, IntPtr d_output, int3 dims, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "Pad")]
        public static extern void Pad(IntPtr d_input, IntPtr d_output, int3 olddims, int3 newdims, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "PadFT")]
        public static extern void PadFT(IntPtr d_input, IntPtr d_output, int3 olddims, int3 newdims, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CropFT")]
        public static extern void CropFT(IntPtr d_input, IntPtr d_output, int3 olddims, int3 newdims, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "RemapToFTComplex")]
        public static extern void RemapToFTComplex(IntPtr d_input, IntPtr d_output, int3 dims, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "RemapToFTFloat")]
        public static extern void RemapToFTFloat(IntPtr d_input, IntPtr d_output, int3 dims, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "RemapFromFTComplex")]
        public static extern void RemapFromFTComplex(IntPtr d_input, IntPtr d_output, int3 dims, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "RemapFromFTFloat")]
        public static extern void RemapFromFTFloat(IntPtr d_input, IntPtr d_output, int3 dims, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "RemapFullToFTFloat")]
        public static extern void RemapFullToFTFloat(IntPtr d_input, IntPtr d_output, int3 dims, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "RemapFullFromFTFloat")]
        public static extern void RemapFullFromFTFloat(IntPtr d_input, IntPtr d_output, int3 dims, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "Extract")]
        public static extern void Extract(IntPtr d_input,
                                          IntPtr d_output,
                                          int3 dims,
                                          int3 dimsregion,
                                          int[] h_origins,
                                          uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "ExtractHalf")]
        public static extern void ExtractHalf(IntPtr d_input,
                                              IntPtr d_output,
                                              int3 dims,
                                              int3 dimsregion,
                                              int[] h_origins,
                                              uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "ReduceMean")]
        public static extern void ReduceMean(IntPtr d_input, IntPtr d_output, uint vectorlength, uint nvectors, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "ReduceMeanHalf")]
        public static extern void ReduceMeanHalf(IntPtr d_input, IntPtr d_output, uint vectorlength, uint nvectors, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "Normalize")]
        public static extern void Normalize(IntPtr d_ps,
                                               IntPtr d_output,
                                               uint length,
                                               uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "NormalizeMasked")]
        public static extern void NormalizeMasked(IntPtr d_ps,
                                                  IntPtr d_output,
                                                  IntPtr d_mask,
                                                  uint length,
                                                  uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "SphereMask")]
        public static extern void SphereMask(IntPtr d_input, IntPtr d_output, int3 dims, float radius, float sigma, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateCTF")]
        public static extern void CreateCTF(IntPtr d_output,
                                            IntPtr d_coords,
                                            uint length,
                                            CTFStruct[] h_params,
                                            bool amplitudesquared,
                                            uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "Resize")]
        public static extern void Resize(IntPtr d_input,
                                         int3 dimsinput,
                                         IntPtr d_output,
                                         int3 dimsoutput,
                                         uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "ShiftStack")]
        public static extern void ShiftStack(IntPtr d_input,
                                             IntPtr d_output,
                                             int3 dims,
                                             float[] h_shifts,
                                             uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "ShiftStackMassive")]
        public static extern void ShiftStackMassive(IntPtr d_input,
                                                    IntPtr d_output,
                                                    int3 dims,
                                                    float[] h_shifts,
                                                    uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "Cart2Polar")]
        public static extern void Cart2Polar(IntPtr d_input, IntPtr d_output, int2 dims, uint innerradius, uint exclusiveouterradius, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "Cart2PolarFFT")]
        public static extern void Cart2PolarFFT(IntPtr d_input, IntPtr d_output, int2 dims, uint innerradius, uint exclusiveouterradius, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "Xray")]
        public static extern void Xray(IntPtr d_input, IntPtr d_output, float ndevs, int2 dims, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "Sum")]
        public static extern void Sum(IntPtr d_input, IntPtr d_output, uint length, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "Abs")]
        public static extern void Abs(IntPtr d_input, IntPtr d_output, long length);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "Amplitudes")]
        public static extern void Amplitudes(IntPtr d_input, IntPtr d_output, long length);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "Sign")]
        public static extern void Sign(IntPtr d_input, IntPtr d_output, long length);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "AddToSlices")]
        public static extern void AddToSlices(IntPtr d_input, IntPtr d_summands, IntPtr d_output, long sliceelements, uint slices);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "SubtractFromSlices")]
        public static extern void SubtractFromSlices(IntPtr d_input, IntPtr d_subtrahends, IntPtr d_output, long sliceelements, uint slices);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "MultiplySlices")]
        public static extern void MultiplySlices(IntPtr d_input, IntPtr d_multiplicators, IntPtr d_output, long sliceelements, uint slices);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "DivideSlices")]
        public static extern void DivideSlices(IntPtr d_input, IntPtr d_divisors, IntPtr d_output, long sliceelements, uint slices);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "AddToSlicesHalf")]
        public static extern void AddToSlicesHalf(IntPtr d_input, IntPtr d_summands, IntPtr d_output, long sliceelements, uint slices);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "SubtractFromSlicesHalf")]
        public static extern void SubtractFromSlicesHalf(IntPtr d_input, IntPtr d_subtrahends, IntPtr d_output, long sliceelements, uint slices);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "MultiplySlicesHalf")]
        public static extern void MultiplySlicesHalf(IntPtr d_input, IntPtr d_multiplicators, IntPtr d_output, long sliceelements, uint slices);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "MultiplyComplexSlicesByScalar")]
        public static extern void MultiplyComplexSlicesByScalar(IntPtr d_input, IntPtr d_multiplicators, IntPtr d_output, long sliceelements, uint slices);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "DivideComplexSlicesByScalar")]
        public static extern void DivideComplexSlicesByScalar(IntPtr d_input, IntPtr d_divisors, IntPtr d_output, long sliceelements, uint slices);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "Scale")]
        public static extern void Scale(IntPtr d_input, IntPtr d_output, int3 dimsinput, int3 dimsoutput, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "ProjectForward")]
        public static extern void ProjectForward(IntPtr d_inputft, IntPtr d_outputft, int3 dimsinput, int2 dimsoutput, float[] h_angles, float supersample, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "ProjectBackward")]
        public static extern void ProjectBackward(IntPtr d_volumeft, IntPtr d_volumeweights, int3 dimsvolume, IntPtr d_projft, IntPtr d_projweights, int2 dimsproj, int rmax, float[] h_angles, float supersample, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "BackprojectorReconstructGPU")]
        public static extern void BackprojectorReconstructGPU(int3 dimsori, int3 dimspadded, int oversampling, IntPtr d_dataft, IntPtr d_weights, bool do_reconstruct_ctf, IntPtr d_result, int pre_planforw = -1, int pre_planback = -1, int pre_planforwctf = -1);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "Bandpass")]
        public static extern void Bandpass(IntPtr d_input, IntPtr d_output, int3 dims, float nyquistlow, float nyquisthigh, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "Rotate2D")]
        public static extern void Rotate2D(IntPtr d_input, IntPtr d_output, int2 dims, float[] h_angles, int oversample, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "ShiftAndRotate2D")]
        public static extern void ShiftAndRotate2D(IntPtr d_input, IntPtr d_output, int2 dims, float[] h_shifts, float[] h_angles, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateFFTPlan")]
        public static extern int CreateFFTPlan(int3 dims, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "CreateIFFTPlan")]
        public static extern int CreateIFFTPlan(int3 dims, uint batch);

        [DllImport("GPUAcceleration.dll", CharSet = CharSet.Ansi, SetLastError = true, CallingConvention = CallingConvention.StdCall, EntryPoint = "DestroyFFTPlan")]
        public static extern void DestroyFFTPlan(int plan);
    }

    public class DeviceToken
    {
        public int ID;

        public DeviceToken(int id)
        {
            ID = id;
        }
    }
}
