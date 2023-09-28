using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace Leayal.SnowBreakLauncher.Classes
{
    sealed class SafeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public static readonly SafeLibraryHandle Invalid = new SafeLibraryHandle(-1);

        public SafeLibraryHandle(nint libraryHandle) : base(true)
        {
            this.SetHandle(libraryHandle);
        }

        public nint GetExport(string name) => NativeLibrary.GetExport(this.handle, name);

        protected override bool ReleaseHandle()
        {
            NativeLibrary.Free(this.handle);
            return true;
        }
    }
}
