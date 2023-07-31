/*
 * This file contains mainly UI interaction and UI event handlers
*/

using Avalonia.Interactivity;
using Leayal.SnowBreakLauncher.Snowbreak;
using System.Collections.Generic;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Leayal.SnowBreakLauncher.Controls;
using Avalonia.Threading;
using Avalonia.Controls;
using System.Threading;
using Avalonia.Platform.Storage;
using System.IO;
using Avalonia;

namespace Leayal.SnowBreakLauncher.Windows
{
    public partial class MainWindow
    {
        private CancellationTokenSource? cancelSrc_UpdateGameClient;
        protected override async void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            
            await Task.WhenAll(this.AfterLoaded_Btn_GameStart(), this.AfterLoaded_LauncherNews());
        }

        private async Task AfterLoaded_LauncherNews()
        {
            var httpClient = SnowBreakHttpClient.Instance;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static List<NewsInlineTextWrapper> ToClassItems(IEnumerable<INewsInlineTextItem> items, int preCount)
            {
                var list = (preCount == -1 ? new List<NewsInlineTextWrapper>() : new List<NewsInlineTextWrapper>(preCount));
                foreach (var item in items)
                {
                    if (string.IsNullOrWhiteSpace(item.link)) continue;
                    list.Add(new NewsInlineTextWrapper()
                    {
                        link = item.link,
                        time = item.time,
                        title = item.title
                    });
                }
                return list;
            }

            using (var newsFeed = await httpClient.GetLauncherNewsAsync())
            {
                // var list_notices = AddItems(newsFeed.Notices, newsFeed.NoticeCount);
                // var list_events = AddItems(newsFeed.Events, newsFeed.EventCount);

                this.LauncherNews_Events.ItemsSource = ToClassItems(newsFeed.Events, newsFeed.EventCount);
                this.LauncherNews_Notices.ItemsSource = ToClassItems(newsFeed.Notices, newsFeed.NoticeCount);

                var listCount_banners = newsFeed.BannerCount;
                var list_banners = (listCount_banners == -1 ? new List<LauncherNewsBanner>() : new List<LauncherNewsBanner>(listCount_banners));
                foreach (var banner in newsFeed.Banners)
                {
                    if (string.IsNullOrWhiteSpace(banner.img) || string.IsNullOrWhiteSpace(banner.link)) continue;
                    
                    this.LauncherNews_Banners.Items.Add(new LauncherNewsBanner(banner.img, banner.link)
                    {
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
                    });
                }
            }

            this.carouselAutoplay.StartAutoplay();
        }

        private void NewsItem_PointerPressed(NewsInlineTextWrapper obj)
        {
            if (obj != null)
            {
                try
                {
                    Leayal.Shared.Windows.WindowsExplorerHelper.OpenUrlWithDefaultBrowser(obj.link);
                }
                catch { }
            }
        }

        public void Btn_BannerGoLeft_Click(object source, RoutedEventArgs args) => this.carouselAutoplay.GoLeft();

        public void Btn_BannerGoRight_Click(object source, RoutedEventArgs args) => this.carouselAutoplay.GoRight();

