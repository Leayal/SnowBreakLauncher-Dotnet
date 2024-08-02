using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Leayal.SnowBreakLauncher.Snowbreak;
using Leayal.SnowBreakLauncher.Windows;
using System;

namespace Leayal.SnowBreakLauncher;

public partial class LauncherSettings : Window
{
    private bool isInDialog;
    public LauncherSettings()
    {
        this.isInDialog = false;
        InitializeComponent();

        if (App.Current is App app)
        {
            var conf = app.LeaLauncherConfig;
            this.CheckBox_Networking_UseDoH.IsChecked = conf.Networking_UseDoH;
            this.CheckBox_AllowFetchingManifestFromOfficial.IsChecked = conf.AllowFetchingOfficialLauncherManifestData;
            this.CheckBox_AllowFetchingManifestFromOfficialInMemory.IsChecked = conf.AllowFetchingOfficialLauncherManifestDataInMemory;
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
                var val_CheckBox_Networking_UseDoH = (this.CheckBox_Networking_UseDoH.IsChecked == true);
                conf.Networking_UseDoH = val_CheckBox_Networking_UseDoH;

                conf.AllowFetchingOfficialLauncherManifestData = (this.CheckBox_AllowFetchingManifestFromOfficial.IsChecked == true);
                conf.AllowFetchingOfficialLauncherManifestDataInMemory = (this.CheckBox_AllowFetchingManifestFromOfficialInMemory.IsChecked == true);

                SnowBreakHttpClient.Instance.EnableDnsOverHttps = val_CheckBox_Networking_UseDoH;

                conf.Save();
            }
            this.Close(true);
        }
        catch (Exception ex)
        {
            await MainWindow.ShowErrorMsgBox(this, ex);
        }
    }
}