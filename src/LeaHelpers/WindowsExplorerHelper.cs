using System;
using System.IO;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Threading;
using Shell32 = global::Windows.Win32.UI.Shell;
using System.Reflection;
using Leayal.Shared;

namespace Leayal.Shared.Windows
{
    /// <summary>Convenient methods to interact with Files Explorer of Windows OS.</summary>
    public static class WindowsExplorerHelper
    {
        private static readonly string ExplorerExe = Path.GetFullPath("explorer.exe", Environment.GetFolderPath(Environment.SpecialFolder.Windows));

        private static readonly WeakLazy<IShellDispatch2> lazy_IShellDispatch2 = new WeakLazy<IShellDispatch2>(() =>
        {
            var t_ShellApp = Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"));
            if (t_ShellApp == null) throw new PlatformNotSupportedException();

            // This is quite expensive. As it will enumerate all the windows.
            var o_shell = Activator.CreateInstance(t_ShellApp);
            if (o_shell == null) throw new PlatformNotSupportedException();

            var shellWindows = (IShellWindows)o_shell;
            // Get the desktop window
            object loc = CSIDL_Desktop;
            object unused = new object();
            int hwnd;
            var o_serviceProvider = shellWindows.FindWindowSW(ref loc, ref unused, SWC_DESKTOP, out hwnd, SWFO_NEEDDISPATCH);
            if (o_serviceProvider == null) throw new PlatformNotSupportedException();

            var serviceProvider = (IServiceProvider)o_serviceProvider;

            // Get the shell browser
            var serviceGuid = SID_STopLevelBrowser;
            var interfaceGuid = typeof(IShellBrowser).GUID;
            var shellBrowser = (IShellBrowser)serviceProvider.QueryService(ref serviceGuid, ref interfaceGuid);

            shellBrowser.QueryActiveShellView(out var iShelView);

            // Get the shell dispatch
            var dispatch = typeof(IDispatch).GUID;
            var folderView = (IShellFolderViewDual)iShelView.GetItemObject(SVGIO_BACKGROUND, ref dispatch);
            return (IShellDispatch2)folderView.Application;
        });

        /// <summary>Launch the process by having Windows Explorer do the process creation.</summary>
        /// <returns><see langword="true"/> if the message (to tell Windows Explorer to launch process) is issued successfully. Otherwise, <see langword="false"/>.</returns>
        /// <remarks>Windows Explorer is typically running unelevated.</remarks>
        public static bool ShellExecuteProcessUnElevated(string process)
            => ShellExecuteProcessUnElevated(process, null);

        /// <summary>Launch the process by having Windows Explorer do the process creation.</summary>
        /// <returns><see langword="true"/> if the message (to tell Windows Explorer to launch process) is issued successfully. Otherwise, <see langword="false"/>.</returns>
        /// <remarks>Windows Explorer is typically running unelevated.</remarks>
        public static bool ShellExecuteProcessUnElevated(string process, string? args)
            => ShellExecuteProcessUnElevated(process, args, null);

