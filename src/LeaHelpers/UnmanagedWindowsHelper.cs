using Avalonia.Controls;
using System;
using System.Threading.Tasks;
using MSWin32 = global::Windows.Win32;
using PInvoke = global::Windows.Win32.PInvoke;

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

#if WINDOWS
        public static bool SetForegroundWindow(Window window)
        {
            var platformHandle = window.TryGetPlatformHandle();
            if (platformHandle == null) return false;
            return PInvoke.SetForegroundWindow(new MSWin32.Foundation.HWND(platformHandle.Handle));
        }
#else
        public static bool SetForegroundWindow(Window window) => false;
#endif
        /// <summary>Brings the thread that created the specified window into the foreground and activates the window.</summary>
        /// <param name="hWnd">
        /// <para>A handle to the window that should be activated and brought to the foreground.</para>
        /// <para><see href="https://docs.microsoft.com/windows/win32/api//winuser/nf-winuser-setforegroundwindow#parameters">Read more on docs.microsoft.com</see>.</para>
        /// </param>
        /// <returns><see langword="true"/> if the operation successes. Otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// <para><see href="https://docs.microsoft.com/windows/win32/api//winuser/nf-winuser-setforegroundwindow">Learn more about this API from docs.microsoft.com</see>.</para>
        /// </remarks>
        public static bool SetForegroundWindow(IntPtr hWnd) => PInvoke.SetForegroundWindow(new MSWin32.Foundation.HWND(hWnd));

        /// <summary>Retrieves a handle to the window that contains the specified point.</summary>
        /// <param name="screenCoordinate">
        /// <para>The point to be checked.</para>
        /// <para><see href="https://docs.microsoft.com/windows/win32/api/winuser/nf-winuser-windowfrompoint#parameters">Read more on docs.microsoft.com</see>.</para>
        /// </param>
        /// <returns>
        /// <para>The return value is a handle to the window that contains the point. If no window exists at the given point, the return value is <seealso cref="IntPtr.Zero"/>. If the point is over a static text control, the return value is a handle to the window under the static text control.</para>
        /// </returns>
        /// <remarks>The <b>WindowFromPoint</b> function does not retrieve a handle to a hidden or disabled window, even if the point is within the window. An application should use the <a href="https://docs.microsoft.com/windows/desktop/api/winuser/nf-winuser-childwindowfrompoint">ChildWindowFromPoint</a> function for a nonrestrictive search.</remarks>
        public static IntPtr WindowFromPoint(System.Drawing.Point screenCoordinate) => PInvoke.WindowFromPoint(screenCoordinate);

        /// <summary>Gets an awaitable object which will complete when <paramref name="window"/> is initialized or is closed.</summary>
        /// <param name="window">The window to await.</param>
        /// <param name="timeout">The time which the awaiter's patience can last for. Zero or sub-zero to disable timeout mechanism, wait indefinitely.</param>
        /// <returns>An awaitable object which will complete when <paramref name="window"/> is initialized or is closed</returns>
        /// <exception cref="ArgumentNullException"><paramref name="window"/> is <see langword="null"/>.</exception>
        public static ValueTask AwaitUntilInitialized(this Window window, int timeout = 0)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));

            if (window.IsInitialized) return ValueTask.CompletedTask;
            var taskSrc = new TaskCompletionSource();
            var wrapper = new WindowInitializationAwaiter(window, taskSrc, timeout);
            return wrapper.ValueTask;
        }

        readonly struct WindowInitializationAwaiter
        {
            public readonly TaskCompletionSource src;

            public readonly ValueTask ValueTask => new ValueTask(this.src.Task);

            public WindowInitializationAwaiter(Window window, TaskCompletionSource src, int timeout) : this()
            {
                this.src = src;
                window.Initialized += this.AwaitUntilInitialized_AwaitedInitialization;
                window.Closed += this.AwaitUntilInitialized_WindowClosed;
                if (timeout > 0)
                {
                    Task.Delay(timeout).ContinueWith(this.CancelWait);
                }
            }

            private readonly void AwaitUntilInitialized_WindowClosed(object? sender, EventArgs e)
            {
                if (sender is Window window)
                {
                    window.Closed -= this.AwaitUntilInitialized_WindowClosed;
                    this.CancelWait(null);
                }
            }

            private readonly void CancelWait(Task? t)
            {
                this.src.TrySetCanceled();
            }

            private readonly void AwaitUntilInitialized_AwaitedInitialization(object? sender, EventArgs e)
            {
                if (sender is Window window)
                {
                    window.Initialized -= this.AwaitUntilInitialized_AwaitedInitialization;
                    this.src.TrySetResult();
                }
            }
        }

        private static void AwaitUntilInitialized_AwaitedInitialization(object? sender, EventArgs e)
        {
            if (sender is Window window)
            {
                window.Initialized -= AwaitUntilInitialized_AwaitedInitialization;
            }
        }
    }
}
