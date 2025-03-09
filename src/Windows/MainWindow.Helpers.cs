/*
 * This file contains mainly helper methods or functions
*/

using Avalonia;
using Leayal.SnowBreakLauncher.Classes;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System.Threading.Tasks;
using Avalonia.Controls;
using Leayal.SnowBreakLauncher.Snowbreak;
using Avalonia.Platform;

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
        private static void AdjustManualSize(Window parent, MsBox.Avalonia.Dto.MessageBoxStandardParams msgboxparams, SizeToContent sizeToContent)
        {
            if (sizeToContent == SizeToContent.Manual)
            {
                int screenWidth = 0, screenHeight = 0;
                if (OperatingSystem.IsWindowsVersionAtLeast(5))
                {
                    screenWidth = global::Windows.Win32.PInvoke.GetSystemMetrics(global::Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
                    screenHeight = global::Windows.Win32.PInvoke.GetSystemMetrics(global::Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);
                }
                else if (parent.Screens.ScreenFromVisual(parent) is Screen screenArea)
                {
                    screenWidth = screenArea.WorkingArea.Width;
                    screenHeight = screenArea.WorkingArea.Width;
                }

                msgboxparams.MinWidth = 300;
                msgboxparams.MinHeight = 200;

                if (screenWidth != 0 && screenHeight != 0)
                {
                    msgboxparams.Width = screenWidth / 2;
                    msgboxparams.Height = screenHeight / 2;
                }
            }
            else
            {
                msgboxparams.SizeToContent = sizeToContent;
            }
        }

        internal Task ShowErrorMsgBox(Exception ex) => ShowErrorMsgBox(this, ex);

        internal static Task ShowErrorMsgBox(Window parent, Exception ex)
        {
            ArgumentNullException.ThrowIfNull(ex);

            var msgBoxParams = new MsBox.Avalonia.Dto.MessageBoxStandardParams()
            {
                ButtonDefinitions = ButtonEnum.Ok,
                CanResize = false,
                ContentHeader = ex.Message,
                ContentTitle = "Error",
                EnterDefaultButton = ClickEnum.Ok,
                EscDefaultButton = ClickEnum.Ok,
                Icon = MsBox.Avalonia.Enums.Icon.Error,
                Markdown = false,
                ShowInCenter = true,
                MinWidth = 300,
                MinHeight = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.Full
            };
            var stacktrace = ex.StackTrace;
            msgBoxParams.ContentMessage = string.IsNullOrEmpty(stacktrace) ? ex.Message : stacktrace;

            AdjustManualSize(parent, msgBoxParams, msgBoxParams.SizeToContent);

            return MessageBoxManager.GetMessageBoxStandard(msgBoxParams).ShowWindowDialogAsync(parent);
        }

        private Task ShowDialog_LetUserKnowGameDirectoryIsNotSetForThisFunction()
            => this.ShowInfoMsgBox("It seems you haven't installed the game yet or the launcher doesn't know where it is." + Environment.NewLine
                    + "Please install or select the game data location before performing this action.", "Information");

        private Task<ButtonResult> ShowYesNoCancelMsgBox(string content, string title, Icon icon = MsBox.Avalonia.Enums.Icon.Question, SizeToContent sizeToContent = SizeToContent.WidthAndHeight)
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

            AdjustManualSize(this, msgboxparams, sizeToContent);

            return MessageBoxManager.GetMessageBoxStandard(msgboxparams).ShowWindowDialogAsync(this);
        }

        private Task ShowInfoMsgBox(string content, string title, Icon icon = MsBox.Avalonia.Enums.Icon.Info, SizeToContent sizeToContent = SizeToContent.WidthAndHeight)
            => ShowInfoMsgBox(this, content, title, icon, sizeToContent);

        internal static Task ShowInfoMsgBox(Window parent, string content, string title, Icon icon = MsBox.Avalonia.Enums.Icon.Info, SizeToContent sizeToContent = SizeToContent.WidthAndHeight)
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

            AdjustManualSize(parent, msgboxparams, sizeToContent);

            return MessageBoxManager.GetMessageBoxStandard(msgboxparams).ShowWindowDialogAsync(parent);
        }

        private Task<ButtonResult> ShowYesNoMsgBox(string content, string title, Icon icon = MsBox.Avalonia.Enums.Icon.Question, SizeToContent sizeToContent = SizeToContent.WidthAndHeight)
            => ShowYesNoMsgBox(this, content, title, icon, sizeToContent);

        internal static Task<ButtonResult> ShowYesNoMsgBox(Window parent, string content, string title, Icon icon = MsBox.Avalonia.Enums.Icon.Question, SizeToContent sizeToContent = SizeToContent.WidthAndHeight)
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

            AdjustManualSize(parent, msgboxparams, sizeToContent);

            return MessageBoxManager.GetMessageBoxStandard(msgboxparams).ShowWindowDialogAsync(parent);
        }
    }
}
