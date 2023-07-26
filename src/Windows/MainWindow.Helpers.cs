/*
 * This file contains mainly helper methods or functions
*/

using Avalonia;
using Leayal.SnowBreakLauncher.Classes;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.NetworkInformation;
using System.Net;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MsBox.Avalonia;
using Windows.Win32;

namespace Leayal.SnowBreakLauncher.Windows
{
    public partial class MainWindow
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryGetAppConfig([NotNullWhen(true)] out OfficialJsonConfiguration? config)
        {
            if (Application.Current is App app)
            {
                config = app.LauncherConfig;
                return true;
            }
            config = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetGameExecutablePath(string installDirectory) => System.IO.Path.Join(installDirectory, "game", "Game", "Binaries", "Win64", "Game.exe");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsGameExisted(string installDirectory) => (!string.IsNullOrEmpty(installDirectory) && System.IO.File.Exists(GetGameExecutablePath(installDirectory)));

        private void ShowErrorMsgBox(Exception ex)
        {
            ArgumentNullException.ThrowIfNull(ex);

            int screenWidth= PInvoke.GetSystemMetrics(global::Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN),
                screenHeight = PInvoke.GetSystemMetrics(global::Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN),
                width = screenWidth / 2,
                height = screenHeight / 2;

            MessageBoxManager.GetMessageBoxStandard(new MsBox.Avalonia.Dto.MessageBoxStandardParams()
            {
                ButtonDefinitions = MsBox.Avalonia.Enums.ButtonEnum.Ok,
                CanResize = false,
                ContentHeader = ex.Message,
                ContentTitle = "Error",
                ContentMessage = ex.StackTrace,
                EnterDefaultButton = MsBox.Avalonia.Enums.ClickEnum.Ok,
                EscDefaultButton = MsBox.Avalonia.Enums.ClickEnum.Ok,
                Icon = MsBox.Avalonia.Enums.Icon.Error,
                Markdown = false,
                ShowInCenter = true,
                MinWidth = 300,
                MinHeight = 200,
                Width = width,
                Height = height,
                SystemDecorations = Avalonia.Controls.SystemDecorations.Full
            }).ShowWindowDialogAsync(this);
        }
    }
}
