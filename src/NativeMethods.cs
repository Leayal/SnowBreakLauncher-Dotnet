using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

[assembly: DisableRuntimeMarshalling]

namespace Leayal.SnowBreakLauncher
{
    internal static unsafe partial class NativeMethods
    {
        [Flags]
		internal enum PROCESS_ACCESS_RIGHTS : uint
		{
			PROCESS_TERMINATE = 0x00000001,
			PROCESS_CREATE_THREAD = 0x00000002,
			PROCESS_SET_SESSIONID = 0x00000004,
			PROCESS_VM_OPERATION = 0x00000008,
			PROCESS_VM_READ = 0x00000010,
			PROCESS_VM_WRITE = 0x00000020,
			PROCESS_DUP_HANDLE = 0x00000040,
			PROCESS_CREATE_PROCESS = 0x00000080,
			PROCESS_SET_QUOTA = 0x00000100,
			PROCESS_SET_INFORMATION = 0x00000200,
			PROCESS_QUERY_INFORMATION = 0x00000400,
			PROCESS_SUSPEND_RESUME = 0x00000800,
			PROCESS_QUERY_LIMITED_INFORMATION = 0x00001000,
			PROCESS_SET_LIMITED_INFORMATION = 0x00002000,
			PROCESS_ALL_ACCESS = 0x001FFFFF,
			PROCESS_DELETE = 0x00010000,
			PROCESS_READ_CONTROL = 0x00020000,
			PROCESS_WRITE_DAC = 0x00040000,
			PROCESS_WRITE_OWNER = 0x00080000,
			PROCESS_SYNCHRONIZE = 0x00100000,
			PROCESS_STANDARD_RIGHTS_REQUIRED = 0x000F0000,
		}

