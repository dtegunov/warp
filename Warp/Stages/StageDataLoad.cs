using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Warp.Headers;
using Warp.Tools;

namespace Warp.Stages
{
    public static class StageDataLoad
    {
        static readonly object Sync = new object();

        public static MapHeader LoadHeader(string path, int2 headerlessSliceDims, long headerlessOffset, Type headerlessType)
        {
            lock (Sync)
            {
                return MapHeader.ReadFromFile(path, headerlessSliceDims, headerlessOffset, headerlessType);
            }
        }

        public static Image LoadMap(string path, int2 headerlessSliceDims, long headerlessOffset, Type headerlessType)
        {
            MapHeader Header = LoadHeader(path, headerlessSliceDims, headerlessOffset, headerlessType);
            float[][] Data;

            lock (Sync)
            {
                Data = IOHelper.ReadMapFloat(path, headerlessSliceDims, headerlessOffset, headerlessType);
            }

            return new Image(Data, Header.Dimensions, false, false);
        }
    }
}
