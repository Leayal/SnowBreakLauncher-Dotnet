using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Leayal.SnowBreakLauncher.Classes;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Threading;

namespace Leayal.SnowBreakLauncher;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        /*
        if (args.Length == 2 && string.Equals(args[0], "--update-launcher", StringComparison.OrdinalIgnoreCase))
        {
            var targetFilename = args[1];
            // Begin swapping file
            return;
        }
        if (OfficialJsonConfiguration.TrySingleInstance(@"F:\SnowBreak\Snow\preference.json", out var config))
        {
            BuildAvaloniaApp(config).StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnMainWindowClose);
        }
        else
        {
            // Simply exit
        }
        */
        using (var instance = new InstanceController())
        {
            instance.Run(args);
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AppBuilder BuildAvaloniaApp(InstanceController processInstance)
    {
        var builder = AppBuilder.Configure<App>(() => new App(processInstance))
            // .UseManagedSystemDialogs()
            .UsePlatformDetect()
            .WithInterFont();
#if DEBUG
        builder.LogToTrace();
#endif
        return builder;
    }

    internal class InstanceController : ApplicationController
    {
        public InstanceController() : base("Leayal.SnowBreakLauncher.Desktop")
        {
        }

        protected override void OnRemoteProcessRun(int processId, string[] args)
        {
            if (args.Length == 2 && string.Equals(args[0], "--update-launcher", StringComparison.OrdinalIgnoreCase))
            {
                var targetFilename = args[1];
                // Begin swapping file
                return;
            }
            base.OnRemoteProcessRun(processId, args);
        }

        protected override void OnStartupFirstInstance(string[] args)
        {
            Environment.ExitCode = BuildAvaloniaApp(this).StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
        }

        private static void ShutdownProcess()
        {
            if (Application.Current is App app && app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                if (lifetime.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.Close();
                }
                else
                {
                    lifetime.Shutdown();
                }
            }
            else
            {
                Dispatcher.UIThread.BeginInvokeShutdown(DispatcherPriority.MaxValue);
            }
        }

        protected override void OnStartupNextInstance(int processId, string[] args)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {

                if (args.Length == 1 && string.Equals(args[0], "--exit-to-update-launcher", StringComparison.OrdinalIgnoreCase))
                {
                    ShutdownProcess();
                    return;
                }

                if (Application.Current is App app && app.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                {
                    if (lifetime.MainWindow is MainWindow mainWindow)
                    {
                        if (!mainWindow.IsEffectivelyVisible)
                        {
                            mainWindow.Show();
                        }
                        if (!mainWindow.IsActive)
                        {
                            if (!Leayal.Shared.Windows.UnmanagedWindowsHelper.SetForegroundWindow(mainWindow))
                            {
                                mainWindow.Activate();
                            }
                        }
                    }
                }
            }
            else
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    OnStartupNextInstance(processId, args);
                });
            }
        }
    }
}