        /// <summary>Closes an open object handle.</summary>
        /// <param name="hObject">A valid handle to an open object.</param>
        /// <returns>
        /// <para>If the function succeeds, the return value is nonzero. If the function fails, the return value is zero. To get extended error information, call <a href="https://docs.microsoft.com/windows/desktop/api/errhandlingapi/nf-errhandlingapi-getlasterror">GetLastError</a>. If the application is running under a debugger,  the function will throw an exception if it receives either a  handle value that is not valid  or a pseudo-handle value. This can happen if you close a handle twice, or if you  call <b>CloseHandle</b> on a handle returned by the <a href="https://docs.microsoft.com/windows/desktop/api/fileapi/nf-fileapi-findfirstfilea">FindFirstFile</a> function instead of calling the <a href="https://docs.microsoft.com/windows/desktop/api/fileapi/nf-fileapi-findclose">FindClose</a> function.</para>
        /// </returns>
        /// <remarks>
        /// <para>The <b>CloseHandle</b> function closes handles to the following objects: </para>
        /// <para>This doc was truncated.</para>
        /// <para><see href="https://learn.microsoft.com/windows/win32/api/handleapi/nf-handleapi-closehandle#">Read more on docs.microsoft.com</see>.</para>
        /// </remarks>
        [LibraryImport("KERNEL32.dll", SetLastError = true),
			DefaultDllImportSearchPaths(DllImportSearchPath.System32),
			SupportedOSPlatform("windows")]
		[return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool CloseHandle(nint hObject);

        /// <summary>Opens an existing local process object.</summary>
		/// <param name="dwDesiredAccess">
		/// <para>The access to the process object. This access right is checked against the  security descriptor for the process. This parameter can be one or more of the <a href="https://docs.microsoft.com/windows/desktop/ProcThread/process-security-and-access-rights">process access rights</a>. If the caller has enabled the <a href="https://docs.microsoft.com/windows/win32/secauthz/privilege-constants#SE_DEBUG_NAME">SeDebugPrivilege privilege</a>, the requested access is granted regardless of the contents of the security descriptor.</para>
		/// <para><see href="https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocess#parameters">Read more on docs.microsoft.com</see>.</para>
		/// </param>
		/// <param name="bInheritHandle">If this value is TRUE, processes created by this process will inherit the handle. Otherwise, the processes do not inherit this handle.</param>
		/// <param name="dwProcessId">
		/// <para>The identifier of the local process to be opened. If the specified process is the System Idle Process (0x00000000), the function fails and the last error code is `ERROR_INVALID_PARAMETER`. If the specified process is the System process or one of the Client Server Run-Time Subsystem (CSRSS) processes, this function fails and the last error code is `ERROR_ACCESS_DENIED` because their access restrictions prevent user-level code from opening them. If you are using <a href="https://docs.microsoft.com/windows/desktop/api/processthreadsapi/nf-processthreadsapi-getcurrentprocessid">GetCurrentProcessId</a> as an argument to this function, consider using <a href="https://docs.microsoft.com/windows/desktop/api/processthreadsapi/nf-processthreadsapi-getcurrentprocess">GetCurrentProcess</a> instead of OpenProcess, for improved performance.</para>
		/// <para><see href="https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocess#parameters">Read more on docs.microsoft.com</see>.</para>
		/// </param>
		/// <returns>
		/// <para>If the function succeeds, the return value is an open handle to the specified process. If the function fails, the return value is NULL. To get extended error information, call <a href="https://docs.microsoft.com/windows/desktop/api/errhandlingapi/nf-errhandlingapi-getlasterror">GetLastError</a>.</para>
		/// </returns>
		/// <remarks>
		/// <para>To open a handle to another local process and obtain full access rights, you must enable the SeDebugPrivilege privilege. For more information, see <a href="https://docs.microsoft.com/windows/desktop/SecBP/changing-privileges-in-a-token">Changing Privileges in a Token</a>. The handle returned by the <b>OpenProcess</b> function can be used in any function that requires a handle to a process, such as the <a href="https://docs.microsoft.com/windows/desktop/Sync/wait-functions">wait functions</a>, provided the appropriate access rights were requested. When you are finished with the handle, be sure to close it using the <a href="https://docs.microsoft.com/windows/desktop/api/handleapi/nf-handleapi-closehandle">CloseHandle</a> function.</para>
		/// <para><see href="https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocess#">Read more on docs.microsoft.com</see>.</para>
		/// </remarks>
		[LibraryImport("KERNEL32.dll", SetLastError = true),
            DefaultDllImportSearchPaths(DllImportSearchPath.System32),
            SupportedOSPlatform("windows")]
        internal static partial nint OpenProcess(in PROCESS_ACCESS_RIGHTS dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, in uint dwProcessId);

        internal enum PROCESS_NAME_FORMAT : uint
        {
            PROCESS_NAME_WIN32 = 0U,
            PROCESS_NAME_NATIVE = 1U,
        }

        /// <summary>Retrieves the full name of the executable image for the specified process. (Unicode)</summary>
        /// <param name="hProcess">
        /// <para>A handle to the process. This handle must be created with the PROCESS_QUERY_INFORMATION or PROCESS_QUERY_LIMITED_INFORMATION access right. For more information, see <a href="https://docs.microsoft.com/windows/desktop/ProcThread/process-security-and-access-rights">Process Security and Access Rights</a>.</para>
        /// <para><see href="https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-queryfullprocessimagenamew#parameters">Read more on docs.microsoft.com</see>.</para>
        /// </param>
        /// <param name="dwFlags"></param>
        /// <param name="lpExeName">The path to the executable image. If the function succeeds, this string is null-terminated.</param>
        /// <param name="lpdwSize">On input, specifies the size of the <i>lpExeName</i> buffer, in characters. On success, receives the number of characters written to the buffer, not including the null-terminating character.</param>
        /// <returns>
        /// <para>If the function succeeds, the return value is nonzero. If the function fails, the return value is zero. To get extended error information, call <a href="https://docs.microsoft.com/windows/desktop/api/errhandlingapi/nf-errhandlingapi-getlasterror">GetLastError</a>.</para>
        /// </returns>
        /// <remarks>
        /// <para>To compile an application that uses this function, define _WIN32_WINNT as 0x0600 or later.</para>
        /// <para>> [!NOTE] > The winbase.h header defines QueryFullProcessImageName as an alias which automatically selects the ANSI or Unicode version of this function based on the definition of the UNICODE preprocessor constant. Mixing usage of the encoding-neutral alias with code that not encoding-neutral can lead to mismatches that result in compilation or runtime errors. For more information, see [Conventions for Function Prototypes](/windows/win32/intl/conventions-for-function-prototypes).</para>
        /// <para><see href="https://learn.microsoft.com/windows/win32/api/winbase/nf-winbase-queryfullprocessimagenamew#">Read more on docs.microsoft.com</see>.</para>
        /// </remarks>
        [LibraryImport("KERNEL32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [SupportedOSPlatform("windows")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool QueryFullProcessImageName(in SafeProcessHandle hProcess, in PROCESS_NAME_FORMAT dwFlags, char* lpExeName, ref uint lpdwSize);

        /// <summary>Retrieves the process identifier of the specified process.</summary>
		/// <param name="Process">
		/// <para>A handle to the process. The handle must have the PROCESS_QUERY_INFORMATION or PROCESS_QUERY_LIMITED_INFORMATION access right. For more information, see <a href="https://docs.microsoft.com/windows/desktop/ProcThread/process-security-and-access-rights">Process Security and Access Rights</a>. <b>Windows Server 2003 and Windows XP:  </b>The handle must have the PROCESS_QUERY_INFORMATION access right.</para>
		/// <para><see href="https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocessid#parameters">Read more on docs.microsoft.com</see>.</para>
		/// </param>
		/// <returns>
		/// <para>If the function succeeds, the return value is the process identifier. If the function fails, the return value is zero. To get extended error information, call <a href="https://docs.microsoft.com/windows/desktop/api/errhandlingapi/nf-errhandlingapi-getlasterror">GetLastError</a>.</para>
		/// </returns>
		/// <remarks>
		/// <para>Until a process terminates, its process identifier uniquely identifies it on the system. For more information about access rights, see <a href="https://docs.microsoft.com/windows/desktop/ProcThread/process-security-and-access-rights">Process Security and Access Rights</a>.</para>
		/// <para><see href="https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-getprocessid#">Read more on docs.microsoft.com</see>.</para>
		/// </remarks>
		[LibraryImport("KERNEL32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [SupportedOSPlatform("windows")]
        internal static partial uint GetProcessId(in SafeProcessHandle Process);

        [Flags]
        internal enum TOKEN_ACCESS_MASK : uint
        {
            TOKEN_DELETE = 0x00010000,
            TOKEN_READ_CONTROL = 0x00020000,
            TOKEN_WRITE_DAC = 0x00040000,
            TOKEN_WRITE_OWNER = 0x00080000,
            TOKEN_ACCESS_SYSTEM_SECURITY = 0x01000000,
            TOKEN_ASSIGN_PRIMARY = 0x00000001,
            TOKEN_DUPLICATE = 0x00000002,
            TOKEN_IMPERSONATE = 0x00000004,
            TOKEN_QUERY = 0x00000008,
            TOKEN_QUERY_SOURCE = 0x00000010,
            TOKEN_ADJUST_PRIVILEGES = 0x00000020,
            TOKEN_ADJUST_GROUPS = 0x00000040,
            TOKEN_ADJUST_DEFAULT = 0x00000080,
            TOKEN_ADJUST_SESSIONID = 0x00000100,
            TOKEN_READ = 0x00020008,
            TOKEN_WRITE = 0x000200E0,
            TOKEN_EXECUTE = 0x00020000,
            TOKEN_TRUST_CONSTRAINT_MASK = 0x00020018,
            TOKEN_ACCESS_PSEUDO_HANDLE_WIN8 = 0x00000018,
            TOKEN_ACCESS_PSEUDO_HANDLE = 0x00000018,
            TOKEN_ALL_ACCESS = 0x000F01FF,
        }

        /// <summary>Opens the access token associated with a process.</summary>
        /// <param name="ProcessHandle">A handle to the process whose access token is opened. The process must have the PROCESS_QUERY_LIMITED_INFORMATION access permission. See [Process Security and Access Rights](/windows/win32/procthread/process-security-and-access-rights) for more info.</param>
        /// <param name="DesiredAccess">
        /// <para>Specifies an <a href="https://docs.microsoft.com/windows/desktop/SecGloss/a-gly">access mask</a> that specifies the requested types of access to the access token. These requested access types are compared with the <a href="https://docs.microsoft.com/windows/desktop/SecGloss/d-gly">discretionary access control list</a> (DACL) of the token to determine which accesses are granted or denied.</para>
        /// <para>For a list of access rights for access tokens, see <a href="https://docs.microsoft.com/windows/desktop/SecAuthZ/access-rights-for-access-token-objects">Access Rights for Access-Token Objects</a>.</para>
        /// <para><see href="https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocesstoken#parameters">Read more on docs.microsoft.com</see>.</para>
        /// </param>
        /// <param name="TokenHandle">A pointer to a handle that identifies the newly opened access token when the function returns.</param>
        /// <returns>
        /// <para>If the function succeeds, the return value is nonzero. If the function fails, the return value is zero. To get extended error information, call <a href="https://docs.microsoft.com/windows/desktop/api/errhandlingapi/nf-errhandlingapi-getlasterror">GetLastError</a>.</para>
        /// </returns>
        /// <remarks>
        /// <para>To get a handle to an elevated process from within a non-elevated process, both processes must be started from the same account. If the process being checked was started by a different account, the checking process needs to have the SE_DEBUG_NAME privilege enabled. See [Privilege Constants (Authorization)](/windows/win32/secauthz/privilege-constants) for more info. To close the access token handle returned through the <i>TokenHandle</i> parameter, call <a href="https://docs.microsoft.com/windows/desktop/api/handleapi/nf-handleapi-closehandle">CloseHandle</a>.</para>
        /// <para><see href="https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocesstoken#">Read more on docs.microsoft.com</see>.</para>
        /// </remarks>
        [LibraryImport("ADVAPI32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [SupportedOSPlatform("windows")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool OpenProcessToken(in SafeProcessHandle ProcessHandle, in TOKEN_ACCESS_MASK DesiredAccess, out SafeFileHandle TokenHandle);

        /// <summary>Contains values that specify the type of information being assigned to or retrieved from an access token.</summary>
        /// <remarks>
        /// <para><see href="https://learn.microsoft.com/windows/win32/api/winnt/ne-winnt-token_information_class">Learn more about this API from docs.microsoft.com</see>.</para>
        /// </remarks>
        internal enum TOKEN_INFORMATION_CLASS
        {
            /// <summary>
            /// <para>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_user">TOKEN_USER</a> structure that contains the user account of the token.</para>
            /// <para><see href="https://learn.microsoft.com/windows/win32/api/winnt/ne-winnt-token_information_class#members">Read more on docs.microsoft.com</see>.</para>
            /// </summary>
            TokenUser = 1,
            /// <summary>
            /// <para>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_groups">TOKEN_GROUPS</a> structure that contains the group accounts associated with the token.</para>
            /// <para><see href="https://learn.microsoft.com/windows/win32/api/winnt/ne-winnt-token_information_class#members">Read more on docs.microsoft.com</see>.</para>
            /// </summary>
            TokenGroups = 2,
            /// <summary>
            /// <para>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_privileges">TOKEN_PRIVILEGES</a> structure that contains the privileges of the token.</para>
            /// <para><see href="https://learn.microsoft.com/windows/win32/api/winnt/ne-winnt-token_information_class#members">Read more on docs.microsoft.com</see>.</para>
            /// </summary>
            TokenPrivileges = 3,
            /// <summary>
            /// <para>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_owner">TOKEN_OWNER</a> structure that contains the default owner <a href="https://docs.microsoft.com/windows/desktop/SecGloss/s-gly">security identifier</a> (SID) for newly created objects.</para>
            /// <para><see href="https://learn.microsoft.com/windows/win32/api/winnt/ne-winnt-token_information_class#members">Read more on docs.microsoft.com</see>.</para>
            /// </summary>
            TokenOwner = 4,
            /// <summary>
            /// <para>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_primary_group">TOKEN_PRIMARY_GROUP</a> structure that contains the default primary group SID for newly created objects.</para>
            /// <para><see href="https://learn.microsoft.com/windows/win32/api/winnt/ne-winnt-token_information_class#members">Read more on docs.microsoft.com</see>.</para>
            /// </summary>
            TokenPrimaryGroup = 5,
            /// <summary>
            /// <para>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_default_dacl">TOKEN_DEFAULT_DACL</a> structure that contains the default DACL for newly created objects.</para>
            /// <para><see href="https://learn.microsoft.com/windows/win32/api/winnt/ne-winnt-token_information_class#members">Read more on docs.microsoft.com</see>.</para>
            /// </summary>
            TokenDefaultDacl = 6,
            /// <summary>
            /// <para>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_source">TOKEN_SOURCE</a> structure that contains the source of the token. <b>TOKEN_QUERY_SOURCE</b> access is needed to retrieve this information.</para>
            /// <para><see href="https://learn.microsoft.com/windows/win32/api/winnt/ne-winnt-token_information_class#members">Read more on docs.microsoft.com</see>.</para>
            /// </summary>
            TokenSource = 7,
            /// <summary>
            /// <para>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ne-winnt-token_type">TOKEN_TYPE</a> value that indicates whether the token is a <a href="https://docs.microsoft.com/windows/desktop/SecGloss/p-gly">primary</a> or <a href="https://docs.microsoft.com/windows/desktop/SecGloss/i-gly">impersonation token</a>.</para>
            /// <para><see href="https://learn.microsoft.com/windows/win32/api/winnt/ne-winnt-token_information_class#members">Read more on docs.microsoft.com</see>.</para>
            /// </summary>
            TokenType = 8,
            /// <summary>
            /// <para>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ne-winnt-security_impersonation_level">SECURITY_IMPERSONATION_LEVEL</a> value that indicates the impersonation level of the token. If the access token is not an <a href="https://docs.microsoft.com/windows/desktop/SecGloss/i-gly">impersonation token</a>, the function fails.</para>
            /// <para><see href="https://learn.microsoft.com/windows/win32/api/winnt/ne-winnt-token_information_class#members">Read more on docs.microsoft.com</see>.</para>
            /// </summary>
            TokenImpersonationLevel = 9,
            /// <summary>
            /// <para>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_statistics">TOKEN_STATISTICS</a> structure that contains various token statistics.</para>
            /// <para><see href="https://learn.microsoft.com/windows/win32/api/winnt/ne-winnt-token_information_class#members">Read more on docs.microsoft.com</see>.</para>
            /// </summary>
            TokenStatistics = 10,
            /// <summary>
            /// <para>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_groups">TOKEN_GROUPS</a> structure that contains the list of restricting SIDs in a <a href="https://docs.microsoft.com/windows/desktop/SecAuthZ/restricted-tokens">restricted token</a>.</para>
            /// <para><see href="https://learn.microsoft.com/windows/win32/api/winnt/ne-winnt-token_information_class#members">Read more on docs.microsoft.com</see>.</para>
            /// </summary>
            TokenRestrictedSids = 11,
            /// <summary>
            /// <para>The buffer receives a <b>DWORD</b> value that indicates the Terminal Services session identifier that is associated with the token. If the token is associated with the terminal server client session, the session identifier is nonzero. <b>Windows Server 2003 and Windows XP:  </b>If the token is associated with the terminal server console session, the session identifier is zero. In a non-Terminal Services environment, the session identifier is zero. If <b>TokenSessionId</b> is set with <a href="https://docs.microsoft.com/windows/desktop/api/securitybaseapi/nf-securitybaseapi-settokeninformation">SetTokenInformation</a>, the application must have the <b>Act As Part Of the Operating System</b> privilege, and the application must be enabled to set the session ID in a token.</para>
            /// <para><see href="https://learn.microsoft.com/windows/win32/api/winnt/ne-winnt-token_information_class#members">Read more on docs.microsoft.com</see>.</para>
            /// </summary>
            TokenSessionId = 12,
            /// <summary>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_groups_and_privileges">TOKEN_GROUPS_AND_PRIVILEGES</a> structure that contains the user SID, the group accounts, the restricted SIDs, and the authentication ID associated with the token.</summary>
            TokenGroupsAndPrivileges = 13,
            /// <summary>Reserved.</summary>
            TokenSessionReference = 14,
            /// <summary>The buffer receives a <b>DWORD</b> value that is nonzero if the token includes the <b>SANDBOX_INERT</b> flag.</summary>
            TokenSandBoxInert = 15,
            /// <summary>Reserved.</summary>
            TokenAuditPolicy = 16,
            /// <summary>
            /// <para>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_origin">TOKEN_ORIGIN</a> value. If the token  resulted from a logon that used explicit credentials, such as passing a name, domain, and password to the  <a href="https://docs.microsoft.com/windows/desktop/api/winbase/nf-winbase-logonusera">LogonUser</a> function, then the <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_origin">TOKEN_ORIGIN</a> structure will contain the ID of the <a href="https://docs.microsoft.com/windows/desktop/SecGloss/l-gly">logon session</a> that created it. If the token resulted from  network authentication, such as a call to <a href="https://docs.microsoft.com/windows/desktop/api/sspi/nf-sspi-acceptsecuritycontext">AcceptSecurityContext</a>  or a call to <a href="https://docs.microsoft.com/windows/desktop/api/winbase/nf-winbase-logonusera">LogonUser</a> with <i>dwLogonType</i> set to <b>LOGON32_LOGON_NETWORK</b> or <b>LOGON32_LOGON_NETWORK_CLEARTEXT</b>, then this value will be zero.</para>
            /// <para><see href="https://learn.microsoft.com/windows/win32/api/winnt/ne-winnt-token_information_class#members">Read more on docs.microsoft.com</see>.</para>
            /// </summary>
            TokenOrigin = 17,
            /// <summary>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ne-winnt-token_elevation_type">TOKEN_ELEVATION_TYPE</a> value that specifies the elevation level of the token.</summary>
            TokenElevationType = 18,
            /// <summary>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_linked_token">TOKEN_LINKED_TOKEN</a> structure that contains a handle to another token that is linked to this token.</summary>
            TokenLinkedToken = 19,
            /// <summary>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_elevation">TOKEN_ELEVATION</a> structure that specifies whether the token is elevated.</summary>
            TokenElevation = 20,
            /// <summary>The buffer receives a <b>DWORD</b> value that is nonzero if the token has ever been filtered.</summary>
            TokenHasRestrictions = 21,
            /// <summary>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_access_information">TOKEN_ACCESS_INFORMATION</a> structure that specifies  security information contained in the token.</summary>
            TokenAccessInformation = 22,
            /// <summary>The buffer receives a <b>DWORD</b> value that is nonzero if  <a href="https://docs.microsoft.com/windows/desktop/SecGloss/v-gly">virtualization</a> is allowed for the token.</summary>
            TokenVirtualizationAllowed = 23,
            /// <summary>The buffer receives a <b>DWORD</b> value that is nonzero if  <a href="https://docs.microsoft.com/windows/desktop/SecGloss/v-gly">virtualization</a> is enabled for the token.</summary>
            TokenVirtualizationEnabled = 24,
            /// <summary>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_mandatory_label">TOKEN_MANDATORY_LABEL</a> structure that specifies the token's integrity level.</summary>
            TokenIntegrityLevel = 25,
            /// <summary>The buffer receives a <b>DWORD</b> value that is nonzero if  the token has the UIAccess flag set.</summary>
            TokenUIAccess = 26,
            /// <summary>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_mandatory_policy">TOKEN_MANDATORY_POLICY</a> structure that specifies the token's mandatory integrity policy.</summary>
            TokenMandatoryPolicy = 27,
            /// <summary>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_groups">TOKEN_GROUPS</a> structure that specifies the token's logon SID.</summary>
            TokenLogonSid = 28,
            /// <summary>The buffer receives a <b>DWORD</b> value that is nonzero if the token is an app container token. Any callers who check the <b>TokenIsAppContainer</b> and have it return 0 should also verify that the caller token is not an identify level impersonation token. If the current token is not an app container but is an identity level token, you should return <b>AccessDenied</b>.</summary>
            TokenIsAppContainer = 29,
            /// <summary>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_groups">TOKEN_GROUPS</a> structure that contains the capabilities associated with the token.</summary>
            TokenCapabilities = 30,
            /// <summary>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_appcontainer_information">TOKEN_APPCONTAINER_INFORMATION</a> structure that contains the AppContainerSid associated with the token. If the token is not associated with an app container, the <b>TokenAppContainer</b> member of the <b>TOKEN_APPCONTAINER_INFORMATION</b> structure points to <b>NULL</b>.</summary>
            TokenAppContainerSid = 31,
            /// <summary>The buffer receives a <b>DWORD</b> value that includes the   app container number for the token. For tokens that are not app container tokens, this value is zero.</summary>
            TokenAppContainerNumber = 32,
            /// <summary>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-claim_security_attributes_information">CLAIM_SECURITY_ATTRIBUTES_INFORMATION</a> structure that contains the user claims associated with the token.</summary>
            TokenUserClaimAttributes = 33,
            /// <summary>The buffer receives  a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-claim_security_attributes_information">CLAIM_SECURITY_ATTRIBUTES_INFORMATION</a> structure that contains the  device claims associated with the token.</summary>
            TokenDeviceClaimAttributes = 34,
            /// <summary>This value is reserved.</summary>
            TokenRestrictedUserClaimAttributes = 35,
            /// <summary>This value is reserved.</summary>
            TokenRestrictedDeviceClaimAttributes = 36,
            /// <summary>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_groups">TOKEN_GROUPS</a> structure that contains the device groups that are associated with the token.</summary>
            TokenDeviceGroups = 37,
            /// <summary>The buffer receives a <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_groups">TOKEN_GROUPS</a> structure that contains the restricted device groups that are associated with the token.</summary>
            TokenRestrictedDeviceGroups = 38,
            /// <summary>This value is reserved.</summary>
            TokenSecurityAttributes = 39,
            /// <summary>This value is reserved.</summary>
            TokenIsRestricted = 40,
            /// <summary></summary>
            TokenProcessTrustLevel = 41,
            /// <summary></summary>
            TokenPrivateNameSpace = 42,
            /// <summary></summary>
            TokenSingletonAttributes = 43,
            /// <summary></summary>
            TokenBnoIsolation = 44,
            /// <summary></summary>
            TokenChildProcessFlags = 45,
            /// <summary></summary>
            TokenIsLessPrivilegedAppContainer = 46,
            TokenIsSandboxed = 47,
            TokenIsAppSilo = 48,
            /// <summary>The maximum value for this enumeration.</summary>
            MaxTokenInfoClass = 49,
        }

        /// <summary>Indicates the elevation type of token being queried by the GetTokenInformation function or set by the SetTokenInformation function.</summary>
		/// <remarks>
		/// <para><see href="https://learn.microsoft.com/windows/win32/api/winnt/ne-winnt-token_elevation_type">Learn more about this API from docs.microsoft.com</see>.</para>
		/// </remarks>
        internal enum TOKEN_ELEVATION_TYPE
        {
            /// <summary>The token does not have a linked token.</summary>
            TokenElevationTypeDefault = 1,
            /// <summary>The token is an elevated token.</summary>
            TokenElevationTypeFull = 2,
            /// <summary>The token is a limited token.</summary>
            TokenElevationTypeLimited = 3,
        }

        /// <summary>Retrieves a specified type of information about an access token. The calling process must have appropriate access rights to obtain the information.</summary>
		/// <param name="TokenHandle">A handle to an access token from which information is retrieved. If <i>TokenInformationClass</i> specifies TokenSource, the handle must have TOKEN_QUERY_SOURCE access. For all other <i>TokenInformationClass</i> values, the handle must have TOKEN_QUERY access.</param>
		/// <param name="TokenInformationClass">
		/// <para>Specifies a value from the <a href="https://docs.microsoft.com/windows/desktop/api/winnt/ne-winnt-token_information_class">TOKEN_INFORMATION_CLASS</a> enumerated type to identify the type of information the function retrieves. Any callers who check the <b>TokenIsAppContainer</b> and have it return 0 should also verify that the caller token is not an identify level impersonation token. If the current token is not an app container but is an identity level token, you should return <b>AccessDenied</b>.</para>
		/// <para><see href="https://learn.microsoft.com/windows/win32/api/securitybaseapi/nf-securitybaseapi-gettokeninformation#parameters">Read more on docs.microsoft.com</see>.</para>
		/// </param>
		/// <param name="TokenInformation">A pointer to a buffer the function fills with the requested information. The structure put into this buffer depends upon the type of information specified by the <i>TokenInformationClass</i> parameter.</param>
		/// <param name="TokenInformationLength">Specifies the size, in bytes, of the buffer pointed to by the <i>TokenInformation</i> parameter. If <i>TokenInformation</i> is <b>NULL</b>, this parameter must be zero.</param>
		/// <param name="ReturnLength">
		/// <para>A pointer to a variable that receives the number of bytes needed for the buffer pointed to by the <i>TokenInformation</i> parameter. If this value is larger than the value specified in the <i>TokenInformationLength</i> parameter, the function fails and stores no data in the buffer. If the value of the <i>TokenInformationClass</i> parameter is TokenDefaultDacl and the token has no default DACL, the function sets the variable pointed to by <i>ReturnLength</i> to <c>sizeof(</code><a href="https://docs.microsoft.com/windows/desktop/api/winnt/ns-winnt-token_default_dacl">TOKEN_DEFAULT_DACL</a><code>)</c> and sets the <b>DefaultDacl</b> member of the <b>TOKEN_DEFAULT_DACL</b> structure to <b>NULL</b>.</para>
		/// <para><see href="https://learn.microsoft.com/windows/win32/api/securitybaseapi/nf-securitybaseapi-gettokeninformation#parameters">Read more on docs.microsoft.com</see>.</para>
		/// </param>
		/// <returns>
		/// <para>If the function succeeds, the return value is nonzero. If the function fails, the return value is zero. To get extended error information, call <a href="https://docs.microsoft.com/windows/desktop/api/errhandlingapi/nf-errhandlingapi-getlasterror">GetLastError</a>.</para>
		/// </returns>
		/// <remarks>
		/// <para><see href="https://learn.microsoft.com/windows/win32/api/securitybaseapi/nf-securitybaseapi-gettokeninformation">Learn more about this API from docs.microsoft.com</see>.</para>
		/// </remarks>
		[LibraryImport("ADVAPI32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [SupportedOSPlatform("windows")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetTokenInformation(in SafeFileHandle TokenHandle, in TOKEN_INFORMATION_CLASS TokenInformationClass, [Optional] void* TokenInformation, uint TokenInformationLength, out uint ReturnLength);

        /// <summary>Brings the thread that created the specified window into the foreground and activates the window.</summary>
		/// <param name="hWnd">
		/// <para>Type: <b>HWND</b> A handle to the window that should be activated and brought to the foreground.</para>
		/// <para><see href="https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setforegroundwindow#parameters">Read more on docs.microsoft.com</see>.</para>
		/// </param>
		/// <returns>
		/// <para>Type: <b>BOOL</b> If the window was brought to the foreground, the return value is nonzero. If the window was not brought to the foreground, the return value is zero.</para>
		/// </returns>
		/// <remarks>
		/// <para>The system restricts which processes can set the foreground window. A process can set the foreground window by calling **SetForegroundWindow** only if: - All of the following conditions are true: - The calling process belongs to a desktop application, not a UWP app or a Windows Store app designed for Windows 8 or 8.1. - The foreground process has not disabled calls to **SetForegroundWindow** by a previous call to the [**LockSetForegroundWindow**](nf-winuser-locksetforegroundwindow.md) function. - The foreground lock time-out has expired (see [**SPI_GETFOREGROUNDLOCKTIMEOUT** in **SystemParametersInfo**](nf-winuser-systemparametersinfoa.md#SPI_GETFOREGROUNDLOCKTIMEOUT)). - No menus are active. - Additionally, at least one of the following conditions is true: - The calling process is the foreground process. - The calling process was started by the foreground process. - There is currently no foreground window, and thus no foreground process. - The calling process received the last input event. - Either the foreground process or the calling process is being debugged. It is possible for a process to be denied the right to set the foreground window even if it meets these conditions. An application cannot force a window to the foreground while the user is working with another window. Instead, Windows flashes the taskbar button of the window to notify the user. A process that can set the foreground window can enable another process to set the foreground window by calling the [**AllowSetForegroundWindow**](nf-winuser-allowsetforegroundwindow.md) function. The process specified by the *dwProcessId* parameter to **AllowSetForegroundWindow** loses the ability to set the foreground window the next time that either the user generates input, unless the input is directed at that process, or the next time a process calls **AllowSetForegroundWindow**, unless the same process is specified as in the previous call to **AllowSetForegroundWindow**. The foreground process can disable calls to <b>SetForegroundWindow</b> by calling the [**LockSetForegroundWindow**](nf-winuser-locksetforegroundwindow.md) function.</para>
		/// <para><see href="https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setforegroundwindow#">Read more on docs.microsoft.com</see>.</para>
		/// </remarks>
		[LibraryImport("USER32.dll", SetLastError = false)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [SupportedOSPlatform("windows")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetForegroundWindow(nint hWnd);

        internal enum SYSTEM_METRICS_INDEX
        {
            SM_ARRANGE = 56,
            SM_CLEANBOOT = 67,
            SM_CMONITORS = 80,
            SM_CMOUSEBUTTONS = 43,
            SM_CONVERTIBLESLATEMODE = 8195,
            SM_CXBORDER = 5,
            SM_CXCURSOR = 13,
            SM_CXDLGFRAME = 7,
            SM_CXDOUBLECLK = 36,
            SM_CXDRAG = 68,
            SM_CXEDGE = 45,
            SM_CXFIXEDFRAME = 7,
            SM_CXFOCUSBORDER = 83,
            SM_CXFRAME = 32,
            SM_CXFULLSCREEN = 16,
            SM_CXHSCROLL = 21,
            SM_CXHTHUMB = 10,
            SM_CXICON = 11,
            SM_CXICONSPACING = 38,
            SM_CXMAXIMIZED = 61,
            SM_CXMAXTRACK = 59,
            SM_CXMENUCHECK = 71,
            SM_CXMENUSIZE = 54,
            SM_CXMIN = 28,
            SM_CXMINIMIZED = 57,
            SM_CXMINSPACING = 47,
            SM_CXMINTRACK = 34,
            SM_CXPADDEDBORDER = 92,
            SM_CXSCREEN = 0,
            SM_CXSIZE = 30,
            SM_CXSIZEFRAME = 32,
            SM_CXSMICON = 49,
            SM_CXSMSIZE = 52,
            SM_CXVIRTUALSCREEN = 78,
            SM_CXVSCROLL = 2,
            SM_CYBORDER = 6,
            SM_CYCAPTION = 4,
            SM_CYCURSOR = 14,
            SM_CYDLGFRAME = 8,
            SM_CYDOUBLECLK = 37,
            SM_CYDRAG = 69,
            SM_CYEDGE = 46,
            SM_CYFIXEDFRAME = 8,
            SM_CYFOCUSBORDER = 84,
            SM_CYFRAME = 33,
            SM_CYFULLSCREEN = 17,
            SM_CYHSCROLL = 3,
            SM_CYICON = 12,
            SM_CYICONSPACING = 39,
            SM_CYKANJIWINDOW = 18,
            SM_CYMAXIMIZED = 62,
            SM_CYMAXTRACK = 60,
            SM_CYMENU = 15,
            SM_CYMENUCHECK = 72,
            SM_CYMENUSIZE = 55,
            SM_CYMIN = 29,
            SM_CYMINIMIZED = 58,
            SM_CYMINSPACING = 48,
            SM_CYMINTRACK = 35,
            SM_CYSCREEN = 1,
            SM_CYSIZE = 31,
            SM_CYSIZEFRAME = 33,
            SM_CYSMCAPTION = 51,
            SM_CYSMICON = 50,
            SM_CYSMSIZE = 53,
            SM_CYVIRTUALSCREEN = 79,
            SM_CYVSCROLL = 20,
            SM_CYVTHUMB = 9,
            SM_DBCSENABLED = 42,
            SM_DEBUG = 22,
            SM_DIGITIZER = 94,
            SM_IMMENABLED = 82,
            SM_MAXIMUMTOUCHES = 95,
            SM_MEDIACENTER = 87,
            SM_MENUDROPALIGNMENT = 40,
            SM_MIDEASTENABLED = 74,
            SM_MOUSEPRESENT = 19,
            SM_MOUSEHORIZONTALWHEELPRESENT = 91,
            SM_MOUSEWHEELPRESENT = 75,
            SM_NETWORK = 63,
            SM_PENWINDOWS = 41,
            SM_REMOTECONTROL = 8193,
            SM_REMOTESESSION = 4096,
            SM_SAMEDISPLAYFORMAT = 81,
            SM_SECURE = 44,
            SM_SERVERR2 = 89,
            SM_SHOWSOUNDS = 70,
            SM_SHUTTINGDOWN = 8192,
            SM_SLOWMACHINE = 73,
            SM_STARTER = 88,
            SM_SWAPBUTTON = 23,
            SM_SYSTEMDOCKED = 8196,
            SM_TABLETPC = 86,
            SM_XVIRTUALSCREEN = 76,
            SM_YVIRTUALSCREEN = 77,
        }

        /// <summary>Retrieves the specified system metric or system configuration setting.</summary>
		/// <param name="nIndex">Type: <b>int</b></param>
		/// <returns>
		/// <para>Type: <b>int</b> If the function succeeds, the return value is the requested system metric or configuration setting. If the function fails, the return value is 0. <a href="https://docs.microsoft.com/windows/desktop/api/errhandlingapi/nf-errhandlingapi-getlasterror">GetLastError</a> does not provide extended error information.</para>
		/// </returns>
		/// <remarks>
		/// <para>System metrics can vary from display to display. <b>GetSystemMetrics</b>(SM_CMONITORS) counts only visible display monitors. This is different from <a href="https://docs.microsoft.com/windows/desktop/api/winuser/nf-winuser-enumdisplaymonitors">EnumDisplayMonitors</a>, which enumerates both visible display monitors and invisible  pseudo-monitors that are associated with mirroring drivers. An invisible pseudo-monitor is associated with a pseudo-device used to mirror application drawing for remoting or other purposes. The SM_ARRANGE setting specifies how the system arranges minimized windows, and consists of a starting position and a direction. The starting position can be one of the following values.</para>
		/// <para></para>
		/// <para>This doc was truncated.</para>
		/// <para><see href="https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getsystemmetrics#">Read more on docs.microsoft.com</see>.</para>
		/// </remarks>
		[LibraryImport("USER32.dll", SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [SupportedOSPlatform("windows5.0")]
        internal static partial int GetSystemMetrics(in SYSTEM_METRICS_INDEX nIndex);
    }
}
