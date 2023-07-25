using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher.Classes
{
    static class EmbedResources
    {
        public static readonly Lazy<Bitmap?> WindowIcon = new Lazy<Bitmap?>(() =>
        {
            var currentAsm = Assembly.GetExecutingAssembly();
            using (var resStream = currentAsm.GetManifestResourceStream("Leayal.SnowBreakLauncher.snowbreak.ico"))
            {
                if (resStream != null)
                {
                    return Bitmap.DecodeToHeight(resStream, 32, BitmapInterpolationMode.HighQuality);
                }
            }
            return null;
        });
    }
}
