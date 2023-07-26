using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher.Classes
{
    class AvaloniaRemoteBitmapSource : Bitmap
    {
        public AvaloniaRemoteBitmapSource(string fileName) : base(fileName)
        {
        }

        public AvaloniaRemoteBitmapSource(Stream stream) : base(stream)
        {
        }

        public AvaloniaRemoteBitmapSource(PixelFormat format, AlphaFormat alphaFormat, nint data, PixelSize size, Vector dpi, int stride) : base(format, alphaFormat, data, size, dpi, stride)
        {
        }


    }
}
