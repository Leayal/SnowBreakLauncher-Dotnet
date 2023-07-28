using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher
{
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
}
