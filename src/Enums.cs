namespace Leayal.SnowBreakLauncher;

public enum GameStartButtonState
{
    LoadingUI = 0,
    NeedInstall,
    CheckingForUpdates,
    RequiresUpdate,
    UpdatingGameClient,
    CanStartGame,
    StartingGame,
    WaitingForGameExit
}
