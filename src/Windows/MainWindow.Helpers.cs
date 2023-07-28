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
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Enums;
using System.Threading.Tasks;

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
                screenHeight = PInvoke.GetSystemMetrics(global::Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN),
                width = screenWidth / 2,
                height = screenHeight / 2;

            MessageBoxManager.GetMessageBoxStandard(new MsBox.Avalonia.Dto.MessageBoxStandardParams()
            {
                ButtonDefinitions = ButtonEnum.Ok,
                CanResize = false,
                ContentHeader = ex.Message,
                ContentTitle = "Error",
                ContentMessage = ex.StackTrace,
                EnterDefaultButton = ClickEnum.Ok,
                EscDefaultButton = ClickEnum.Ok,
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

        private async ValueTask<ButtonResult> ShowYesNoMsgBox(string content, string title, Icon icon = MsBox.Avalonia.Enums.Icon.Question, Avalonia.Controls.SizeToContent sizeToContent = Avalonia.Controls.SizeToContent.WidthAndHeight)
        {
            ArgumentException.ThrowIfNullOrEmpty(content);

          

            var msgboxparams = new MsBox.Avalonia.Dto.MessageBoxStandardParams()
            {
                ButtonDefinitions = ButtonEnum.YesNo,
                CanResize = false,
                ContentTitle = title,
                ContentMessage = content,
                EnterDefaultButton = ClickEnum.Yes,
                EscDefaultButton = ClickEnum.No,
                Icon = icon,
                Markdown = false,
                ShowInCenter = true,
                SystemDecorations = Avalonia.Controls.SystemDecorations.Full
            };

            if (sizeToContent == Avalonia.Controls.SizeToContent.Manual)
            {
                int screenWidth = PInvoke.GetSystemMetrics(global::Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN),
                    screenHeight = PInvoke.GetSystemMetrics(global::Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);
                msgboxparams.MinWidth = 300;
                msgboxparams.MinHeight = 200;
                msgboxparams.Width = screenWidth / 2;
                msgboxparams.Height = screenHeight / 2;
            }
            else
            {
                msgboxparams.SizeToContent = sizeToContent;
            }

            return await MessageBoxManager.GetMessageBoxStandard(msgboxparams).ShowWindowDialogAsync(this);
        }
    }
}