        private async Task AfterLoaded_Btn_GameStart()
        {
            var installedDirectory = this._launcherConfig.GameClientInstallationPath;
            if (string.IsNullOrEmpty(installedDirectory) || !IsGameExisted(System.IO.Path.GetFullPath(installedDirectory)))
            {
                // Game isn't installed or not detected
                this.GameStartButtonState = GameStartButtonState.NeedInstall;
            }
            else
            {
                var gameMgr = GameManager.SetGameDirectory(installedDirectory);
                if (gameMgr.Process.IsGameRunning)
                {
                    this.GameStartButtonState = GameStartButtonState.WaitingForGameExit;
                }
                else
                {
                    this.GameStartButtonState = GameStartButtonState.CheckingForUpdates;
                    var hasUpdate = await gameMgr.Updater.CheckForUpdatesAsync();
                    this.GameStartButtonState = hasUpdate switch
                    {
                        true => GameStartButtonState.RequiresUpdate,
                        _ => GameStartButtonState.CanStartGame
                    };
                }
            }
        }

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (e.IsProgrammatic)
            {
                base.OnClosing(e);
            }
            else
            {
                base.OnClosing(e);
                if (!e.Cancel)
                {
                    if (this.GameStartButtonState == GameStartButtonState.UpdatingGameClient)
                    {
                        // Always stop closing down the window as long as we're still in the state above.
                        e.Cancel = true;
                        if (this.cancelSrc_Root.IsCancellationRequested)
                        {
                            // We're already issued exit signal.
                            // When updating is completely cancelled and finalized, it will call Window.Close(), which leads to "IsProgrammatic" above and close the window gracefully.
                            return;
                        }
                        else
                        {
                            if (e.CloseReason == WindowCloseReason.OSShutdown)
                            {
                                // Because the OS is shutting down, there should be no interaction/user input to prevent OS from being stuck at shutting down screen.
                                // We send signal without user confirmation.
                                this.cancelSrc_Root.Cancel();
                            }
                            else if ((await this.ShowYesNoMsgBox("Game client is being updated. Are you sure you want to cancel updating and close the launcher?", "Confirmation")) == MsBox.Avalonia.Enums.ButtonResult.Yes)
                            {
                                // Send signal that the launcher should exit after complete the updating game client task.
                                this.cancelSrc_Root.Cancel();
                            }
                        }
                    }
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            GameManager.GameLocationChanged -= this.OnGameManagerChanged;
            if (GameManager.Instance is GameManager instance)
            {
                instance.Process.ProcessStarted -= this.GameManager_Process_Started;
                instance.Process.ProcessExited -= this.GameManager_Process_Exited;
            }
            this._launcherConfig.Dispose();
            this.carouselAutoplay.Dispose();
            base.OnClosed(e);
        }

        public async void Btn_UpdateCancel_Click(object source, RoutedEventArgs args)
        {
            // Don't need Interlocked, unneccessary to be strict here. It's just to avoid re-show the prompt below.
            // But it's still okay to re-show, too.
            if (this.cancelSrc_UpdateGameClient == null) return;

            if ((await this.ShowYesNoMsgBox("Game client is being updated. Are you sure you want to cancel updating?", "Confirmation")) == MsBox.Avalonia.Enums.ButtonResult.Yes)
            {
                // Steal it so that in case the button is clicked multiple times "at once", only the first click will do cancellation.
                var stolenCancelSrc = Interlocked.Exchange(ref this.cancelSrc_UpdateGameClient, null);
                stolenCancelSrc?.Cancel();
            }
        }

        public async void Btn_StartGame_Click(object source, RoutedEventArgs args)
        {
            var localvar_btnGameStartState = this.GameStartButtonState; // can access field "this._gameStartButtonState" directly for performance;
            switch (localvar_btnGameStartState)
            {
                case GameStartButtonState.NeedInstall:
                    // GameManager.Instance should be null here.
                    {
                        var installedDirectory = this._launcherConfig.GameClientInstallationPath;
                        if (!string.IsNullOrEmpty(installedDirectory))
                        {
                            installedDirectory = System.IO.Path.GetFullPath(installedDirectory);
                            if (IsGameExisted(installedDirectory))
                            {
                                if ((await ShowYesNoMsgBox("It seems like the configuration has changed." + Environment.NewLine
                                    + "Do you want to use the path from the configuration file (see below)?" + Environment.NewLine + Environment.NewLine
                                    + installedDirectory, "Confirmation")) == MsBox.Avalonia.Enums.ButtonResult.Yes)
                                {
                                    await this.AfterLoaded_Btn_GameStart();
                                    return;
                                }
                            }
                        }

                        var selection = await ShowYesNoCancelMsgBox("The launcher cannot find your game client. Please choose these options:" + Environment.NewLine + Environment.NewLine
                                   + "- Yes: Select a folder to install the game to." + Environment.NewLine
                                   + "- No: Browse for your existing game client." + Environment.NewLine
                                   + "- Cancel: Abort and go back.", "Prompt");
                        
                        if (selection == MsBox.Avalonia.Enums.ButtonResult.Yes)  // Browse for a folder, then install to the selected folder.
                        {
                            if (StorageProvider.CanPickFolder) // Should be true anyway, since this app is only Windows in mind.
                            {
                                var folderPickOpts = new FolderPickerOpenOptions()
                                {
                                    AllowMultiple = false,
                                    Title = "Select a folder to install the game to"
                                };

                                while (true)
                                {
                                    var results = await StorageProvider.OpenFolderPickerAsync(folderPickOpts);
                                    if (results == null || results.Count == 0) break;

                                    var selectedPath = results[0].TryGetLocalPath();
                                    if (string.IsNullOrEmpty(selectedPath))
                                    {
                                        await this.ShowInfoMsgBox("The path to the folder you selected is not a local path.", "Invalid item selected");
                                        continue;
                                    }
                                    
                                    if (!Directory.Exists(selectedPath))
                                    {
                                        if ((await this.ShowYesNoMsgBox("The path you specified doesn't exist. Create destination folder?", "Confirmation")) == MsBox.Avalonia.Enums.ButtonResult.No)
                                            continue;
                                    }

                                    if (IsGameExisted(selectedPath))
                                    {
                                        if ((await this.ShowYesNoMsgBox("Detected your game client:" + Environment.NewLine
                                            + selectedPath + Environment.NewLine + Environment.NewLine
                                            + "Do you want to use this path?" + Environment.NewLine
                                            + "(The path above is not missing anything, it is where the 'manifest.json' file supposed to be)", "Confirmation")) == MsBox.Avalonia.Enums.ButtonResult.Yes)
                                        {
                                            this._launcherConfig.GameClientInstallationPath = selectedPath;
                                            await this.AfterLoaded_Btn_GameStart();
                                            break;
                                        }
                                    }

                                    if ((await this.ShowYesNoMsgBox("Destination to install SnowBreak game client:" + Environment.NewLine
                                           + selectedPath + Environment.NewLine + Environment.NewLine
                                           + "Do you want to install the game to this destination?", "Confirmation")) == MsBox.Avalonia.Enums.ButtonResult.Yes)
                                    {
                                        selectedPath = Directory.CreateDirectory(selectedPath).FullName;
                                        this._launcherConfig.GameClientInstallationPath = selectedPath;
                                        GameManager.SetGameDirectory(selectedPath);
                                        this.GameStartButtonState = GameStartButtonState.RequiresUpdate;
                                        this.Btn_StartGame_Click(source, args);
                                        break;
                                    }
                                }
                            }
                        }
                        else if (selection == MsBox.Avalonia.Enums.ButtonResult.No) // Browse for existing game client
                        {
                            if (StorageProvider.CanOpen) // Should be true anyway, since this app is only Windows in mind.
                            {
                                var openFileOpts = new FilePickerOpenOptions()
                                {
                                    AllowMultiple = false,
                                    Title = "Browse for existing game client",
                                    FileTypeFilter = new List<FilePickerFileType>()
                                    {
                                        new FilePickerFileType("Game Client File") { Patterns = new string[] { "manifest.json", "game.exe" } },
                                        FilePickerFileTypes.All
                                    }
                                };

                                while (true)
                                {
                                    var results = await StorageProvider.OpenFilePickerAsync(openFileOpts);
                                    if (results == null || results.Count == 0) break;

                                    var path = results[0].TryGetLocalPath();
                                    if (string.IsNullOrEmpty(path))
                                    {
                                        await this.ShowInfoMsgBox("The file you selected is not a physical file.", "Invalid item selected");
                                        continue;
                                    }

                                 

                                    static string FolderGoBackFromGameExecutable(string path) => path.Remove(path.Length - GameManager.RelativePathToExecutablePath.Length - 1);

                                    string? selectedInstallationDirectory = IsClientOrManifest(path) switch
                                    {
                                        true => System.IO.Path.GetDirectoryName(path),
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
                                        + "(The path above is not missing anything, it is where the 'manifest.json' file supposed to be)", "Confirmation")) == MsBox.Avalonia.Enums.ButtonResult.Yes)
                                    {
                                        this._launcherConfig.GameClientInstallationPath = selectedInstallationDirectory;
                                        await this.AfterLoaded_Btn_GameStart();
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    break;
                case GameStartButtonState.CanStartGame:
                    {
                        if (GameManager.Instance is GameManager gameMgr)
                        {
                            var processMgr = gameMgr.Process;
                            if (processMgr.IsGameRunning)
                            {
                                this.GameStartButtonState = GameStartButtonState.WaitingForGameExit;
                            }
                            else
                            {
                                var prevState = this.GameStartButtonState;
                                try
                                {
                                    this.GameStartButtonState = GameStartButtonState.CheckingForUpdates;
                                    if (await gameMgr.Updater.CheckForUpdatesAsync())
                                    {
                                        if ((await ShowYesNoMsgBox("Your game client seems to be out-dated. Do you want to update it now?", "Confirmation")) == MsBox.Avalonia.Enums.ButtonResult.Yes)
                                        {
                                            this.GameStartButtonState = GameStartButtonState.RequiresUpdate;
                                            this.Btn_StartGame_Click(source, args);
                                        }
                                        else
                                        {
                                            this.GameStartButtonState = GameStartButtonState.RequiresUpdate;
                                        }
                                    }
                                    else
                                    {
                                        this.GameStartButtonState = GameStartButtonState.StartingGame;
                                        try
                                        {
                                            await processMgr.StartGame();
                                        }
                                        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
                                        {
                                            this.GameStartButtonState = GameStartButtonState.CanStartGame;
                                        }
                                        catch (Exception ex)
                                        {
                                            this.GameStartButtonState = GameStartButtonState.CanStartGame;
                                            this.ShowErrorMsgBox(ex);
                                            // MessageBox.Avalonia.MessageBoxManager
                                        }
                                    }
                                }
                                catch
                                {
                                    this.GameStartButtonState = prevState;
                                    throw;
                                }
                            }
                        }
                    }
                    break;
                case GameStartButtonState.RequiresUpdate:
                    {
                        if (GameManager.Instance is GameManager gameMgr)
                        {
                            var processMgr = gameMgr.Process;
                            if (processMgr.IsGameRunning)
                            {
                                this.GameStartButtonState = GameStartButtonState.WaitingForGameExit;
                            }
                            else
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
                                            attachedprogressbar.ProgressTextFormat = string.Concat(System.IO.Path.GetFileName(progress.Filename.AsSpan()), " ({1}%)");
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
                                    await updater.UpdateGameClientAsync(progressCallback: progressCallback, cancellationToken: cancelToken);

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
                                        this.ShowErrorMsgBox(ex);
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
                    break;
                default:
                // case GameStartButtonState.LoadingUI:
                // case GameStartButtonState.StartingGame:
                // case GameStartButtonState.WaitingForGameExit:
                    // Do nothing
                    break;
            }
        }
    }
}
