using System;
using System.Diagnostics;
using System.Buffers;
using Microsoft.Win32.SafeHandles;
using MSWin32 = global::Windows.Win32;
using PInvoke = global::Windows.Win32.PInvoke;
using System.Runtime.CompilerServices;
using System.Threading;

#nullable enable
namespace Leayal.Shared.Windows
{
    /// <summary>A class provides quick and convenience method that .NET6 APIs doesn't really provide (yet?).</summary>
    public static class ProcessInfoHelper
    {
        internal static readonly SafeProcessHandle InvalidHandle = new SafeProcessHandle();

        internal static SafeProcessHandle OpenProcessForQueryLimitedInfo(uint processId)
        {
            var handle = PInvoke.OpenProcess(MSWin32.System.Threading.PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (handle.IsNull)
            {
                return InvalidHandle;
            }
            return new SafeProcessHandle(handle.Value, true);
        }

        /// <summary>Registers a callback which will be invoked when the process exits.</summary>
        /// <param name="process"></param>
        /// <param name="callback"></param>
        /// <param name="cancellationToken"></param>
        /// <remarks>
        /// <para>The difference between this method and <seealso cref="Process.Exited"/> event is that this method will open a new handle with <see href="https://learn.microsoft.com/en-us/windows/win32/procthread/process-security-and-access-rights">SYNCHRONIZE access right</see> and wait for the handle's signal, this should avoid privilege-related issues in case the current process is unelevated while <paramref name="process"/> process is elevated.</para>
        /// <para><paramref name="callback"/> will not be invoked if <paramref name="cancellationToken"/> signal cancel waiting.</para>
        /// </remarks>
        /// <returns><see langword="true"/> if the method successfully registered the <paramref name="callback"/> to process exit signal. Otherwise, <see langword="false"/>. If the <paramref name="process"/> becomes invalid, this method will also return <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="process"/> or <paramref name="callback"/> is <see langword="null"/>.</exception>
        public static bool RegisterProcessExitCallback(this Process process, Action<Process> callback, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(process);
            ArgumentNullException.ThrowIfNull(callback);

            var handle = PInvoke.OpenProcess(MSWin32.System.Threading.PROCESS_ACCESS_RIGHTS.PROCESS_SYNCHRONIZE, false, unchecked((uint)process.Id));
            if (handle.IsNull)
            {
                return false;
            }
            var waitHandle = new ProcessWaitHandle(new SafeWaitHandle(handle.Value, true));
            var tuple = new Tuple<ProcessWaitHandle, Process, Action<Process>>(waitHandle, process, callback);
            var registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(waitHandle, new WaitOrTimerCallback(ProcessWaitedForExit), new Tuple<ProcessWaitHandle, Process, Action<Process>>(waitHandle, process, callback), Timeout.Infinite, true);
            cancellationToken.Register(obj =>
            {
                if (obj is Tuple<RegisteredWaitHandle, ProcessWaitHandle> data)
                {
                    var (registeredWaitHandle, waitHandle) = data;
                    registeredWaitHandle.Unregister(null);
                    waitHandle.Dispose();
                }
            }, new Tuple<RegisteredWaitHandle, ProcessWaitHandle>(registeredWaitHandle, waitHandle), false);
            return true;
        }

        sealed class ProcessWaitHandle : WaitHandle
        {
            public ProcessWaitHandle(SafeWaitHandle swh)
            {
                this.SafeWaitHandle = swh;
            }
        }

        private static void ProcessWaitedForExit(object? obj, bool timedOut)
        {
            if (obj is Tuple<ProcessWaitHandle, Process, Action<Process>> tuple)
            {
                var (waitHandle, myself, callback) = tuple;
                waitHandle.Dispose();
                callback.Invoke(myself);
            }
        }

        /// <summary>The hint about type of path to returns when calling the method.</summary>
        public enum QueryProcessNameType : uint
        {
            /// <summary>The name should use the Win32 path format.</summary>
            Win32 = 0,
            /// <summary>The name should use the native system path format.</summary>
            Native = 0x00000001
        }

        /// <summary>Retrieves the full name of the executable image for the specified process.</summary>
        /// <param name="process">The process to get the file path.</param>
        /// <param name="buffer">The minimum number of character buffer size to pre-allocate to fetch the path string. Default size is 4096.</param>
        /// <param name="uacHint">Hinting the API to aware about access rights between proccesses with different privilleges. This mainly to avoid overhead caused by raising Exception.</param>
        /// <returns>A string contains full path to the executable file which started the process. Or <see langword="null"/> on failures.</returns>
        public static string? QueryFullProcessImageName(this Process process, int buffer = 4096, bool uacHint = false)
            => QueryFullProcessImageName(process, QueryProcessNameType.Win32, buffer, uacHint);

        /// <summary>Retrieves the full name of the executable image for the specified process.</summary>
        /// <param name="processHandle">The handle to the process to get the file path.</param>
        /// <param name="buffer">The minimum number of character buffer size to pre-allocate to fetch the path string. Default size is 4096.</param>
        /// <returns>A string contains full path to the executable file which started the process. Or <see langword="null"/> on failures.</returns>
        public static string? QueryFullProcessImageName(SafeProcessHandle processHandle, int buffer = 4096)
            => QueryFullProcessImageName(processHandle, QueryProcessNameType.Win32, buffer);

        /// <summary>Retrieves the full name of the executable image for the specified process.</summary>
        /// <param name="process">The process to get the file path.</param>
        /// <param name="nameType">The hint about type of path to returns when calling the method.</param>
        /// <param name="buffer">The minimum number of character buffer size to pre-allocate to fetch the path string. Default size is 4096.</param>
        /// <param name="uacHint">Hinting the API to aware about access rights between proccesses with different privilleges. This mainly to avoid overhead caused by raising Exception.</param>
        /// <returns>A string contains full path to the executable file which started the process. Or <see langword="null"/> on failures.</returns>
        public static string? QueryFullProcessImageName(this Process process, QueryProcessNameType nameType, int buffer = 4096, bool uacHint = false)
        {
            SafeProcessHandle hProcess;
            bool isOwnHandle = false;
            try
            {
                if (uacHint)
                {
                    if (UacHelper.IsCurrentProcessElevated)
                    {
                        // This may still cause Access Denied error.
                        hProcess = process.SafeHandle;
                    }
                    else
                    {
                        hProcess = OpenProcessForQueryLimitedInfo(unchecked((uint)(process.Id)));
                        if (hProcess.IsInvalid)
                        {
                            return null;
                        }
                        isOwnHandle = true;
                    }
                }
                else
                {
                    // This may cause Access Denied error.
                    hProcess = process.SafeHandle;
                }
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.HResult == -2147467259)
            {
                // Should be access denied. So we open by our own with "LimitedQuery" access right.
                hProcess = OpenProcessForQueryLimitedInfo(unchecked((uint)(process.Id)));
                if (hProcess.IsInvalid)
                {
                    return null;
                }
                isOwnHandle = true;
            }
            try
            {
                return QueryFullProcessImageName(hProcess, isOwnHandle ? 0 : unchecked((uint)(process.Id)), nameType, buffer);
            }
            finally
            {
                if (isOwnHandle)
                {
                    hProcess.Dispose();
                }
            }
        }

        /// <summary>Retrieves the full name of the executable image for the specified process.</summary>
        /// <param name="processHandle">The handle to the process to get the file path.</param>
        /// <param name="nameType">The hint about type of path to returns when calling the method.</param>
        /// <param name="buffer">The minimum number of character buffer size to pre-allocate to fetch the path string. Default size is 4096.</param>
        /// <returns>A string contains full path to the executable file which started the process. Or <see langword="null"/> on failures.</returns>
        public static string? QueryFullProcessImageName(SafeProcessHandle processHandle, QueryProcessNameType nameType, int buffer = 4096)
            => QueryFullProcessImageName(processHandle, UacHelper.IsCurrentProcessElevated ? 0 : PInvoke.GetProcessId(processHandle), nameType, buffer);

        private static string? QueryFullProcessImageName(SafeProcessHandle processHandle, uint processId, QueryProcessNameType dwNameType, int buffer)
        {
            char[] ch = ArrayPool<char>.Shared.Rent(buffer + 1);
            try
            {
                uint bufferLength = Convert.ToUInt32(ch.Length - 1);
                bool isSuccess;
                unsafe
                {
                    fixed (char* c = ch)
                    {
                        isSuccess = PInvoke.QueryFullProcessImageName(processHandle, Unsafe.As<QueryProcessNameType, MSWin32.System.Threading.PROCESS_NAME_FORMAT>(ref dwNameType), new MSWin32.Foundation.PWSTR(c), ref bufferLength);
                    }
                }
                if (isSuccess)
                {
                    return new string(ch, 0, Convert.ToInt32(bufferLength));
                }
                else if (processId != 0)
                {
                    bufferLength = Convert.ToUInt32(ch.Length - 1);
                    using (var hProcess = OpenProcessForQueryLimitedInfo(processId))
                    {
                        if (hProcess.IsInvalid)
                        {
                            return null;
                        }
                        unsafe
                        {
                            fixed (char* c = ch)
                            {
                                isSuccess = PInvoke.QueryFullProcessImageName(hProcess, Unsafe.As<QueryProcessNameType, MSWin32.System.Threading.PROCESS_NAME_FORMAT>(ref dwNameType), new MSWin32.Foundation.PWSTR(c), ref bufferLength);
                            }
                        }
                        if (isSuccess)
                        {
                            return new string(ch, 0, Convert.ToInt32(bufferLength));
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
                return null;
            }
            finally
            {
                ArrayPool<char>.Shared.Return(ch, true);
            }
        }
    }
}
#nullable restore