        /// <summary>Launch the process by having Windows Explorer do the process creation.</summary>
        /// <returns><see langword="true"/> if the message (to tell Windows Explorer to launch process) is issued successfully. Otherwise, <see langword="false"/>.</returns>
        /// <remarks>Windows Explorer is typically running unelevated.</remarks>
        public static bool ShellExecuteProcessUnElevated(string process, string? args, string? currentDirectory)
        {
            if (string.IsNullOrWhiteSpace(process)) throw new ArgumentNullException(nameof(process));

            try
            {
                var shellDispatch = lazy_IShellDispatch2.Value;
                lock (shellDispatch)
                {
                    try
                    {
                        // Use the dispatch to send message to the original explorer.exe (which is unelevated) to launch the process for us
                        shellDispatch.ShellExecute(process, args, currentDirectory, string.Empty, SW_SHOWNORMAL);
                    }
                    finally
                    {

                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Interop definitions
        /// </summary>
        private const int CSIDL_Desktop = 0;
        private const int SWC_DESKTOP = 8;
        private const int SWFO_NEEDDISPATCH = 1;
        private const int SW_SHOWNORMAL = 1;
        private const int SVGIO_BACKGROUND = 0;
        private readonly static Guid SID_STopLevelBrowser = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837");

        /*
        [ComImport]
        [Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39")]
        [ClassInterface(ClassInterfaceType.None)]
        private class CShellWindows
        {
        }
        */

        [ComImport]
        [Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85")]
        [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface IShellWindows
        {
            [return: MarshalAs(UnmanagedType.IDispatch)]
            object FindWindowSW([MarshalAs(UnmanagedType.Struct)] ref object pvarloc, [MarshalAs(UnmanagedType.Struct)] ref object pvarlocRoot, int swClass, out int pHWND, int swfwOptions);
        }

        [ComImport]
        [Guid("6d5140c1-7436-11ce-8034-00aa006009fa")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IServiceProvider
        {
            [return: MarshalAs(UnmanagedType.Interface)]
            object QueryService(ref Guid guidService, ref Guid riid);
        }

        [ComImport]
        [Guid("000214E2-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellBrowser
        {
            /// <summary>Get the handle of the shellview implemented.</summary>
            void GetWindow(out IntPtr windowHandle);

            /// <summary>ContextSensitiveHelp.</summary>
            void ContextSensitiveHelp(bool fEnterMode);
            void InsertMenusSB(IntPtr IntPtrShared, IntPtr lpMenuWidths);
            void SetMenuSB(IntPtr IntPtrShared, IntPtr holemenuRes, IntPtr IntPtrActiveObject);
            void RemoveMenusSB(IntPtr IntPtrShared);
            void SetStatusTextSB(IntPtr pszStatusText);
            void EnableModelessSB(bool fEnable);
            void TranslateAcceleratorSB(IntPtr pmsg, UInt16 wID);
            void BrowseObject(IntPtr pidl, UInt32 wFlags);
            void GetViewStateStream(UInt32 grfMode, IntPtr ppStrm);
            void GetControlWindow(UInt32 id, out IntPtr lpIntPtr);
            void SendControlMsg(UInt32 id, UInt32 uMsg, UInt32 wParam, UInt32 lParam, IntPtr pret);
            void QueryActiveShellView(out IShellView ppshv);
            void OnViewWindowActive(IShellView ppshv);
            void SetToolbarItems(IntPtr lpButtons, UInt32 nButtons, UInt32 uFlags);
        }

        [ComImport]
        [Guid("000214E3-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellView
        {
            /// <summary>Get the handle of the shellview implemented.</summary>
            void GetWindow(out IntPtr windowHandle);

            /// <summary>ContextSensitiveHelp.</summary>
            void ContextSensitiveHelp(bool fEnterMode);

            /// <summary>Translates accelerator key strokes when a namespace extension's view has the focus.</summary>
            [PreserveSig]
            long TranslateAcceleratorA(IntPtr message);

            /// <summary>Enables or disables modeless dialog boxes.</summary>
            /// <remarks>This method is currently not implemented.</remarks>
            void EnableModeless(bool enable);

            /// <summary>Called when the activation state of the view window is changed by an event that is not caused by the Shell view itself. For example, if the TAB key is pressed when the tree has the focus, the view should be given the focus.</summary>
            void UIActivate(uint activtionState);

            /// <summary>Refreshes the view's contents in response to user input.</summary>
            /// <remarks>Explorer calls this method when the F5 key is pressed on an already open view.</remarks>
            void Refresh();

            /// <summary>Creates a view window.</summary>
            /// <remarks>This can be either the right pane of Explorer or the client window of a folder window.</remarks>
            void CreateViewWindow([In, MarshalAs(UnmanagedType.Interface)] IShellView? previousShellView, [In] ref Shell32.FOLDERSETTINGS folderSetting, [In] IShellBrowser shellBrowser, [Out] out global::Windows.Win32.Foundation.RECT bounds, [Out] out IntPtr handleOfCreatedWindow);

            /// <summary>Destroys the view window.</summary>
            void DestroyViewWindow();

            /// <summary>Retrieves the current folder settings.</summary>
            void GetCurrentInfo(ref Shell32.FOLDERSETTINGS pfs);

            /// <summary>Allows the view to add pages to the Options property sheet from the View menu.</summary>
            void AddPropertySheetPages([In, MarshalAs(UnmanagedType.U4)] uint reserved, [In] ref IntPtr functionPointer, [In] IntPtr lparam);

            /// <summary>Saves the Shell's view settings so the current state can be restored during a subsequent browsing session.</summary>
            void SaveViewState();

            /// <summary>Changes the selection state of one or more items within the Shell view window.</summary>
            void SelectItem(IntPtr pidlItem, [MarshalAs(UnmanagedType.U4)] uint flags);

            /// <summary>Retrieves an interface that refers to data presented in the view.</summary>
            [return: MarshalAs(UnmanagedType.Interface)]
            object GetItemObject(UInt32 aspectOfView, ref Guid riid);
        }

        [ComImport]
        [Guid("00020400-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface IDispatch
        {
        }

        [ComImport]
        [Guid("E7A1AF80-4D96-11CF-960C-0080C7F4EE85")]
        [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface IShellFolderViewDual
        {
            object Application { [return: MarshalAs(UnmanagedType.IDispatch)] get; }
        }

        [ComImport]
        [Guid("A4C6892C-3BA9-11D2-9DEA-00C04FB16162")]
        [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
        private interface IShellDispatch2
        {
            // void ShellExecute([MarshalAs(UnmanagedType.BStr)] string File, [MarshalAs(UnmanagedType.Struct)] object vArgs, [MarshalAs(UnmanagedType.Struct)] object vDir, [MarshalAs(UnmanagedType.Struct)] object vOperation, [MarshalAs(UnmanagedType.Struct)] object vShow);
            [DispId(0x60020000)]
            object Application { [return: MarshalAs(UnmanagedType.IDispatch)][MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020000)] get; }
            [DispId(0x60020001)]
            object Parent { [return: MarshalAs(UnmanagedType.IDispatch)][MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020001)] get; }
            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020002)]
            Folder NameSpace([In, MarshalAs(UnmanagedType.Struct)] object vDir);
            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020003)]
            Folder BrowseForFolder([In] int Hwnd, [In, MarshalAs(UnmanagedType.BStr)] string Title, [In] int Options, [In, Optional, MarshalAs(UnmanagedType.Struct)] object RootFolder);
            [return: MarshalAs(UnmanagedType.IDispatch)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020004)]
            object Windows();
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020005)]
            void Open([In, MarshalAs(UnmanagedType.Struct)] object vDir);
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020006)]
            void Explore([In, MarshalAs(UnmanagedType.Struct)] object vDir);
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020007)]
            void MinimizeAll();
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020008)]
            void UndoMinimizeALL();
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020009)]
            void FileRun();
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x6002000a)]
            void CascadeWindows();
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x6002000b)]
            void TileVertically();
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x6002000c)]
            void TileHorizontally();
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x6002000d)]
            void ShutdownWindows();
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x6002000e)]
            void Suspend();
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x6002000f)]
            void EjectPC();
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020010)]
            void SetTime();
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020011)]
            void TrayProperties();
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020012)]
            void Help();
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020013)]
            void FindFiles();
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020014)]
            void FindComputer();
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020015)]
            void RefreshMenu();
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020016)]
            void ControlPanelItem([In, MarshalAs(UnmanagedType.BStr)] string bstrDir);
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60030000)]
            int IsRestricted([In, MarshalAs(UnmanagedType.BStr)] string Group, [In, MarshalAs(UnmanagedType.BStr)] string Restriction);
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60030001)]
            void ShellExecute([In, MarshalAs(UnmanagedType.BStr)] string File, [In, Optional, MarshalAs(UnmanagedType.Struct)] object? vArgs, [In, Optional, MarshalAs(UnmanagedType.Struct)] object? vDir, [In, Optional, MarshalAs(UnmanagedType.Struct)] object? vOperation, [In, Optional, MarshalAs(UnmanagedType.Struct)] object? vShow);
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60030002)]
            void FindPrinter([In, Optional, MarshalAs(UnmanagedType.BStr)] string Name, [In, Optional, MarshalAs(UnmanagedType.BStr)] string location, [In, Optional, MarshalAs(UnmanagedType.BStr)] string model);
            [return: MarshalAs(UnmanagedType.Struct)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60030003)]
            object GetSystemInformation([In, MarshalAs(UnmanagedType.BStr)] string Name);
            [return: MarshalAs(UnmanagedType.Struct)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60030004)]
            object ServiceStart([In, MarshalAs(UnmanagedType.BStr)] string ServiceName, [In, MarshalAs(UnmanagedType.Struct)] object Persistent);
            [return: MarshalAs(UnmanagedType.Struct)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60030005)]
            object ServiceStop([In, MarshalAs(UnmanagedType.BStr)] string ServiceName, [In, MarshalAs(UnmanagedType.Struct)] object Persistent);
            [return: MarshalAs(UnmanagedType.Struct)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60030006)]
            object IsServiceRunning([In, MarshalAs(UnmanagedType.BStr)] string ServiceName);
            [return: MarshalAs(UnmanagedType.Struct)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60030007)]
            object CanStartStopService([In, MarshalAs(UnmanagedType.BStr)] string ServiceName);
            [return: MarshalAs(UnmanagedType.Struct)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60030008)]
            object ShowBrowserBar([In, MarshalAs(UnmanagedType.BStr)] string bstrClsid, [In, MarshalAs(UnmanagedType.Struct)] object bShow);
        }

        [ComImport, Guid("BBCBDE60-C3FF-11CE-8350-444553540000"), TypeLibType((short)0x1040), DefaultMember("Title")]
        interface Folder
        {
            [DispId(0)]
            string Title { [return: MarshalAs(UnmanagedType.BStr)][MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0)] get; }
            [DispId(0x60020001)]
            object Application { [return: MarshalAs(UnmanagedType.IDispatch)][MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020001)] get; }
            [DispId(0x60020002)]
            object Parent { [return: MarshalAs(UnmanagedType.IDispatch)][MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020002)] get; }
            [DispId(0x60020003)]
            Folder ParentFolder { [return: MarshalAs(UnmanagedType.Interface)][MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020003)] get; }
            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020004)]
            FolderItems Items();
            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020005)]
            FolderItem ParseName([In, MarshalAs(UnmanagedType.BStr)] string bName);
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020006)]
            void NewFolder([In, MarshalAs(UnmanagedType.BStr)] string bName, [In, Optional, MarshalAs(UnmanagedType.Struct)] object vOptions);
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020007)]
            void MoveHere([In, MarshalAs(UnmanagedType.Struct)] object vItem, [In, Optional, MarshalAs(UnmanagedType.Struct)] object vOptions);
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020008)]
            void CopyHere([In, MarshalAs(UnmanagedType.Struct)] object vItem, [In, Optional, MarshalAs(UnmanagedType.Struct)] object vOptions);
            [return: MarshalAs(UnmanagedType.BStr)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020009)]
            string GetDetailsOf([In, MarshalAs(UnmanagedType.Struct)] object vItem, [In] int iColumn);
        }

        [ComImport, Guid("744129E0-CBE5-11CE-8350-444553540000"), TypeLibType((short)0x1040)]
        interface FolderItems
        {
            [DispId(0x60020000)]
            int Count { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020000)] get; }
            [DispId(0x60020001)]
            object Application { [return: MarshalAs(UnmanagedType.IDispatch)][MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020001)] get; }
            [DispId(0x60020002)]
            object Parent { [return: MarshalAs(UnmanagedType.IDispatch)][MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020002)] get; }
            [return: MarshalAs(UnmanagedType.Interface)]
            [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020003)]
            FolderItem Item([In, Optional, MarshalAs(UnmanagedType.Struct)] object index);
        }

        [ComImport, Guid("FAC32C80-CBE4-11CE-8350-444553540000"), TypeLibType((short)0x1040), DefaultMember("Name")]
        interface FolderItem
        {
            [DispId(0x60020000)]
            object Application { [return: MarshalAs(UnmanagedType.IDispatch)][MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020000)] get; }
            [DispId(0x60020001)]
            object Parent { [return: MarshalAs(UnmanagedType.IDispatch)][MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020001)] get; }
            [DispId(0)]
            string Name { [return: MarshalAs(UnmanagedType.BStr)][MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0)] get; [param: In, MarshalAs(UnmanagedType.BStr)][MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0)] set; }
            [DispId(0x60020004)]
            string Path { [return: MarshalAs(UnmanagedType.BStr)][MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020004)] get; }
            [DispId(0x60020005)]
            object GetLink { [return: MarshalAs(UnmanagedType.IDispatch)][MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020005)] get; }
            [DispId(0x60020006)]
            object GetFolder { [return: MarshalAs(UnmanagedType.IDispatch)][MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020006)] get; }
            [DispId(0x60020007)]
            bool IsLink { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020007)] get; }
            [DispId(0x60020008)]
            bool IsFolder { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020008)] get; }
            [DispId(0x60020009)]
            bool IsFileSystem { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x60020009)] get; }
            [DispId(0x6002000a)]
            bool IsBrowsable { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x6002000a)] get; }
            [DispId(0x6002000b)]
            DateTime ModifyDate { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x6002000b)] get; [param: In][MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x6002000b)] set; }
            [DispId(0x6002000d)]
            int Size { [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x6002000d)] get; }
            [DispId(0x6002000e)]
            string Type { [return: MarshalAs(UnmanagedType.BStr)][MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime), DispId(0x6002000e)] get; }
        }

        /// <summary>Open the parent directory of the path and select the file/folder in the file explorer.</summary>
        /// <param name="path">The path to the selected folder.</param>
        /// <param name="waiting">Allow making runtime wait for this call to be finished before continue.</param>
        /// <exception cref="DirectoryNotFoundException">The path does not exist or is a file.</exception>
        public static void SelectPathInExplorer(string path, bool waiting = false)
        {
            if (!FileSystem.PathExists(path)) throw new DirectoryNotFoundException();

            if (ShellExecuteProcessUnElevated(ExplorerExe, $"/select,\"{path}\"")) return;

            using (var proc = Process.Start(ExplorerExe, $"/select,\"{path}\""))
            {
                if (waiting) proc.WaitForExit(500);
            }
        }

        /// <summary>Open the folder in the file explorer.</summary>
        /// <param name="directory">The path to the selected folder.</param>
        /// <param name="waiting">Allow making runtime wait for this call to be finished before continue.</param>
        /// <exception cref="DirectoryNotFoundException">The path does not exist or is a file.</exception>
        public static void ShowPathInExplorer(string directory, bool waiting = false)
        {
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException();

            if (ShellExecuteProcessUnElevated(directory, null)) return;

            using (var proc = Process.Start(ExplorerExe, $"/root,\"{directory}\""))
            {
                if (waiting) proc.WaitForExit(500);
            }
        }

        /// <summary>Open the given URL with user's default browser.</summary>
        /// <param name="url">The URL for the default browser to open.</param>
        /// <param name="waiting">Allow making runtime wait for this call to be finished before continue.</param>
        public static void OpenUrlWithDefaultBrowser(Uri url, bool waiting = false)
            => OpenUrlWithDefaultBrowser(url.IsAbsoluteUri ? url.AbsoluteUri : url.ToString(), waiting);

        /// <summary>Open the given URL with user's default browser.</summary>
        /// <param name="url">The URL for the default browser to open.</param>
        /// <param name="waiting">Allow making runtime wait for this call to be finished before continue.</param>
        public static void OpenUrlWithDefaultBrowser(string url, bool waiting = false)
        {
            if (ShellExecuteProcessUnElevated(url, null)) return;

            using (var proc = Process.Start(ExplorerExe, $"\"{url}\""))
            {
                if (waiting) proc.WaitForExit(500);
            }
        }
    }
}
