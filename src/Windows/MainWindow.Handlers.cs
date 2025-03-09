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
using Avalonia.Controls;
using System.Threading;
using Avalonia.Platform.Storage;
using System.IO;
using Leayal.Shared.Windows;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Reflection;
using Avalonia.Threading;

namespace Leayal.SnowBreakLauncher.Windows
{
    public partial class MainWindow
    {
        private CancellationTokenSource? cancelSrc_UpdateGameClient;
        protected override async void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            await Task.WhenAll(this.AfterLoaded_Btn_GameStart(), this.AfterLoaded_LauncherNews());
            _ = Task.Factory.StartNew(this.PeriodicallyRefreshNews, TaskCreationOptions.LongRunning);
        }

        private async Task PeriodicallyRefreshNews()
        {  
            try
            {
                using (var timer = new PeriodicTimer(TimeSpan.FromHours(8)))
                {
                    await timer.WaitForNextTickAsync();
                    var dispatcher = Dispatcher.UIThread;
                    if (dispatcher.CheckAccess())
                    {
                        // Can't be here anyway.
                        await this.AfterLoaded_LauncherNews();
                    }
                    else
                    {
                        await dispatcher.InvokeAsync(this.AfterLoaded_LauncherNews);
                    }
                }
            }
            catch { }
        }

        private void ExtraContextMenu_Initialized(object? sender, EventArgs e)
        {
            this.MenuItem_LocalizationMagicTrick.ToggleType = MenuItemToggleType.CheckBox;
            if (!OperatingSystem.IsWindows() && sender is ContextMenu ctxMenu)
            {
                var linuxWineSettingsBtn = new MenuItem();
                linuxWineSettingsBtn.Header = new TextBlock() { Text = "Wine Settings" };
                linuxWineSettingsBtn.Click += this.LinuxWineSettingsBtn_Click;
                var items = ctxMenu.Items;
                var lastItemIndex = items.IndexOf(this.MenuItem_LocalizationMagicTrick);
                if (lastItemIndex != -1)
                {
                    items.Insert(0, linuxWineSettingsBtn);
                }
                else
                {
                    items.Add(linuxWineSettingsBtn);
                }
            }
        }

