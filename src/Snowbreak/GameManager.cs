using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher.Snowbreak
{
    /// <summary>Mainly to managing the states and fields in a convenient way.</summary>
    /// <remarks>As well as packing fields into one object.</remarks>
    class GameManager
    {
        public static readonly string RelativePathToExecutablePath = Path.Join("game", "Game", "Binaries", "Win64", "Game.exe");
        private static readonly object lockObj = new object();
        private static GameManager? _instance;

        public static GameManager? Instance => _instance;
        

        public delegate void GameLocationChangedHandler(GameManager? oldInstance, GameManager newInstance);
        public static event GameLocationChangedHandler? GameLocationChanged;

        static GameManager()
        {
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        private static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
            _instance?.Process.Dispose();
        }

        public static GameManager SetGameDirectory(string directoryPath)
        {
            ArgumentException.ThrowIfNullOrEmpty(directoryPath);

            lock (lockObj)
            {
                var targetingPath = Path.GetFullPath(directoryPath);
                if (_instance == null)
                {
                    _instance = new GameManager(directoryPath, targetingPath);
                    GameLocationChanged?.Invoke(null, _instance);
                }
                else if (!MemoryExtensions.Equals(targetingPath, _instance.FullPathOfInstallationDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    var newInstance = new GameManager(directoryPath, targetingPath);
                    var oldInstance = Interlocked.Exchange(ref _instance, newInstance);
                    oldInstance?.Process.Dispose();
                    GameLocationChanged?.Invoke(oldInstance, newInstance);
                }
                return _instance;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetGameExecutablePath(string installDirectory) => Path.GetFullPath(RelativePathToExecutablePath, installDirectory);

        /// <summary>The string which was passed to <seealso cref="GameManager.GameManager(string)"/> constructor.</summary>
        /// <remarks>This is not guaranteed to be a full qualified path.</remarks>
        public readonly string InstallationDirectory;
        /// <summary>The full qualified path to the executable file of the game.</summary>
        public readonly string GameExecutablePath;

        public readonly GameProcessManager Process;
        public readonly GameDataManager Files;
        public readonly GameUpdater Updater;

        private GameManager(string installDirectoryRaw, string installDirectoryFullPath)
        {
            // ArgumentException.ThrowIfNullOrEmpty(installDirectory);

            this.InstallationDirectory = installDirectoryRaw;
            this.GameExecutablePath = GetGameExecutablePath(installDirectoryFullPath);

            this.Process = new GameProcessManager(this);
            this.Files = new GameDataManager(this);
            this.Updater = new GameUpdater(this);
        }

        /// <summary>Usually this is the path to folder which contains the official game launcher.</summary>
        /// <remarks>This is only an assumption path, it only returns a full path to the parent folder that containing the game client's folder. This path may not really be the official launcher's location.</remarks>
        public ReadOnlySpan<char> FullPathOfOfficialLauncherDirectory => Path.GetDirectoryName(FullPathOfInstallationDirectory);
        /// <summary>Usually this is the path to folder which contains the game client files.</summary>
        /// <remarks>
        /// <para>Different from <seealso cref="FullPathOfGameDirectory"/>, this returns the path to <u>the parent folder</u> of <seealso cref="FullPathOfGameDirectory"/>.</para>
        /// <para>This folder is also what the official game launcher store local manifest files and stuffs to speed up the game client updating progress.</para>
        /// </remarks>
        public ReadOnlySpan<char> FullPathOfInstallationDirectory => this.GameExecutablePath.AsSpan(0, this.GameExecutablePath.Length - RelativePathToExecutablePath.Length - 1);
        /// <summary>Usually this is the path to folder which contains the game client.</summary>
        /// <remarks>
        /// <para>Different from <seealso cref="FullPathOfInstallationDirectory"/>, this returns the path to the actual folder of the Unreal Engine game client.</para>
        /// <para>This folder is the UE distribution.</para>
        /// </remarks>
        public ReadOnlySpan<char> FullPathOfGameDirectory => this.GameExecutablePath.AsSpan(0, this.GameExecutablePath.Length - RelativePathToExecutablePath.AsSpan(4).Length);
    }
}
