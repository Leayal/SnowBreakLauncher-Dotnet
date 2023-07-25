/*
 * This file contains mainly UI interaction and UI event handlers
*/

using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Leayal.SnowBreakLauncher.Snowbreak;
using MsBox.Avalonia;
using System;
using System.ComponentModel;

namespace Leayal.SnowBreakLauncher
{
    public partial class MainWindow
    {
        protected override async void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            // Check if game is installed.
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
