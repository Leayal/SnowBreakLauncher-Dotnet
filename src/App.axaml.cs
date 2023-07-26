using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Leayal.SnowBreakLauncher.Classes;
using Leayal.SnowBreakLauncher.Windows;
using System;
using System.IO;

namespace Leayal.SnowBreakLauncher;

public partial class App : Application
{
    public readonly OfficialJsonConfiguration LauncherConfig;
    internal readonly Program.InstanceController? ProcessInstance;

    public App() : base()
    {
        // Hardcoded to use the "preference.json" in the same folder.
        // This means after-build:
        // - Either copies the launcher's files to the game's directory (locating its "preference.json" file).
        // - Or copies the file "preference.json" from the official launcher's folder to this launcher's folder, then edit the copied json file to correct the game client's location path.
        this.LauncherConfig = new OfficialJsonConfiguration(Path.GetFullPath("preference.json", AppContext.BaseDirectory));
    }

    internal App(Program.InstanceController processInstance) : this()
    {
        this.ProcessInstance = processInstance;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(this.LauncherConfig);
        }

        base.OnFrameworkInitializationCompleted();
    }
}