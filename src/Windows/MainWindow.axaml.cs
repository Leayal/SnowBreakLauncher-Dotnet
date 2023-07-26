/*
 * This file contains mainly UI manipulation code
*/

using Avalonia.Controls;
using System.IO;
using System.Diagnostics;
using Avalonia;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;
using System.Net;
using Leayal.SnowBreakLauncher.Snowbreak;
using Leayal.SnowBreakLauncher.Classes;
using Leayal.SnowBreakLauncher.Controls;

namespace Leayal.SnowBreakLauncher.Windows;

public partial class MainWindow : Window
{
    private readonly CarouselAutoplay carouselAutoplay;

    public MainWindow(OfficialJsonConfiguration launcherConfig)
    {
        this._launcherConfig = launcherConfig;
        GameManager.GameLocationChanged += this.OnGameManagerChanged;
        InitializeComponent();
        if (EmbedResources.WindowIcon.Value is Bitmap bm)
        {
            this.Icon = new WindowIcon(bm);
        }
        this.GameStartButtonState = GameStartButtonState.LoadingUI;
        this.carouselAutoplay = new CarouselAutoplay(this.LauncherNews_Banners);
    }

    #region "| GameStartButtonState |"
    private GameStartButtonState _gameStartButtonState;

    public static readonly DirectProperty<MainWindow, GameStartButtonState> GameStartButtonStateProperty = AvaloniaProperty.RegisterDirect<MainWindow, GameStartButtonState>("GameStartButtonState",
        window => window._gameStartButtonState,
        (window, value) =>
        {
            if (window._gameStartButtonState != value)
            {
                window._gameStartButtonState = value;
                switch (value)
                {
                    case GameStartButtonState.CheckingForUpdates:
                        window.Btn_StartGame.IsEnabled = false;
                        window.BtnText_StartGame.Text = "Checking for game client updates";
                        break;
                    case GameStartButtonState.CanStartGame:
                        window.BtnText_StartGame.Text = "Start game";
                        window.Btn_StartGame.IsEnabled = true;
                        break;
                    case GameStartButtonState.RequiresUpdate:
                        window.Btn_StartGame.IsEnabled = true;
                        window.BtnText_StartGame.Text = "Update game client";
                        break;
                    case GameStartButtonState.StartingGame:
                        window.Btn_StartGame.IsEnabled = false;
                        window.BtnText_StartGame.Text = "Game is starting...";
                        break;
                    case GameStartButtonState.WaitingForGameExit:
                        window.Btn_StartGame.IsEnabled = false;
                        window.BtnText_StartGame.Text = "Game is running...";
                        break;
                    case GameStartButtonState.NeedInstall:
                        window.Btn_StartGame.IsEnabled = true;
                        window.BtnText_StartGame.Text = "Install or select existing data";
                        break;
                    default:
                        window.Btn_StartGame.IsEnabled = false;
                        window.BtnText_StartGame.Text = "Loading launcher UI...";
                        break;
                }
            }
        }, GameStartButtonState.LoadingUI);

    public GameStartButtonState GameStartButtonState
    {
        get => this.GetValue(GameStartButtonStateProperty);
        set => this.SetValue(GameStartButtonStateProperty, value);
    }
    #endregion
}