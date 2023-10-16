using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Unicode;
using System.Threading;

namespace Leayal.Shared.Windows
{
    public static partial class ProcessInfoHelper
    {
        [UnsupportedOSPlatform("windows")]
        internal static partial class Linux
        {
            private static readonly long SSIZE_MAX = Environment.Is64BitProcess ? (2L^63L) : (2^31);

            // https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/Interop/Unix/System.Native/Interop.ReadLink.cs

            /// <summary>
            /// Takes a path to a symbolic link and attempts to place the link target path into the buffer. If the buffer is too
            /// small, the path will be truncated. No matter what, the buffer will not be null terminated.
            /// </summary>
            /// <param name="path">The path to the symlink</param>
            /// <param name="buffer">The buffer to hold the output path</param>
            /// <param name="bufferSize">The size of the buffer</param>
            /// <returns>
            /// Returns the number of bytes placed into the buffer on success; bufferSize if the buffer is too small; and -1 on error.
            /// </returns>
            [LibraryImport("libc", EntryPoint = "readlink", SetLastError = true)]
            private static partial int ReadLink(ref byte path, ref byte buffer, int bufferSize);

            /// <summary>
            /// Takes a path to a symbolic link and returns the link target path.
            /// </summary>
            /// <param name="path">The path to the symlink.</param>
            /// <returns>Returns the link to the target path on success; and null otherwise.</returns>
            internal static string? OS_ReadLink(ReadOnlySpan<char> path)
            {
                if (path.IsEmpty) return null;

                const int StackBufferSize = 256;

                // Use an initial buffer size that prevents disposing and renting
                // a second time when calling ConvertAndTerminateString.
                var utf8PathByteLen = Encoding.UTF8.GetByteCount(path);
                var needborrowed = utf8PathByteLen >= StackBufferSize;
                var borrowed = needborrowed ? ArrayPool<byte>.Shared.Rent(utf8PathByteLen + 1) : null;
                Span<byte> utf8Path = needborrowed ? borrowed : stackalloc byte[StackBufferSize];
                utf8Path.Fill(byte.MinValue);
                Span<byte> spanBuffer = stackalloc byte[StackBufferSize];
                byte[]? arrayBuffer = null;
                try
                {
                    System.Text.Encoding.UTF8.GetBytes(path, utf8Path);
                    ref byte pathReference = ref MemoryMarshal.GetReference(utf8Path);
                    while (true)
                    {
                        int error = 0;
                        try
                        {
                            int resultLength = ReadLink(ref pathReference, ref MemoryMarshal.GetReference(spanBuffer), spanBuffer.Length);

                            if (resultLength < 0)
                            {
                                // error
                                error = Marshal.GetLastPInvokeError();
                                return null;
                            }
                            else if (resultLength < spanBuffer.Length)
                            {
                                // success
                                return Encoding.UTF8.GetString(spanBuffer.Slice(0, resultLength));
                            }
                        }
                        finally
                        {
                            if (arrayBuffer != null)
                            {
                                ArrayPool<byte>.Shared.Return(arrayBuffer);
                            }

                            if (error > 0)
                            {
                                Marshal.SetLastPInvokeError(error);
                            }
                        }

                        // Output buffer was too small, loop around again and try with a larger buffer.
                        arrayBuffer = ArrayPool<byte>.Shared.Rent(spanBuffer.Length * 2);
                        spanBuffer = arrayBuffer;
                    }
                }
                finally
                {
                    if (borrowed != null)
                    {
                        ArrayPool<byte>.Shared.Return(borrowed);
                    }
                }
            }

            internal static string? ReadLink(string path)
            {
                using (var proc = new Process())
                {
                    proc.StartInfo.FileName = "readlink";
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.RedirectStandardOutput = true;
                    proc.StartInfo.ArgumentList.Add("-f");
                    proc.StartInfo.ArgumentList.Add(path);
                    proc.Start();
                    var result = proc.StandardOutput.ReadLine();
                    proc.WaitForExit();
                    return result;
                }
            }

            public static string? GetProcessInfo(Process proc, string infoName)
            {
                var link = $"/proc/{proc.Id.ToString(NumberFormatInfo.InvariantInfo)}/{infoName}";
                var result = ReadLink(link);
                return result;
            }

            readonly struct WrapperRegisterProcessExitCallback
            {
                public readonly Process process;
                public readonly ProcessExitCallback callback;

                public WrapperRegisterProcessExitCallback(Process proc, ProcessExitCallback call, in CancellationToken cancellationToken) : this()
                {
                    this.process = proc;
                    this.callback = call;
                    if (cancellationToken.CanBeCanceled)
                    {
                        cancellationToken.Register(this.OnCancel);
                    }
                }

                public readonly void OnExit(object? sender, EventArgs e)
                {
                    this.OnCancel();
                    callback.Invoke(this.process, 0);
                }

                private readonly void OnCancel()
                {
                    this.process.Exited -= this.OnExit;
                }
            }

            public static bool RegisterProcessExitCallback(Process process, ProcessExitCallback callback, CancellationToken cancellationToken = default, bool immediatelyInvokeCallbackIfAlreadyExited = false)
            {
                ArgumentNullException.ThrowIfNull(process);
                ArgumentNullException.ThrowIfNull(callback);

                if (process.HasExited)
                {
                    if (immediatelyInvokeCallbackIfAlreadyExited)
                    {
                        callback.Invoke(process, 0);
                    }
                    return false;
                }
                else
                {
                    var sss = new WrapperRegisterProcessExitCallback(process, callback, in cancellationToken);
                    if (cancellationToken.CanBeCanceled && cancellationToken.IsCancellationRequested) return false;
                    process.EnableRaisingEvents = true;
                    process.Exited += sss.OnExit;
                    return true;
                }
            }
        }
    }
}
