/*
 * This file contains mainly logic and functional code
*/

using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Leayal.SnowBreakLauncher.Classes;
using Leayal.SnowBreakLauncher.Snowbreak;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher.Windows
{
    public partial class MainWindow
    {
        private readonly CancellationTokenSource cancelSrc_Root;
        private readonly OfficialJsonConfiguration _launcherConfig;

        private void OnGameManagerChanged(GameManager? oldOne, GameManager newOne)
        {
            // Since we're changing directory, closing the old File handles in the old directory. This is only meaningful on non-Windows.
            if (!OperatingSystem.IsWindows()) GameClientManifestData.CloseAllHandles();

            if (oldOne != null)
            {
                oldOne.Process.ProcessStarted -= GameManager_Process_Started;
                oldOne.Process.ProcessExited -= GameManager_Process_Exited;
            }
            newOne.Process.ProcessStarted += GameManager_Process_Started;
            newOne.Process.ProcessExited += GameManager_Process_Exited;
        }

        private void GameManager_Process_Started(in uint processId)
        {
            if (this.CheckAccess())
            {
                this.GameStartButtonState = GameStartButtonState.WaitingForGameExit;
            }
            else
            {
                var wrapper = new Wrapper_GameManager_ProcessStarted(in processId, this);
                Dispatcher.UIThread.InvokeAsync(wrapper.GameManager_Process_Started);
            }
        }

        private readonly struct Wrapper_GameManager_ProcessStarted
        {
            public readonly uint processId;
            private readonly MainWindow window;

            public Wrapper_GameManager_ProcessStarted(in uint processId, MainWindow window)
            {
                this.processId = processId;
                this.window = window;
            }

            public void GameManager_Process_Started()
            {
                this.window.GameManager_Process_Started(in this.processId);
            }
        }

        private void GameManager_Process_Exited(in uint processId)
        {
            if (this.CheckAccess())
            {
                this.GameStartButtonState = GameStartButtonState.CanStartGame;
            }
            else
            {
                var wrapper = new Wrapper_GameManager_ProcessExited(in processId, this);
                Dispatcher.UIThread.InvokeAsync(wrapper.GameManager_Process_Exited);
            }
        }

        private readonly struct Wrapper_GameManager_ProcessExited
        {
            public readonly uint processId;
            private readonly MainWindow window;

            public Wrapper_GameManager_ProcessExited(in uint processId, MainWindow window)
            {
                this.processId = processId;
                this.window = window;
            }

            public void GameManager_Process_Exited()
            {
                this.window.GameManager_Process_Exited(in this.processId);
            }
        }

        private async Task StuckInLoop_BrowseGameFolder()
        {
            var openFileOpts = new FilePickerOpenOptions()
            {
                AllowMultiple = false,
                Title = "Browse for existing game client",
                FileTypeFilter = new List<FilePickerFileType>(2)
                {
                    new FilePickerFileType("Game Client File") { Patterns = new string[] { "manifest.json", "game.exe" } },
                    FilePickerFileTypes.All
                }
            };

            while (true) // You're stuck here!!!! Actually not, this is for re-entering the dialog when user doesn't click cancel.
            {
                var results = await StorageProvider.OpenFilePickerAsync(openFileOpts);
                if (results == null || results.Count == 0) break;

                var path = results[0].TryGetLocalPath();
                if (string.IsNullOrEmpty(path))
                {
                    await this.ShowInfoMsgBox("The file you selected is not a local file on your machine.", "Invalid item selected");
                    continue;
                }

                static string FolderGoBackFromGameExecutable(string path) => path.Remove(path.Length - GameManager.RelativePathToExecutablePath.Length - 1);

                string? selectedInstallationDirectory = IsClientOrManifest(path) switch
                {
                    true => Path.GetDirectoryName(path),
                    false => FolderGoBackFromGameExecutable(path),
                    _ => null
                };

                if (string.IsNullOrEmpty(selectedInstallationDirectory))
                {
                    await this.ShowInfoMsgBox("The file you selected doesn't seem to be the expected SnowBreak game client file.", "Invalid item selected");
                    continue;
                }

                if ((await this.ShowYesNoMsgBox("Detected your game client:" + Environment.NewLine
                    + selectedInstallationDirectory + Environment.NewLine + Environment.NewLine
                    + "Do you want to use this path?" + Environment.NewLine
                    + "(The path above is not missing anything, it is where the 'manifest.json' file supposed to be)" + Environment.NewLine
                    + "(If the path doesn't look like what you desired, please move the folder and select the relocated folder)", "Confirmation")) == MsBox.Avalonia.Enums.ButtonResult.Yes)
                {
                    this._launcherConfig.GameClientInstallationPath = selectedInstallationDirectory;
                    await this.AfterLoaded_Btn_GameStart();
                    break;
                }
            }
        }

        private async Task PerformGameClientUpdate(GameManager gameMgr, bool skipCrcTableCache = false)
        {
            var updater = gameMgr.Updater;
            this.GameStartButtonState = GameStartButtonState.UpdatingGameClient;
            var newCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.cancelSrc_Root.Token);
            var oldCancellation = Interlocked.Exchange(ref this.cancelSrc_UpdateGameClient, newCancellation);
            try
            {
                if (oldCancellation != null)
                {
                    oldCancellation.Cancel();
                    oldCancellation.Dispose();
                }
            }
            catch { }
            Action StopIndetermined = () =>
            {
                this.ProgressBar_Total.ProgressTextFormat = "File check and downloading ({1}%)";
                this.ProgressBar_Total.IsIndeterminate = false;
                this.ProgressBar_Download1.IsIndeterminate = false;
                this.ProgressBar_Download2.IsIndeterminate = false;
                this.ProgressBar_Download1.ShowProgressText = true;
                this.ProgressBar_Download2.ShowProgressText = true;
            };
            var progressCallback = new GameUpdaterProgressCallback(() =>
            {
                if (this.ProgressBar_Total.CheckAccess())
                {
                    StopIndetermined.Invoke();
                }
                else
                {
                    // Should be here, since we're under different thread that invoked this callback.
                    Dispatcher.UIThread.InvokeAsync(StopIndetermined);
                }
            });
            this.ProgressBar_Total.IsIndeterminate = true;
            this.ProgressBar_Download1.IsIndeterminate = true;
            this.ProgressBar_Download2.IsIndeterminate = true;
            this.ProgressBar_Total.ProgressTextFormat = "Downloading manifest from remote host";
            this.ProgressBar_Download1.ShowProgressText = false;
            this.ProgressBar_Download2.ShowProgressText = false;
            var uiUpdaterCancellation = DispatcherTimer.Run(() =>
            {
                if (!this.ProgressBar_Total.IsIndeterminate)
                {
                    // Yes, max is 50%, we split the "Total Progress" bar into 2, half for file checking, the other half for file downloading.
                    if (progressCallback.FileCheckProgress.IsDone && progressCallback.TotalDownloadProgress.IsDone)
                    {
                        this.ProgressBar_Total.Value = 100;
                    }
                    else
                    {
                        if (progressCallback.FileCheckProgress.IsDone)
                        {
                            this.ProgressBar_Total.Value = progressCallback.TotalDownloadProgress.GetPercentile();
                        }
                        else if (progressCallback.TotalDownloadProgress.IsDone)
                        {
                            this.ProgressBar_Total.Value = progressCallback.FileCheckProgress.GetPercentile();
                        }
                        else
                        {
                            long sumCurrent = progressCallback.TotalDownloadProgress.CurrentProgress + progressCallback.FileCheckProgress.CurrentProgress,
                                sumTotal = progressCallback.TotalDownloadProgress.TotalProgress + progressCallback.FileCheckProgress.TotalProgress;

                            var tmp = (sumCurrent * 100d) / sumTotal;
                            this.ProgressBar_Total.Value = tmp;
                        }
                    }
                }
                static void UpdateProgressBar(GameUpdaterDownloadProgressValue progress, ProgressBar attachedprogressbar)
                {
                    /* String.Format of the Avalonia ProgressBarText
                    0 = Value
                    1 = Value as a Percentage from 0 to 100 (e.g. Minimum = 0, Maximum = 50, Value = 25, then Percentage = 50)
                    2 = Minimum
                    3 = Maximum
                    */
                    var oldFilename = attachedprogressbar.Tag as string;
                    if (!string.Equals(oldFilename, progress.Filename, StringComparison.Ordinal))
                    {
                        attachedprogressbar.Tag = progress.Filename;
                        attachedprogressbar.ProgressTextFormat = string.Concat(Path.GetFileName(progress.Filename.AsSpan()), " ({1}%)");
                    }
                    if (progress.IsDone)
                    {
                        attachedprogressbar.Value = 100;
                    }
                    else if (progress.TotalProgress == 0 || attachedprogressbar.IsIndeterminate)
                    {
                        attachedprogressbar.Value = 0;
                    }
                    else
                    {
                        attachedprogressbar.Value = progress.GetPercentile();
                    }
                }

                UpdateProgressBar(progressCallback.Download1Progress, this.ProgressBar_Download1);
                UpdateProgressBar(progressCallback.Download2Progress, this.ProgressBar_Download2);

                return true;
            }, TimeSpan.FromMilliseconds(50), DispatcherPriority.Render);
            try
            {
                var cancelToken = newCancellation.Token;

                await updater.UpdateGameClientAsync(fixMode: skipCrcTableCache, progressCallback: progressCallback, cancellationToken: cancelToken);

                if (await updater.CheckForUpdatesAsync(cancelToken))
                {
                    this.GameStartButtonState = GameStartButtonState.RequiresUpdate;
                }
                else
                {
                    this.GameStartButtonState = GameStartButtonState.CanStartGame;
                }
            }
            catch (Exception ex)
            {
                this.GameStartButtonState = GameStartButtonState.RequiresUpdate;
                if (ex is not OperationCanceledException)
                    await this.ShowErrorMsgBox(ex);
            }
            finally
            {
                uiUpdaterCancellation.Dispose();
                Interlocked.Exchange(ref this.cancelSrc_UpdateGameClient, null); // Set it back to null
                newCancellation.Dispose();
                if (this.cancelSrc_Root.IsCancellationRequested)
                {
                    this.Close();
                }
            }
        }
    }
}
