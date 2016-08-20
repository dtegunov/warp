using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Warp.Tools;

namespace Warp
{
    public class Projector : IDisposable
    {
        public int3 Dims, DimsOversampled;
        public int Oversampling;

        public Image Data;
        public Image Weights;

        public Projector(int3 dims, int oversampling)
        {
            Dims = dims;
            Oversampling = oversampling;

            int Oversampled = 2 * (oversampling * (Dims.X / 2) + 1) + 1;
            DimsOversampled = new int3(Oversampled, Oversampled, Oversampled);

            Data = new Image(IntPtr.Zero, DimsOversampled, true, true);
            Weights = new Image(IntPtr.Zero, DimsOversampled, true, false);
        }

        public Projector(Image data, int oversampling)
        {
            Dims = data.Dims;
            Oversampling = oversampling;

            int Oversampled = 2 * (oversampling * (Dims.X / 2) + 1) + 1;
            DimsOversampled = new int3(Oversampled, Oversampled, Oversampled);

            float[] Continuous = data.GetHostContinuousCopy();
            float[] Initialized = new float[(DimsOversampled.X / 2 + 1) * DimsOversampled.Y * DimsOversampled.Z * 2];

            CPU.InitProjector(Dims, Oversampling, Continuous, Initialized);

            float2[] Initialized2 = Helper.FromInterleaved2(Initialized);
            Data = new Image(Initialized2, DimsOversampled, true);
            //Data = Data.AsAmplitudes();
            //Data.WriteMRC("d_proj.mrc");
            Weights = new Image(DimsOversampled, true, false);
        }

        public Image Project(int2 dims, float3[] angles, int rmax)
        {
            return Data.AsProjections(angles, dims, Oversampling);
        }

        public void BackProject(Image projft, Image projweights, float3[] angles)
        {
            if (!projft.IsFT || !projft.IsComplex || !projweights.IsFT)
                throw new Exception("Input data must be complex (except weights) and in FFTW layout.");

            float[] Angles = Helper.ToInterleaved(angles);
            GPU.ProjectBackward(Data.GetDevice(Intent.ReadWrite),
                                Weights.GetDevice(Intent.ReadWrite),
                                DimsOversampled,
                                projft.GetDevice(Intent.Read),
                                projweights.GetDevice(Intent.Read),
                                projft.DimsSlice,
                                projft.Dims.X / 2,
                                Angles,
                                Oversampling,
                                (uint)projft.Dims.Z);
        }

        public Image Reconstruct(bool isctf)
        {
            float[] ContinuousData = Data.GetHostContinuousCopy();
            float[] ContinuousWeights = Weights.GetHostContinuousCopy();

            float[] ContinuousResult = new float[Dims.Elements()];

            Data.FreeDevice();
            Weights.FreeDevice();

            CPU.BackprojectorReconstruct(Dims, Oversampling, ContinuousData, ContinuousWeights, "C1", isctf, ContinuousResult);

            return new Image(ContinuousResult, Dims, isctf);
        }

        public void Dispose()
        {
            Data.Dispose();
            Weights.Dispose();
        }
    }
}
