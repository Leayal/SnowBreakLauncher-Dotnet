using Avalonia.Input;
using Avalonia.Media.Imaging;
using System;
using System.Reflection;

namespace Leayal.SnowBreakLauncher.Classes
{
    static class EmbedResources
    {
        public static readonly Lazy<Bitmap?> WindowIcon;
        public static readonly Lazy<Cursor> Cursor_Hand;

        static EmbedResources()
        {
            WindowIcon = new Lazy<Bitmap?>(() =>
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
            Cursor_Hand = new Lazy<Cursor>(() => Cursor.Parse("Hand"));

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        private static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
            if (Cursor_Hand.IsValueCreated)
            {
                Cursor_Hand.Value.Dispose();
            }
            if (WindowIcon.IsValueCreated)
            {
                WindowIcon.Value?.Dispose();
            }
        }
    }
}
