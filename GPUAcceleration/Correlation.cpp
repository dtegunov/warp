#include "Functions.h"
using namespace gtom;

__declspec(dllexport) void __stdcall CorrelateSubTomos(float2* d_projectordata, 
                                                        float projectoroversample, 
                                                        int3 dimsprojector,
                                                        float2* d_experimentalft,
                                                        float* d_ctf,
                                                        int3 dimsvolume,
                                                        uint nvolumes,
                                                        float3* h_angles,
                                                        uint nangles,
                                                        float maskradius,
                                                        float* d_bestcorrelation,
                                                        float* d_bestrot,
                                                        float* d_besttilt,
                                                        float* d_bestpsi)
{
    d_PickSubTomograms(d_projectordata,
                        projectoroversample,
                        dimsprojector,
                        d_experimentalft,
                        d_ctf,
                        dimsvolume,
                        nvolumes,
                        (tfloat3*)h_angles,
                        nangles,
                        maskradius,
                        d_bestcorrelation,
                        d_bestrot,
                        d_besttilt,
                        d_bestpsi);
}