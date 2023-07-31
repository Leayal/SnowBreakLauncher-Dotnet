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
    WaitingForGameExit,
    // I seriously don't know what this should be for, the most generic and vague one, implying the launcher is "doing something" but unsure what to say/show to user.
    // Unused value for now.
    PreparingGeneric
}
