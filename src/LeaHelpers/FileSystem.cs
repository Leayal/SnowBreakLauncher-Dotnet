using System;
using MSWin32 = global::Windows.Win32;
using MSFileSystem = global::Windows.Win32.Storage.FileSystem;
using PInvoke = global::Windows.Win32.PInvoke;
using System.Runtime.CompilerServices;
using System.IO;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using Windows.Win32.Storage.FileSystem;
using System.Windows.Markup;

namespace Leayal.Shared.Windows
{
    /// <summary>Low-level FileSystem APIs</summary>
    public static class FileSystem
    {
        const int FILE_ATTRIBUTE_DIRECTORY = (int)FileAttributes.Directory,
            CopyBufferingSize = 1024 * 32;
        private readonly static string PrefixLongPath = @"\\?\";

        const int MAX_PATH = 260;

        /// <summary>Copies a file (also copies attributes, including file times) to a destination.</summary>
        /// <param name="srcPath">The path to the source file to be copied.</param>
        /// <param name="dstPath">The destination of the copied file.</param>
        /// <param name="overwrite"><see langword="true"/> to allow overwriting if the destination is already existed. Otherwise, <see langword="false"/>.</param>
        /// <param name="copyBufferingSize">The buffering size (in bytes) for copying. Should not be less than 4096 bytes.</param>
        /// <exception cref="ArgumentNullException"><paramref name="srcPath" /> or <paramref name="dstPath" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentException"><paramref name="srcPath" /> or <paramref name="dstPath" /> is an empty string (""), contains only white space, or contains one or more invalid characters.
        /// -or-
        /// <paramref name="srcPath" /> or <paramref name="dstPath" /> refers to a non-file device, such as <c>CON:</c>, <c>COM1:</c>, <c>LPT1:</c>, etc. in an NTFS environment.</exception>
        /// <exception cref="NotSupportedException"><paramref name="srcPath" /> or <paramref name="dstPath" /> refers to a non-file device, such as <c>CON:</c>, <c>COM1:</c>, <c>LPT1:</c>, etc. in a non-NTFS environment.</exception>
        /// <exception cref="FileNotFoundException">The file specified by <paramref name="srcPath" /> does not exist.</exception>
        /// <exception cref="System.Security.SecurityException">The caller does not have the required permission.</exception>
        /// <exception cref="DirectoryNotFoundException">The specified path is invalid, such as being on an unmapped drive.</exception>
        /// <exception cref="PathTooLongException">The specified path, file name, or both exceed the system-defined maximum length. </exception>
        public static void CopyFile(string srcPath, string dstPath, bool overwrite = false, int copyBufferingSize = CopyBufferingSize)
        {
            if (copyBufferingSize < 4096)
            {
                throw new ArgumentOutOfRangeException(nameof(copyBufferingSize), "'copyBufferingSize' should not be less than 4096");
            }
            bool isDstExist = File.Exists(dstPath);
            if (!overwrite && isDstExist) return;

            using (var hSrc = File.OpenHandle(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan))
            using (var fsSrc = new FileStream(hSrc, FileAccess.Read, 0))
            {
                if (overwrite && isDstExist)
                {
                    var attr = File.GetAttributes(dstPath);
                    if ((attr & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(dstPath, attr & ~FileAttributes.ReadOnly);
                    }
                }

                var fileLength = fsSrc.Length;
                using (var hDst = File.OpenHandle(dstPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, FileOptions.None, fileLength))
                {
                    // Must add ref manually to avoid Handle closing upon closing the filestream below.
                    bool addRefSuccess = false;
                    hDst.DangerousAddRef(ref addRefSuccess);
                    try
                    {
                        if (fileLength != 0)
                        {
                            // Don't dispose the stream, it will close the file handle, we still need the handle to set file times and attributes.
                            using (var fsDst = new FileStream(hDst, FileAccess.Write, 0))
                            {
                                var buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(copyBufferingSize);
                                int bufferLength = buffer.Length;
                                try
                                {
                                    int read;
                                    while ((read = fsSrc.Read(buffer, 0, bufferLength)) != 0)
                                    {
                                        fsDst.Write(buffer, 0, read);
                                    }
                                    fsDst.Flush(flushToDisk: true);
                                }
                                finally
                                {
                                    System.Buffers.ArrayPool<byte>.Shared.Return(buffer, true);
                                }
                                if (!addRefSuccess)
                                {
                                    // In case reference count couldn't be increased. We copy attribute before closing the file stream.
                                    // Though, this can't be happened anyway.
                                    CopyAttributes(hSrc, hDst);
                                }
                            }
                        }

                        if (addRefSuccess)
                        {
                            CopyAttributes(hSrc, hDst);
                        }
                    }
                    finally
                    {
                        if (addRefSuccess)
                        {
                            hDst.DangerousRelease();
                        }
                    }
                }
            }
        }

        /// <summary>Copies attributes (including file times) of a file to another.</summary>
        /// <param name="srcPath">The path to the source file to copy the attributes.</param>
        /// <param name="dstPath">The destination file to receive the copied attributes.</param>
        /// <exception cref="ArgumentNullException"><paramref name="srcPath" /> or <paramref name="dstPath" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentException"><paramref name="srcPath" /> or <paramref name="dstPath" /> is an empty string (""), contains only white space, or contains one or more invalid characters.
        /// -or-
        /// <paramref name="srcPath" /> or <paramref name="dstPath" /> refers to a non-file device, such as <c>CON:</c>, <c>COM1:</c>, <c>LPT1:</c>, etc. in an NTFS environment.</exception>
        /// <exception cref="NotSupportedException"><paramref name="srcPath" /> or <paramref name="dstPath" /> refers to a non-file device, such as <c>CON:</c>, <c>COM1:</c>, <c>LPT1:</c>, etc. in a non-NTFS environment.</exception>
        /// <exception cref="FileNotFoundException">The file specified by <paramref name="srcPath" /> does not exist.</exception>
        /// <exception cref="System.Security.SecurityException">The caller does not have the required permission.</exception>
        /// <exception cref="DirectoryNotFoundException">The specified path is invalid, such as being on an unmapped drive.</exception>
        /// <exception cref="PathTooLongException">The specified path, file name, or both exceed the system-defined maximum length. </exception>
        public static bool CopyAttributes(string srcPath, string dstPath)
        {
            using (var hSrc = File.OpenHandle(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.None))
            using (var hDst = File.OpenHandle(dstPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, FileOptions.None))
            {
                return CopyAttributes(hSrc, hDst);
            }
        }

        /// <summary>Copies attributes (including file times) of a file to another.</summary>
        /// <param name="hSrc">The handle of the source file to be copied.</param>
        /// <param name="hDst">The handle of the destination file.</param>
        public static bool CopyAttributes(SafeFileHandle hSrc, SafeFileHandle hDst)
        {
            MSFileSystem.FILE_BASIC_INFO info_src = new MSFileSystem.FILE_BASIC_INFO(),
                       info_dst = new MSFileSystem.FILE_BASIC_INFO();

            unsafe
            {
                if (GetFileBasicInformationByHandle(hSrc, ref info_src)
                && GetFileBasicInformationByHandle(hDst, ref info_dst))
                {
                    info_dst.FileAttributes = info_src.FileAttributes;
                    info_dst.LastAccessTime = info_src.LastAccessTime;
                    info_dst.LastWriteTime = info_src.LastWriteTime;
                    info_dst.CreationTime = info_src.CreationTime;

                    return SetFileBasicInformationByHandle(hDst, ref info_dst);
                }
            }

            return false;
        }

        /// <summary>Tries to set the file times from a handle to a file.</summary>
        /// <param name="filehandle">The file handle to set the time information.</param>
        /// <param name="creationTime">The creation time of the file.</param>
        /// <param name="lastAccessTime">The last accessed time of the file.</param>
        /// <param name="lastWriteTime">The last written time of the file.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">All three <paramref name="creationTime"/>, <paramref name="lastAccessTime"/>, <paramref name="lastWriteTime"/> are not set.</exception>
        public static bool SetFileTime(SafeFileHandle filehandle, [Optional] DateTime creationTime, [Optional] DateTime lastAccessTime, [Optional] DateTime lastWriteTime)
        {
            DateTime emptyTime = default;
            if (creationTime == emptyTime && lastAccessTime == emptyTime && lastWriteTime == emptyTime)
                throw new ArgumentException("There must be at least one parameter have a valid DateTime. It would be meaningless otherwise and questionable to make a call to this method without any valid DateTime object.");

            long t_creationTime = creationTime.ToFileTimeUtc(),
                t_lastAccessTime = lastAccessTime.ToFileTimeUtc(),
                t_lastWriteTime = lastWriteTime.ToFileTimeUtc();

            unsafe
            {
                return PInvoke.SetFileTime(filehandle,
                    Unsafe.AsRef<System.Runtime.InteropServices.ComTypes.FILETIME>(&t_creationTime),
                    Unsafe.AsRef<System.Runtime.InteropServices.ComTypes.FILETIME>(&t_lastAccessTime),
                    Unsafe.AsRef<System.Runtime.InteropServices.ComTypes.FILETIME>(&t_lastWriteTime));
            }
        }

        /// <summary>Tries to get the file times from a handle to a file.</summary>
        /// <param name="filehandle">The file handle to query time information.</param>
        /// <param name="creationTime">The creation time (in local time) of the file.</param>
        /// <param name="lastAccessTime">The last accessed time (in local time) of the file.</param>
        /// <param name="lastWriteTime">The last written time (in local time) of the file.</param>
        /// <returns></returns>
#if NET7_0_OR_GREATER
        [Obsolete("You don't need this as .NET 7 already has similar standard API which accepts file handle.", false)]
#endif
        public static bool GetFileTime(SafeFileHandle filehandle, out DateTime creationTime, out DateTime lastAccessTime, out DateTime lastWriteTime)
        {
            System.Runtime.InteropServices.ComTypes.FILETIME t_creationTime = new System.Runtime.InteropServices.ComTypes.FILETIME(),
                t_lastAccessTime = new System.Runtime.InteropServices.ComTypes.FILETIME(),
                t_lastWriteTime = new System.Runtime.InteropServices.ComTypes.FILETIME();

            unsafe
            {
                if (PInvoke.GetFileTime(filehandle, &t_creationTime, &t_lastAccessTime, &t_lastWriteTime))
                {
                    creationTime = t_creationTime.ToDateTime();
                    lastAccessTime = t_lastAccessTime.ToDateTime();
                    lastWriteTime = t_lastWriteTime.ToDateTime();
                    return true;
                }
                else
                {
                    creationTime = DateTime.MinValue;
                    lastAccessTime = DateTime.MinValue;
                    lastWriteTime = DateTime.MinValue;
                    return false;
                }
            }
        }

        /// <summary>Tries to get the file times from a file stream.</summary>
        /// <param name="stream">The file stream to query time information.</param>
        /// <param name="creationTime">The creation time (in UTC time) of the file.</param>
        /// <param name="lastAccessTime">The last accessed time (in UTC time) of the file.</param>
        /// <param name="lastWriteTime">The last written time (in UTC time) of the file.</param>
        /// <returns><see langword="true"/> if the call succeeds. Otherwise, <see langword="false"/>.</returns>
#if NET7_0_OR_GREATER
        [Obsolete("You don't need this as .NET 7 already has similar standard API which accepts file handle.", false)]
#endif
        public static bool GetFileTimeUTC(this FileStream stream, out DateTime creationTime, out DateTime lastAccessTime, out DateTime lastWriteTime)
            => GetFileTimeUTC(stream.SafeFileHandle, out creationTime, out lastAccessTime, out lastWriteTime);

        /// <summary>Tries to get the file times from a handle to a file.</summary>
        /// <param name="filehandle">The file handle to query time information.</param>
        /// <param name="creationTime">The creation time (in UTC time) of the file.</param>
        /// <param name="lastAccessTime">The last accessed time (in UTC time) of the file.</param>
        /// <param name="lastWriteTime">The last written time (in UTC time) of the file.</param>
        /// <returns><see langword="true"/> if the call succeeds. Otherwise, <see langword="false"/>.</returns>
#if NET7_0_OR_GREATER
        [Obsolete("You don't need this as .NET 7 already has similar standard API which accepts file handle.", false)]
#endif
        public static bool GetFileTimeUTC(SafeFileHandle filehandle, out DateTime creationTime, out DateTime lastAccessTime, out DateTime lastWriteTime)
        {
            System.Runtime.InteropServices.ComTypes.FILETIME t_creationTime = new System.Runtime.InteropServices.ComTypes.FILETIME(),
                t_lastAccessTime = new System.Runtime.InteropServices.ComTypes.FILETIME(),
                t_lastWriteTime = new System.Runtime.InteropServices.ComTypes.FILETIME();

            unsafe
            {
                if (PInvoke.GetFileTime(filehandle, &t_creationTime, &t_lastAccessTime, &t_lastWriteTime))
                {
                    creationTime = t_creationTime.ToDateTimeUTC();
                    lastAccessTime = t_lastAccessTime.ToDateTimeUTC();
                    lastWriteTime = t_lastWriteTime.ToDateTimeUTC();
                    return true;
                }
                else
                {
                    creationTime = DateTime.MinValue;
                    lastAccessTime = DateTime.MinValue;
                    lastWriteTime = DateTime.MinValue;
                    return false;
                }
            }
        }

        /// <summary>Tries to get the creation time of a file from a file stream.</summary>
        /// <param name="stream">The file stream to fetch handle to query time information.</param>
        /// <param name="creationTime">The creation time (in UTC time) of the file.</param>
        /// <returns></returns>
#if NET7_0_OR_GREATER
        [Obsolete("You don't need this as .NET 7 already has similar standard API which accepts file handle.", false)]
#endif
        public static bool GetFileCreationTimeUTC(this FileStream stream, out DateTime creationTime)
            => GetFileCreationTimeUTC(stream.SafeFileHandle, out creationTime);

        /// <summary>Tries to get the creation time of a file from its handle.</summary>
        /// <param name="filehandle">The file handle to query time information.</param>
        /// <param name="creationTime">The creation time (in UTC time) of the file.</param>
        /// <returns></returns>
#if NET7_0_OR_GREATER
        [Obsolete("You don't need this as .NET 7 already has similar standard API which accepts file handle.", false)]
#endif
        public static bool GetFileCreationTimeUTC(SafeFileHandle filehandle, out DateTime creationTime)
        {
            var t = new System.Runtime.InteropServices.ComTypes.FILETIME();

            unsafe
            { 
                bool isRefAdded = false;
                filehandle.DangerousAddRef(ref isRefAdded);
                if (!isRefAdded)
                {
                    throw new ObjectDisposedException(nameof(filehandle));
                }

                try
                {
                    if (PInvoke.GetFileTime(new MSWin32.Foundation.HANDLE(filehandle.DangerousGetHandle()), lpCreationTime: &t))
                    {
                        creationTime = t.ToDateTimeUTC();
                        return true;
                    }
                    else
                    {
                        creationTime = DateTime.MinValue;
                        return false;
                    }
                }
                finally
                {
                    filehandle.DangerousRelease();
                }
            }
        }

        /// <summary>Tries to get the creation time of a file from a file stream.</summary>
        /// <param name="stream">The file stream to fetch handle to query time information.</param>
        /// <param name="lastWriteTime">The last written time (in UTC time) of the file.</param>
        /// <returns></returns>
#if NET7_0_OR_GREATER
        [Obsolete("You don't need this as .NET 7 already has similar standard API which accepts file handle.", false)]
#endif
        public static bool GetFileLastWriteTimeUTC(this FileStream stream, out DateTime lastWriteTime)
            => GetFileLastWriteTimeUTC(stream.SafeFileHandle, out lastWriteTime);

        /// <summary>Tries to get the creation time of a file from its handle.</summary>
        /// <param name="filehandle">The file handle to query time information.</param>
        /// <param name="lastWriteTime">The last written time (in UTC time) of the file.</param>
        /// <returns></returns>
#if NET7_0_OR_GREATER
        [Obsolete("You don't need this as .NET 7 already has similar standard API which accepts file handle.", false)]
#endif
        public static bool GetFileLastWriteTimeUTC(SafeFileHandle filehandle, out DateTime lastWriteTime)
        {
            var t = new System.Runtime.InteropServices.ComTypes.FILETIME();

            unsafe
            {
                bool isRefAdded = false;
                filehandle.DangerousAddRef(ref isRefAdded);
                if (!isRefAdded)
                {
                    throw new ObjectDisposedException(nameof(filehandle));
                }

                try
                {
                    if (PInvoke.GetFileTime(new MSWin32.Foundation.HANDLE(filehandle.DangerousGetHandle()), lpLastWriteTime: &t))
                    {
                        lastWriteTime = t.ToDateTimeUTC();
                        return true;
                    }
                    else
                    {
                        lastWriteTime = DateTime.MinValue;
                        return false;
                    }
                }
                finally
                {
                    filehandle.DangerousRelease();
                }
            }
        }

        /// <summary>Tries to get the creation time of a file from a file stream.</summary>
        /// <param name="stream">The file stream to fetch handle to query time information.</param>
        /// <param name="lastAccessTime">The last accessed time (in UTC time) of the file.</param>
        /// <returns></returns>
#if NET7_0_OR_GREATER
        [Obsolete("You don't need this as .NET 7 already has similar standard API which accepts file handle.", false)]
#endif
        public static bool GetFileLastAccessTimeUTC(this FileStream stream, out DateTime lastAccessTime)
            => GetFileLastAccessTimeUTC(stream.SafeFileHandle, out lastAccessTime);

        /// <summary>Tries to get the creation time of a file from its handle.</summary>
        /// <param name="filehandle">The file handle to query time information.</param>
        /// <param name="lastAccessTime">The last accessed time (in UTC time) of the file.</param>
        /// <returns></returns>
#if NET7_0_OR_GREATER
        [Obsolete("You don't need this as .NET 7 already has similar standard API which accepts file handle.", false)]
#endif
        public static bool GetFileLastAccessTimeUTC(SafeFileHandle filehandle, out DateTime lastAccessTime)
        {
            var t = new System.Runtime.InteropServices.ComTypes.FILETIME();

            unsafe
            {
                bool isRefAdded = false;
                filehandle.DangerousAddRef(ref isRefAdded);
                if (!isRefAdded)
                {
                    throw new ObjectDisposedException(nameof(filehandle));
                }

                try
                {
                    if (PInvoke.GetFileTime(new MSWin32.Foundation.HANDLE(filehandle.DangerousGetHandle()), lpLastAccessTime: &t))
                    {
                        lastAccessTime = t.ToDateTimeUTC();
                        return true;
                    }
                    else
                    {
                        lastAccessTime = DateTime.MinValue;
                        return false;
                    }
                }
                finally
                {
                    filehandle.DangerousRelease();
                }
            }
        }

        internal static unsafe bool GetFileBasicInformationByHandle(SafeFileHandle hFile, ref MSFileSystem.FILE_BASIC_INFO pInfo)
            => PInvoke.GetFileInformationByHandleEx(hFile, MSFileSystem.FILE_INFO_BY_HANDLE_CLASS.FileBasicInfo, Unsafe.AsPointer(ref pInfo), (uint)Marshal.SizeOf(pInfo));

        internal static unsafe bool SetFileBasicInformationByHandle(SafeFileHandle hFile, ref MSFileSystem.FILE_BASIC_INFO pInfo)
            => PInvoke.SetFileInformationByHandle(hFile, MSFileSystem.FILE_INFO_BY_HANDLE_CLASS.FileBasicInfo, Unsafe.AsPointer(ref pInfo), (uint)Marshal.SizeOf(pInfo));

        /// <summary>Gets file information from a file handle.</summary>
        /// <param name="hFile">The file handle to query time information.</param>
        /// <param name="info">The information you want.</param>
        /// <returns><see langword="true"/> if the call succeeds. Otherwise, <see langword="false"/>.</returns>
        public static bool GetFileBasicInformationByHandle(SafeFileHandle hFile, out FileBasicInfo info)
        {
            var obj = new MSFileSystem.FILE_BASIC_INFO();
            if (GetFileBasicInformationByHandle(hFile, ref obj))
            {
                info = new FileBasicInfo(in obj);
                return true;
            }
            info = default;
            return false;
        }

        /// <summary>Sets file information the file handle pointing to.</summary>
        /// <param name="hFile">The file handle to query time information.</param>
        /// <param name="info">The information you want.</param>
        /// <returns><see langword="true"/> if the call succeeds. Otherwise, <see langword="false"/>.</returns>
        public static bool SetFileBasicInformationByHandle(SafeFileHandle hFile, in FileBasicInfo info)
        {
            var obj = info.data;
            return SetFileBasicInformationByHandle(hFile, ref obj);
        }

        /// <summary>Converts the native file time to standard <seealso cref="DateTime"/> structure.</summary>
        /// <param name="time">The native file time structure.</param>
        /// <returns>A <seealso cref="DateTime"/> which is in UTC time format.</returns>
        public static unsafe DateTime ToDateTimeUTC(this System.Runtime.InteropServices.ComTypes.FILETIME time)
            => DateTime.FromFileTimeUtc(Unsafe.AsRef<long>(&time));

        /// <summary>Converts the native file time to standard <seealso cref="DateTime"/> structure.</summary>
        /// <param name="time">The native file time structure.</param>
        /// <returns>A <seealso cref="DateTime"/> which is in local time format.</returns>
        public static unsafe DateTime ToDateTime(this System.Runtime.InteropServices.ComTypes.FILETIME time)
            => DateTime.FromFileTime(Unsafe.Read<long>(&time));

        /// <summary>Check if the given path is actually existed on the filesystem.</summary>
        /// <param name="path">The path to check existence.</param>
        /// <returns><see langword="true"/> if the path is existed. Otherwise, <see langword="false"/>.</returns>
        public static bool PathExists(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            if (path.Length < MAX_PATH)
            {
                if (!Path.IsPathRooted(path) || Path.IsPathFullyQualified(path))
                {
                    if (path.StartsWith(PrefixLongPath))
                    {
                        path = path.Remove(0, PrefixLongPath.Length);
                    }
                    return PInvoke.PathFileExists(path);
                }
            }
            else if (!Path.IsPathFullyQualified(path))
            {
                path = Path.GetFullPath(path.StartsWith(PrefixLongPath) ? path.Remove(0, PrefixLongPath.Length) : path);
            }
            if (!path.StartsWith(PrefixLongPath))
            {
                path = PrefixLongPath + path;
            }
            var stuff = new MSFileSystem.WIN32_FILE_ATTRIBUTE_DATA();
            bool isSuccess;
            unsafe
            {
                isSuccess = PInvoke.GetFileAttributesEx(path, MSFileSystem.GET_FILEEX_INFO_LEVELS.GetFileExInfoStandard, Unsafe.AsPointer(ref stuff));
            }
            return isSuccess;
        }

        /// <summary>Check if the given path is actually existed on the filesystem.</summary>
        /// <param name="path">The path to check existence.</param>
        /// <param name="isDirectory">A boolean determine if the path is pointing to a folder or not.</param>
        /// <returns><see langword="true"/> if the path is existed. Otherwise, <see langword="false"/>.</returns>
        public static bool PathExists(string path, out bool isDirectory)
        {
            if (string.IsNullOrEmpty(path))
            {
                isDirectory = false;
                return false;
            }

            if (path.Length < MAX_PATH)
            {
                if (!Path.IsPathRooted(path) || Path.IsPathFullyQualified(path))
                {
                    if (path.StartsWith(PrefixLongPath))
                    {
                        path = path.Remove(0, PrefixLongPath.Length);
                    }
                }
            }
            else if (!Path.IsPathFullyQualified(path))
            {
                path = Path.GetFullPath(path.StartsWith(PrefixLongPath) ? path.Remove(0, PrefixLongPath.Length) : path);
            }
            if (!path.StartsWith(PrefixLongPath))
            {
                path = PrefixLongPath + path;
            }
            var stuff = new MSFileSystem.WIN32_FILE_ATTRIBUTE_DATA();
            bool isSuccess;
            unsafe
            {
                isSuccess = PInvoke.GetFileAttributesEx(path, MSFileSystem.GET_FILEEX_INFO_LEVELS.GetFileExInfoStandard, Unsafe.AsPointer(ref stuff));
            }
            if (isSuccess)
            {
                isDirectory = (stuff.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == FILE_ATTRIBUTE_DIRECTORY;
            }
            else
            {
                isDirectory = false;
            }
            return isSuccess;
        }

        #region "| Structs |"
        /// <summary>Contains metadata information about a directory or a file..</summary>
        public readonly struct FileBasicInfo
        {
            // Can't use ref or pointer here, I feel like it would go haywire
            // Since "FILE_BASIC_INFO" lives in a local stack of a function, the moment the function returns, "FILE_BASIC_INFO" will be "freed" and the pointer will point to "something unknown".
            // Therefore, we need to create a copy of it.
            internal readonly FILE_BASIC_INFO data;
            internal FileBasicInfo(in FILE_BASIC_INFO data)
            {
                this.data = data;
            }

            /// <summary>Gets file creation time, expressed in file time.</summary>
            public readonly long CreationTimeRaw => this.data.CreationTime;

            /// <summary>Gets file creation time, expressed in system time.</summary>
            public readonly DateTime CreationTime => DateTime.FromFileTimeUtc(this.data.CreationTime);

            /// <summary>Gets file last asccess time, expressed in file time.</summary>
            public readonly long LastAccessTimeRaw => this.data.LastAccessTime;

            /// <summary>Gets file last access time, expressed in system time.</summary>
            public readonly DateTime LastAccessTime => DateTime.FromFileTimeUtc(this.data.LastAccessTime);

            /// <summary>Gets file last write time, expressed in file time.</summary>
            public readonly long LastWriteTimeRaw => this.data.LastWriteTime;

            /// <summary>Gets file last write time, expressed in system time.</summary>
            public readonly DateTime LastWriteTime => DateTime.FromFileTimeUtc(this.data.LastWriteTime);

            /// <summary>Gets metadata change time, expressed in file time.</summary>
            public readonly long ChangeTimeRaw => this.data.ChangeTime;

            /// <summary>Gets metadata change time, expressed in system time.</summary>
            public readonly DateTime ChangeTime => DateTime.FromFileTimeUtc(this.data.ChangeTime);

            /// <summary>Gets file attribute, expressed in unmanaged value.</summary>
            public readonly uint FileAttributesRaw => this.data.FileAttributes;

            /// <summary>Gets file attribute, expressed in .NET managed value.</summary>
            public readonly FileAttributes FileAttributes => (FileAttributes)(this.data.FileAttributes);
        }
        #endregion
    }
}
