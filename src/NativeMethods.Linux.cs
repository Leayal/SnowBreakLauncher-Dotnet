using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Leayal.SnowBreakLauncher
{
    internal static unsafe partial class NativeMethods
    {
        [LibraryImport("libc", SetLastError = false), UnsupportedOSPlatform("windows")]
        public static partial uint geteuid();
    }
}
