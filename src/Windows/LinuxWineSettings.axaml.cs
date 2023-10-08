using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Leayal.SnowBreakLauncher.Windows;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Leayal.SnowBreakLauncher;

[UnsupportedOSPlatform("windows")]
public partial class LinuxWineSettings : Window
{
    private bool isInDialog;
    public LinuxWineSettings()
    {
        this.isInDialog = false;
        InitializeComponent();
        if (App.Current is App app)
        {
            var conf = app.LeaLauncherConfig;
            this.CheckBox_WineUnix.IsChecked = conf.WineUseUnixFileSystem;
            var winePath = conf.WinePath;
            this.TextBox_WinePath.Text = string.IsNullOrWhiteSpace(winePath) ? string.Empty : winePath;
        }
    }

    private void CloseBtn_Click(object? sender, RoutedEventArgs e)
    {
        this.Close(false);
    }

    private async void SaveBtn_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (App.Current is App app)
            {
                var conf = app.LeaLauncherConfig;
                if (this.TextBox_WinePath.Text is string str && !string.IsNullOrWhiteSpace(str))
                {
                    conf.WinePath = str;
                }
                else
                {
                    conf.WinePath = string.Empty;
                }

                conf.WineUseUnixFileSystem = (this.CheckBox_WineUnix.IsChecked == true);
                conf.Save();
            }
            this.Close(true);
        }
        catch (Exception ex)
        {
            await MainWindow.ShowErrorMsgBox(this, ex);
        }
    }

    private async void BrowseForWineBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (this.isInDialog) return;
        this.isInDialog = true;
        try
        {
            var openFileOpts = new FilePickerOpenOptions()
            {
                AllowMultiple = false,
                Title = "Browse for Wine executable file",
                FileTypeFilter = new List<FilePickerFileType>(2)
                {
                    new FilePickerFileType("Wine Executable File") { Patterns = new string[] { "wine" } },
                    FilePickerFileTypes.All
                }
            };
            var results = await StorageProvider.OpenFilePickerAsync(openFileOpts);
            if (results == null || results.Count == 0) return;

            var path = results[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
            {
                await MainWindow.ShowInfoMsgBox(this, "The file you selected is not a local file on your machine.", "Invalid item selected");
                return;
            }
            else if (string.IsNullOrWhiteSpace(path))
            {
                await MainWindow.ShowInfoMsgBox(this, "The path you selected is invalid.", "Invalid item selected");
                return;
            }
            this.TextBox_WinePath.Text = path;
        }
        finally
        {
            this.isInDialog = false;
        }
    }
}