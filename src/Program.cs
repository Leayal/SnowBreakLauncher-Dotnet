using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Leayal.SnowBreakLauncher.Windows;
using System;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using Leayal.SnowBreakLauncher.Snowbreak;
using Avalonia.Vulkan;

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
    public static AppBuilder? BuildAvaloniaApp(InstanceController processInstance, string? proxyUrl)
    {
        var builder = AppBuilder.Configure<App>(() => new App(processInstance))
            // The options below will be ignored by Avalonia if the operating system running this launcher on non-Windows OS.
            .With(new VulkanOptions()
            {
#if DEBUG
                VulkanInstanceCreationOptions = new VulkanInstanceCreationOptions() { UseDebug = true },
#endif
                VulkanDeviceCreationOptions = new VulkanDeviceCreationOptions() { PreferDiscreteGpu = true }
            })
            .With(new Win32PlatformOptions()
            {
                CompositionMode = new Win32CompositionMode[] { Win32CompositionMode.WinUIComposition, Win32CompositionMode.DirectComposition, Win32CompositionMode.LowLatencyDxgiSwapChain, Win32CompositionMode.RedirectionSurface },
                RenderingMode = new Win32RenderingMode[] { Win32RenderingMode.Vulkan, Win32RenderingMode.AngleEgl, Win32RenderingMode.Wgl, Win32RenderingMode.Software }
            })
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
            /*
            if (args.Length == 2 && string.Equals(args[0], "--update-launcher", StringComparison.OrdinalIgnoreCase))
            {
                var targetFilename = args[1];
                // Begin swapping file
                return;
            }
            */
            base.OnRemoteProcessRun(processId, args);
        }

        protected override void OnStartupFirstInstance(string[] args)
        {
            string? proxyUrl = null;
            try
            {
                var argCount = args.Length;
                if (argCount != 0)
                {
                    ReadOnlySpan<string> spanOfArgs = args;
                    for (int i = 0; i < argCount; i++)
                    {
                        ref readonly var arg = ref spanOfArgs[i];
                        if (string.Equals(arg, "--proxy", StringComparison.OrdinalIgnoreCase))
                        {
                            proxyUrl = spanOfArgs[++i];
                        }
                    }

                }
            }
            catch { }

            var app = BuildAvaloniaApp(this, proxyUrl);
            if (app == null) return;

            Environment.ExitCode = app.StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);

            // When shutting down the launcher, closing the file handles, too.
            if (!OperatingSystem.IsWindows()) GameClientManifestData.CloseAllHandles();
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