        private void ExtraContextMenu_Opened(object? sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu ctxMenu)
            {
                bool hasMagicTrick = false;
                if (GameManager.Instance is GameManager mgr /*mainly to check for null here*/)
                {
                    var installationDir = mgr.FullPathOfInstallationDirectory;
                    if (!installationDir.IsEmpty)
                    {
                        // This is bad design. Doing disk I/O on UI thread is against responsiveness design.
                        // Also ugly if-else.
                        var magicTrickLocation = Path.Join(installationDir, "localization.txt");
                        if (File.Exists(magicTrickLocation))
                        {
                            using (var fhmagicTrick = File.OpenRead(magicTrickLocation))
                            using (var textReadermagicTrick = new StreamReader(fhmagicTrick /*, System.Text.Encoding.GetEncoding(1252)*/))
                            {
                                var line = textReadermagicTrick.ReadLine();
                                if (!string.IsNullOrWhiteSpace(line))
                                {
                                    var valueSpan = line.AsSpan();
                                    var i_splitter = valueSpan.IndexOf('=');
                                    if (i_splitter != -1 && (i_splitter < (valueSpan.Length - 1)))
                                    {
                                        var propName = valueSpan.Slice(0, i_splitter).Trim();
                                        var propValueText = valueSpan.Slice(i_splitter + 1).Trim();
                                        if (!propName.IsEmpty && !propValueText.IsEmpty)
                                        {
                                            if (MemoryExtensions.Equals(propName, "localization", StringComparison.OrdinalIgnoreCase))
                                            {
                                                if (int.TryParse(propValueText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var propValue))
                                                {
                                                    if (propValue != 0) /* I don't know if this is correct or not. Mainly because the game may check if it's either 0 or 1, any other numbers may be treated the same meaning as 0 */
                                                    {
                                                        hasMagicTrick = true;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                this.MenuItem_LocalizationMagicTrick.IsChecked = hasMagicTrick;
            }
        }

        [UnsupportedOSPlatform("windows")]
        private void LinuxWineSettingsBtn_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new LinuxWineSettings();
            dialog.ShowDialog(this);
        }

        private void LauncherVersionString_Initialized(object? sender, EventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                var lines = textBlock.Inlines;
                if (lines == null)
                {
                    lines = new Avalonia.Controls.Documents.InlineCollection();
                    textBlock.Inlines = lines;
                }
                lines.AddRange(new Avalonia.Controls.Documents.Run[]
                {
                    new Avalonia.Controls.Documents.Run("Launcher version: "),
                    new Avalonia.Controls.Documents.Run(Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "Unknown version") { TextDecorations = Avalonia.Media.TextDecorations.Underline },
                    new Avalonia.Controls.Documents.Run(" (Click to open releases page)"),
                });
                Clickable.OnClick(textBlock, LauncherVersionString_Clicked);
            }
        }

        private static void LauncherVersionString_Clicked(TextBlock sender, RoutedEventArgs e)
        {
            OpenURLWithDefaultBrowser("https://github.com/Leayal/SnowBreakLauncher-Dotnet/releases/latest");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void OpenURLWithDefaultBrowser(string url)
        {
            if (OperatingSystem.IsWindows())
            {
                // We really need this on Windows to avoid starting a new web browser process as Admin, in case this launcher is run as Admin.
                WindowsExplorerHelper.OpenUrlWithDefaultBrowser(url);
            }
            else
            {
                Process.Start(new ProcessStartInfo(url)
                {
                    UseShellExecute = true,
                    Verb = "open"
                })?.Dispose();
            }
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
                var events = ToClassItems(newsFeed.Events, newsFeed.EventCount);
                this.LauncherNews_Events.ItemsSource = events.Count == 0 ? null : events;
                var notices = ToClassItems(newsFeed.Notices, newsFeed.NoticeCount);
                this.LauncherNews_Notices.ItemsSource = notices.Count == 0 ? null : notices;

                var listCount_banners = newsFeed.BannerCount;
                var list_banners = (listCount_banners == -1 ? new List<LauncherNewsBanner>() : new List<LauncherNewsBanner>(listCount_banners));
                foreach (var banner in newsFeed.Banners)
                {
                    if (string.IsNullOrWhiteSpace(banner.img) || string.IsNullOrWhiteSpace(banner.link)) continue;

                    list_banners.Add(new LauncherNewsBanner(banner.img, banner.link)
                    {
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
                    });
                }

                this.LauncherNews_Banners.ItemsSource = list_banners.Count == 0 ? null : list_banners;
            }

            this.carouselAutoplay.StartAutoplay();
        }

        private void NewsItem_PointerPressed(NewsInlineTextWrapper obj)
        {
            if (obj != null)
            {
                try
                {
                    OpenURLWithDefaultBrowser(obj.link);
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

        public void MenuItem_LocalizationMagicTrick_Click(object source, RoutedEventArgs args)
        {
            if (source is MenuItem menuItem && GameManager.Instance is GameManager mgr /*mainly to check for null here*/)
            {
                var exePath = mgr.GameExecutablePath;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath) /* Check if the game executable is really existed */)
                {
                    // Thankfully, the value is updated before the Click Event invoke the this handler.
                    // In the other word: Getting IsChecked value in this handler gives "new value" instead of "current value".
                    var newValue = menuItem.IsChecked;

                    // This is bad design. Doing disk I/O on UI thread is against responsiveness design. Especially on "disk write I/O" operations.
                    var magicTrickLocation = Path.Join(mgr.FullPathOfInstallationDirectory, "localization.txt");
                    using (var fhmagicTrick = new FileStream(magicTrickLocation, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096))
                    {
                        // var newStringValue = "localization = " + (newValue ? "1" : "0"); // Does this get optimized by compiler to be more memory-optimized?
                        var newStringValue = newValue ? "localization = 1" : "localization = 0"; // This is memory-optimized

                        /* The dilema I'm having is that I don't know whether the game actually use ANSI or it uses UTF-8.
                         * But I'll pick ANSI. Using ANSI encoding should have highest compatibility in most situations.
                        */
                        var encoding = System.Text.Encoding.Latin1; // This one is ANSI-compatible.
                        var byteCount = newStringValue.Length * (encoding.IsSingleByte ? 1 : 2); /* encoding.GetByteCount(newStringValue); */
                        var ch = System.Buffers.ArrayPool<byte>.Shared.Rent(byteCount + 1);
                        try
                        {
                            byteCount /* fixes the byte count to correct value of the encoded data length */ = encoding.GetBytes(newStringValue, ch);
                            fhmagicTrick.Position = 0;
                            fhmagicTrick.Write(ch, 0, byteCount);
                            if (fhmagicTrick.Length != byteCount) // Best screnario is file's length is already same length.
                            {
                                fhmagicTrick.SetLength(byteCount);
                            }
                        }
                        finally
                        {
                            System.Buffers.ArrayPool<byte>.Shared.Return(ch);
                        }
                        fhmagicTrick.Flush();
                    }
                }
            }
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

        public void MenuItem_LauncherSettings_Click(object source, RoutedEventArgs args)
        {
            var dialog = new LauncherSettings();
            dialog.ShowDialog(this);
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

            switch (this.GameStartButtonState)
            {
                case GameStartButtonState.CanStartGame:
                case GameStartButtonState.RequiresUpdate:
                    break;
                default:
                    return;
            }

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
