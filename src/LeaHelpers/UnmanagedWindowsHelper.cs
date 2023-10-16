using Avalonia.Controls;
using Leayal.SnowBreakLauncher;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

namespace Leayal.Shared.Windows
{
    /// <summary>A class providing convenient methods to interact with other windows through unmanaged code.</summary>
    public static class UnmanagedWindowsHelper
    {
        /// <summary>Brings the thread that created the specified window into the foreground and activates the window.</summary>
        /// <param name="window">
        /// <para>The window that should be activated and brought to the foreground.</para>
        /// <para><see href="https://docs.microsoft.com/windows/win32/api//winuser/nf-winuser-setforegroundwindow#parameters">Read more on docs.microsoft.com</see>.</para>
        /// </param>
        /// <returns><see langword="true"/> if the operation successes. Otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// <para><see href="https://docs.microsoft.com/windows/win32/api//winuser/nf-winuser-setforegroundwindow">Learn more about this API from docs.microsoft.com</see>.</para>
        /// </remarks>
        /// 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SetForegroundWindow(Window window) => SetForegroundWindowImpl(window);

        private static readonly Func<Window, bool> SetForegroundWindowImpl = OperatingSystem.IsWindowsVersionAtLeast(5) ? SetForegroundWindowImpl_Win : new Func<Window, bool>(delegate { return false; });

        [SupportedOSPlatform("windows5.0")]
        private static bool SetForegroundWindowImpl_Win(Window window)
        {
            var platformHandle = window.TryGetPlatformHandle();
            if (platformHandle == null) return false;
            return global::Windows.Win32.PInvoke.SetForegroundWindow(new global::Windows.Win32.Foundation.HWND(platformHandle.Handle));
        }
    }
}
