#include "Functions.h"

__declspec(dllexport) int __stdcall GetDeviceCount()
{
	int result = 0;
	cudaGetDeviceCount(&result);

	return result;
}

__declspec(dllexport) void __stdcall SetDevice(int device)
{
	cudaSetDevice(device);
}

__declspec(dllexport) long __stdcall GetFreeMemory()
{
	size_t freemem = 0, totalmem = 0;
	cudaMemGetInfo(&freemem, &totalmem);

	return (long)(freemem >> 20);
}

__declspec(dllexport) long __stdcall GetTotalMemory()
{
	size_t freemem = 0, totalmem = 0;
	cudaMemGetInfo(&freemem, &totalmem);

	return (long)(totalmem >> 20);
}