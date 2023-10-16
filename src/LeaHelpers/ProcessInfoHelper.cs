using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Leayal.Shared.Windows
{
    public static partial class ProcessInfoHelper
    {
        /// <summary>Define callback interface.</summary>
        /// <param name="process">The process which has been exited.</param>
        /// <param name="processId">The unique identifier of the process. In case the callback is invoked immediately because the process has already been exited, this will be zero.</param>
        public delegate void ProcessExitCallback(Process process, in uint processId);

        /// <summary>Registers a callback which will be invoked when the process exits.</summary>
        /// <param name="process"></param>
        /// <param name="callback"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="immediatelyInvokeCallbackIfAlreadyExited"></param>
        /// <remarks>
        /// <para>The difference between this method and <seealso cref="Process.Exited"/> event is that this method will open a new handle with <see href="https://learn.microsoft.com/en-us/windows/win32/procthread/process-security-and-access-rights">SYNCHRONIZE access right</see> and wait for the handle's signal, this should avoid privilege-related issues in case the current process is unelevated while <paramref name="process"/> process is elevated.</para>
        /// <para><paramref name="callback"/> will not be invoked if <paramref name="cancellationToken"/> signal cancel waiting.</para>
        /// </remarks>
        /// <returns><see langword="true"/> if the method successfully registered the <paramref name="callback"/> to process exit signal. Otherwise, <see langword="false"/>. If the <paramref name="process"/> becomes invalid, this method will also return <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="process"/> or <paramref name="callback"/> is <see langword="null"/>.</exception>
        public static bool RegisterProcessExitCallback(this Process process, ProcessExitCallback callback, CancellationToken cancellationToken = default, bool immediatelyInvokeCallbackIfAlreadyExited = false)
        {
            if (OperatingSystem.IsWindows())
                return Win.RegisterProcessExitCallback(process, callback, cancellationToken, immediatelyInvokeCallbackIfAlreadyExited);
            else
                return Linux.RegisterProcessExitCallback(process, callback, cancellationToken, immediatelyInvokeCallbackIfAlreadyExited);
        }

        /// <summary>Retrieves the full name of the executable image for the specified process.</summary>
        /// <param name="process">The process to get the file path.</param>
        /// <returns>A string contains full path to the executable file which started the process. Or <see langword="null"/> on failures.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string? QueryFullProcessImageName(this Process process)
        {
            if (OperatingSystem.IsWindows())
                return Win.QueryFullProcessImageName(process, Win.QueryProcessNameType.Win32, 4096, true);
            else
                return Linux.GetProcessInfo(process, "exe");
        }
    }
}
