using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using MSWin32 = global::Windows.Win32;
using PInvoke = global::Windows.Win32.PInvoke;

namespace Leayal.Shared.Windows
{
    /// <summary>A helper class providing convenient methods to read User Account Control (UAC)'s settings.</summary>
    public static class UacHelper
    {
        private const string uacRegistryKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\System";
        private const string uacRegistryValue = "EnableLUA";

        /// <summary>Gets a boolean determining whether UAC is enabled or not.</summary>
        public static bool IsUacEnabled
        {
            get
            {
                using (var uacKey = Registry.LocalMachine.OpenSubKey(uacRegistryKey, false))
                {
                    if (uacKey != null)
                    {
                        var obj = uacKey.GetValue(uacRegistryValue);
                        if (obj is int i && i != 0)
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }
        }

        /// <summary>A boolean which determines whether the current running process is elevated.</summary>
        /// <remarks><see langword="true"/> if the current process is elevated. Otherwise, <see langword="false"/>.</remarks>
        public static readonly bool IsCurrentProcessElevated;

        static UacHelper()
        {
            using (var proc = Process.GetCurrentProcess())
            {
                // Don't worry about Handle's access right for the current process. Full access right~
                IsCurrentProcessElevated = IsProcessElevated(proc.SafeHandle);
            }
        }

        /// <summary>Gets a boolean which determines whether the specified process is elevated.</summary>
        /// <param name="process">The process to determine.</param>
        /// <returns><see langword="true"/> if the process is elevated. Otherwise, <see langword="false"/>.</returns>
        /// <exception cref="System.ComponentModel.Win32Exception">The current process doesn't have enough privilege to query information of target process.</exception>
        public static bool IsProcessElevated(this Process process)
        {
            SafeProcessHandle hProcess;
            bool isOwnHandle = false;
            try
            {
                if (UacHelper.IsCurrentProcessElevated)
                {
                    // This may still cause Access Denied error.
                    hProcess = process.SafeHandle;
                }
                else
                {
                    var handle = PInvoke.OpenProcess(MSWin32.System.Threading.PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, Convert.ToUInt32(process.Id));
                    if (handle.IsNull)
                    {
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    }
                    hProcess = new SafeProcessHandle(handle.Value, true);
                    // hProcess = OpenProcess(QueryLimitedInformation, false, process.Id);
                    if (hProcess.IsInvalid)
                    {
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    }
                    isOwnHandle = true;
                }
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.HResult == -2147467259)
            {
                // Should be access denied. So we open by our own with "QueryLimited" access right.
                var handle = PInvoke.OpenProcess(MSWin32.System.Threading.PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, Convert.ToUInt32(process.Id));
                if (handle.IsNull)
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
                hProcess = new SafeProcessHandle(handle.Value, true);
                if (hProcess.IsInvalid)
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
                isOwnHandle = true;
            }
            try
            {
                return IsProcessElevated(hProcess);
            }
            finally
            {
                if (isOwnHandle)
                {
                    hProcess.Dispose();
                }
            }
        }

        /// <summary>Gets a boolean which determines whether the specified process is elevated.</summary>
        /// <param name="processHandle">The handle to a process.</param>
        /// <returns><see langword="true"/> if the process is elevated. Otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="processHandle"/> is <see langword="null"/>.</exception>
        /// <exception cref="ObjectDisposedException"><paramref name="processHandle"/> is closed or invalid.</exception>
        /// <exception cref="System.ComponentModel.Win32Exception">The current process doesn't have enough privilege to query information of target process.</exception>
        public static bool IsProcessElevated(SafeProcessHandle processHandle)
        {
            if (processHandle == null) throw new ArgumentNullException(nameof(processHandle));
            if (processHandle.IsClosed || processHandle.IsInvalid) throw new ObjectDisposedException(nameof(processHandle));
            if (IsUacEnabled)
            {
                if (!PInvoke.OpenProcessToken(processHandle, MSWin32.Security.TOKEN_ACCESS_MASK.TOKEN_READ, out var tokenHandle))
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    // throw new ApplicationException("Could not get process token. Win32 Error Code: " + Marshal.GetLastWin32Error());
                }

                MSWin32.Security.TOKEN_ELEVATION_TYPE elevationResult = MSWin32.Security.TOKEN_ELEVATION_TYPE.TokenElevationTypeDefault;
                const uint elevationResultSize = sizeof(MSWin32.Security.TOKEN_ELEVATION_TYPE);
                try
                {
                    uint returnedSize = 0;
                    bool success;
                    unsafe
                    {  
                        success = PInvoke.GetTokenInformation(tokenHandle, MSWin32.Security.TOKEN_INFORMATION_CLASS.TokenElevationType, Unsafe.AsPointer(ref elevationResult), elevationResultSize, out returnedSize);
                    }
                    
                    if (success)
                    {
                        // elevationResult = (TOKEN_ELEVATION_TYPE)Marshal.ReadInt32(elevationTypePtr);
                        bool isProcessAdmin = elevationResult == MSWin32.Security.TOKEN_ELEVATION_TYPE.TokenElevationTypeFull;
                        return isProcessAdmin;
                    }
                    else
                    {
                        throw new ApplicationException("Unable to determine the current elevation.");
                    }
                }
                finally
                {
                    tokenHandle.Dispose();
                }
            }
            else
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return (principal.IsInRole(WindowsBuiltInRole.Administrator)
                               || principal.IsInRole(0x200)); // Domain Administrator
                }
            }
        }
    }
}
