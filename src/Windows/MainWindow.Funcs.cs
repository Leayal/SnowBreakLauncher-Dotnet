/*
 * This file contains mainly logic and functional code
*/

using Avalonia.Threading;
using Leayal.SnowBreakLauncher.Classes;
using Leayal.SnowBreakLauncher.Snowbreak;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher
{
    public partial class MainWindow
    {
        private readonly OfficialJsonConfiguration _launcherConfig;

        private void OnGameManagerChanged(GameManager? oldOne, GameManager newOne)
        {
            if (oldOne != null)
            {
                oldOne.Process.ProcessStarted -= GameManager_Process_Started;
                oldOne.Process.ProcessExited -= GameManager_Process_Exited;
            }
            newOne.Process.ProcessStarted += GameManager_Process_Started;
            newOne.Process.ProcessExited += GameManager_Process_Exited;
        }

     

        private void GameManager_Process_Started(in uint processId)
        {
            if (this.CheckAccess())
            {
                this.GameStartButtonState = GameStartButtonState.WaitingForGameExit;
            }
            else
            {
                var wrapper = new Wrapper_GameManager_ProcessStarted(in processId, this);
                Dispatcher.UIThread.InvokeAsync(wrapper.GameManager_Process_Started);
            }
        }

        private readonly struct Wrapper_GameManager_ProcessStarted
        {
            public readonly uint processId;
            private readonly MainWindow window;

            public Wrapper_GameManager_ProcessStarted(in uint processId, MainWindow window)
            {
                this.processId = processId;
                this.window = window;
            }

            public void GameManager_Process_Started()
            {
                this.window.GameManager_Process_Started(in this.processId);
            }
        }

        private void GameManager_Process_Exited(in uint processId)
        {
            if (this.CheckAccess())
            {
                this.GameStartButtonState = GameStartButtonState.CanStartGame;
            }
            else
            {
                var wrapper = new Wrapper_GameManager_ProcessExited(in processId, this);
                Dispatcher.UIThread.InvokeAsync(wrapper.GameManager_Process_Exited);
            }
        }

        private readonly struct Wrapper_GameManager_ProcessExited
        {
            public readonly uint processId;
            private readonly MainWindow window;

            public Wrapper_GameManager_ProcessExited(in uint processId, MainWindow window)
            {
                this.processId = processId;
                this.window = window;
            }

            public void GameManager_Process_Exited()
            {
                this.window.GameManager_Process_Exited(in this.processId);
            }
        }
    }
}
