/*
 * This file contains mainly UI interaction and UI event handlers
*/

using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Leayal.SnowBreakLauncher.Snowbreak;
using System.Collections.Generic;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Leayal.SnowBreakLauncher.Controls;

namespace Leayal.SnowBreakLauncher.Windows
{
    public partial class MainWindow
    {
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
            base.OnClosed(e);
        }

        public async void Btn_StartGame_Click(object source, RoutedEventArgs args)
        {
            var localvar_btnGameStartState = this.GameStartButtonState; // can access field "this._gameStartButtonState" directly for performance;
            switch (localvar_btnGameStartState)
            {
                case GameStartButtonState.NeedInstall:
                    break;
                case GameStartButtonState.CanStartGame:
                    if (GameManager.Instance is GameManager gameMgr)
                    {
                        var processMgr = gameMgr.Process;
                        if (processMgr.IsGameRunning)
                        {
                            this.GameStartButtonState = GameStartButtonState.WaitingForGameExit;
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
