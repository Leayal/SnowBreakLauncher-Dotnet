/*
 * This file contains mainly helper methods or functions
*/

using Avalonia;
using Leayal.SnowBreakLauncher.Classes;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using MsBox.Avalonia;
using Windows.Win32;
using MsBox.Avalonia.Enums;
using System.Threading.Tasks;
using Avalonia.Controls;
using Leayal.SnowBreakLauncher.Snowbreak;

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
        private static bool IsGameExisted(string installDirectory) => System.IO.File.Exists(GameManager.GetGameExecutablePath(installDirectory));

        /// <summary></summary>
        /// <param name="path"></param>
        /// <returns>
        /// <para><see langword="true"/> if the path is a folder containing 'manifest.json' file.</para>
        /// <para><see langword="false"/> if the path is a folder containing 'game.exe' file.</para>
        /// <para><see langword="null"/> if neither cases above matched.</para>
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool? IsClientOrManifest(string path)
        {
            var span_filename = path.AsSpan();
            if (MemoryExtensions.Equals(System.IO.Path.GetFileName(span_filename), "manifest.json", StringComparison.OrdinalIgnoreCase)) return true;
            else if (MemoryExtensions.Equals(System.IO.Path.GetFileName(span_filename), "game.exe", StringComparison.OrdinalIgnoreCase)) return false;
            else return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AdjustManualSize(MsBox.Avalonia.Dto.MessageBoxStandardParams msgboxparams, SizeToContent sizeToContent)
        {
            if (sizeToContent == SizeToContent.Manual)
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
        }

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
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.Full
            }).ShowWindowDialogAsync(this);
        }

        private async ValueTask<ButtonResult> ShowYesNoCancelMsgBox(string content, string title, Icon icon = MsBox.Avalonia.Enums.Icon.Question, SizeToContent sizeToContent = SizeToContent.WidthAndHeight)
        {
            ArgumentException.ThrowIfNullOrEmpty(content);
            ArgumentException.ThrowIfNullOrEmpty(title);

            var msgboxparams = new MsBox.Avalonia.Dto.MessageBoxStandardParams()
            {
                ButtonDefinitions = ButtonEnum.YesNoCancel,
                CanResize = false,
                ContentTitle = title,
                ContentMessage = content,
                EscDefaultButton = ClickEnum.Cancel,
                Icon = icon,
                Markdown = false,
                ShowInCenter = true,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.Full
            };

            AdjustManualSize(msgboxparams, sizeToContent);

            return await MessageBoxManager.GetMessageBoxStandard(msgboxparams).ShowWindowDialogAsync(this);
        }

        private async ValueTask ShowInfoMsgBox(string content, string title, Icon icon = MsBox.Avalonia.Enums.Icon.Info, SizeToContent sizeToContent = SizeToContent.WidthAndHeight)
        {
            ArgumentException.ThrowIfNullOrEmpty(content);
            ArgumentException.ThrowIfNullOrEmpty(title);

            var msgboxparams = new MsBox.Avalonia.Dto.MessageBoxStandardParams()
            {
                ButtonDefinitions = ButtonEnum.Ok,
                CanResize = false,
                ContentTitle = title,
                ContentMessage = content,
                EnterDefaultButton = ClickEnum.Ok,
                EscDefaultButton = ClickEnum.Ok,
                Icon = icon,
                Markdown = false,
                ShowInCenter = true,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.Full
            };

            AdjustManualSize(msgboxparams, sizeToContent);

            await MessageBoxManager.GetMessageBoxStandard(msgboxparams).ShowWindowDialogAsync(this);
        }

        private async ValueTask<ButtonResult> ShowYesNoMsgBox(string content, string title, Icon icon = MsBox.Avalonia.Enums.Icon.Question, SizeToContent sizeToContent = SizeToContent.WidthAndHeight)
        {
            ArgumentException.ThrowIfNullOrEmpty(content);
            ArgumentException.ThrowIfNullOrEmpty(title);

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
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.Full
            };

            AdjustManualSize(msgboxparams, sizeToContent);

            return await MessageBoxManager.GetMessageBoxStandard(msgboxparams).ShowWindowDialogAsync(this);
        }
    }
}
