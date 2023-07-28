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
            string installedDirectory = this._launcherConfig.GameClientInstallationPath;
            if (!IsGameExisted(installedDirectory))
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

        public void Btn_UpdateCancel_Click(object source, RoutedEventArgs args)
        {
            // Steal it so that in case the button is clicked multiple times "at once", only the first click will do cancellation.
            var stolenCancelSrc = Interlocked.Exchange(ref this.cancelSrc_UpdateGameClient, null);
            stolenCancelSrc?.Cancel();
        }

        public async void Btn_StartGame_Click(object source, RoutedEventArgs args)
        {
            var localvar_btnGameStartState = this.GameStartButtonState; // can access field "this._gameStartButtonState" directly for performance;
            switch (localvar_btnGameStartState)
            {
                case GameStartButtonState.NeedInstall:
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
                            else if (await gameMgr.Updater.CheckForUpdatesAsync())
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
                                var newCancellation = new CancellationTokenSource();
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
                                        int progressPercentile_FileCheck = progressCallback.FileCheckProgress.TotalProgress == 0 ? 0 : (progressCallback.FileCheckProgress.IsDone ? 50 : progressCallback.FileCheckProgress.GetPercentile(50)),
                                        progressPercentile_FileDownload = progressCallback.TotalDownloadProgress.TotalProgress == 0 ? 0 : (progressCallback.TotalDownloadProgress.IsDone ? 50 : progressCallback.TotalDownloadProgress.GetPercentile(50));

                                        this.ProgressBar_Total.Value = progressPercentile_FileCheck + progressPercentile_FileDownload;
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
