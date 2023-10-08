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
using Leayal.Shared.Windows;
using System.Diagnostics;
using Leayal.SnowBreakLauncher.Classes;
using System.Runtime.Versioning;

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

        private void ExtraContextMenu_Initialized(object? sender, EventArgs e)
        {
            if (!OperatingSystem.IsWindows() && sender is ContextMenu ctxMenu)
            {
                var linuxWineSettingsBtn = new MenuItem();
                linuxWineSettingsBtn.Header = new TextBlock() { Text = "Wine Settings" };
                linuxWineSettingsBtn.Click += this.LinuxWineSettingsBtn_Click;
                ctxMenu.Items.Add(linuxWineSettingsBtn);
            }
        }

        [UnsupportedOSPlatform("windows")]
        private void LinuxWineSettingsBtn_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new LinuxWineSettings();
            dialog.ShowDialog(this);
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
                    if (OperatingSystem.IsWindows())
                    {
                        // We really need this on Windows to avoid starting a new web browser process as Admin, in case this launcher is run as Admin.
                        Leayal.Shared.Windows.WindowsExplorerHelper.OpenUrlWithDefaultBrowser(obj.link);
                    }
                    else
                    {
                        Process.Start(new ProcessStartInfo(obj.link)
                        {
                            UseShellExecute = true,
                            Verb = "open"
                        })?.Dispose();
                    }
                }
                catch { }
            }
        }

        public void Btn_BannerGoLeft_Click(object source, RoutedEventArgs args) => this.carouselAutoplay.GoLeft();

        public void Btn_BannerGoRight_Click(object source, RoutedEventArgs args) => this.carouselAutoplay.GoRight();

        private async Task AfterLoaded_Btn_GameStart()
        {
            var installedDirectory = this._launcherConfig.GameClientInstallationPath;
            if (string.IsNullOrEmpty(installedDirectory) || !IsGameExisted(Path.GetFullPath(installedDirectory)))
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
            this.cancelSrc_Root?.Dispose();
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

        public void BtnSettings_Click(object source, RoutedEventArgs args)
        {
            var btn = (source as Button) ?? (args.Source as Button);
            if (btn == null) return;
            if (btn.ContextMenu is ContextMenu ctxMenu) ctxMenu.Open(btn);
        }

        public async void MenuItem_OpenGameDataDirectory_Click(object source, RoutedEventArgs args)
        {
            var gameMgr = GameManager.Instance as GameManager;
            if (gameMgr == null)
            {
                await this.ShowDialog_LetUserKnowGameDirectoryIsNotSetForThisFunction();
                return;
            }
            var browsingDir = new string(gameMgr.FullPathOfGameDirectory);

            if (OperatingSystem.IsWindows())
            {
                // We really need this on Windows to avoid opening a new File Explorer process as Admin, in case this launcher is run as Admin.
                WindowsExplorerHelper.SelectPathInExplorer(browsingDir);
            }
            else
            {
                Process.Start(new ProcessStartInfo(browsingDir)
                {
                    UseShellExecute = true,
                    Verb = "open"
                })?.Dispose();
            }
        }

        public async void MenuItem_ChangeGameClientDirectory_Click(object source, RoutedEventArgs args)
        {
            var gameMgr = GameManager.Instance as GameManager;
            if (gameMgr == null)
            {
                await this.ShowDialog_LetUserKnowGameDirectoryIsNotSetForThisFunction();
                return;
            }

            if (this.GameStartButtonState != GameStartButtonState.CanStartGame) return;
            await this.StuckInLoop_BrowseGameFolder();
        }

        public async void MenuItem_GameDataIntegrityCheck_Click(object source, RoutedEventArgs args)
        {
            var gameMgr = GameManager.Instance as GameManager;
            if (gameMgr == null)
            {
                await this.ShowDialog_LetUserKnowGameDirectoryIsNotSetForThisFunction();
                return;
            }

            if (this.GameStartButtonState != GameStartButtonState.CanStartGame) return;

            if ((await ShowYesNoMsgBox("Are you sure you want to begin file integrity check and download missing/damaged files?" + Environment.NewLine
                + "(This action will take a short time or long time, depending on your disk's speed)", "Confirmation")) != MsBox.Avalonia.Enums.ButtonResult.Yes)
            {
                return;
            }

            try
            {
                await this.PerformGameClientUpdate(gameMgr, true);
            }
            catch (OperationCanceledException) { } // Silence it, user intentionally cancel it anyway
            catch (Exception ex)
            {
                await this.ShowErrorMsgBox(ex);
            }
            finally
            {
                this.GameStartButtonState = GameStartButtonState.CanStartGame;
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
                            installedDirectory = Path.GetFullPath(installedDirectory);
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
                            else
                            {
                                await this.ShowInfoMsgBox("This action Your OS is not supported. Something went wrong!!!", "Error", MsBox.Avalonia.Enums.Icon.Error);
                            }
                        }
                        else if (selection == MsBox.Avalonia.Enums.ButtonResult.No) // Browse for existing game client
                        {
                            if (StorageProvider.CanOpen) // Should be true anyway, since this app is only Windows in mind.
                            {
                                await this.StuckInLoop_BrowseGameFolder();
                            }
                            else
                            {
                                await this.ShowInfoMsgBox("This action Your OS is not supported. Something went wrong!!!", "Error", MsBox.Avalonia.Enums.Icon.Error);
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
                                if (OperatingSystem.IsWindows())
                                {
                                    this.GameStartButtonState = GameStartButtonState.WaitingForGameExit;
                                }
                                else
                                {
                                    await ShowInfoMsgBox("The game is already running", "Game already running");
                                }
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
                                            await this.ShowErrorMsgBox(ex);
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
                                await this.PerformGameClientUpdate(gameMgr);
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
