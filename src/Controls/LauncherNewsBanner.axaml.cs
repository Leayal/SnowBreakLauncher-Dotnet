using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Leayal.SnowBreakLauncher.Classes;
using Leayal.SnowBreakLauncher.Windows;
using System;

namespace Leayal.SnowBreakLauncher.Controls;

public partial class LauncherNewsBanner : UserControl
{
    private readonly string img, url;
    private readonly ProgressBar LoadingSpin;
    private readonly Image BannerImage;

    public LauncherNewsBanner(string img, string url)
    {
        this.img = img;
        this.url = url;

        // <Image Name="BannerImage" IsVisible="False" Stretch="Uniform" StretchDirection="Both" Cursor="Hand" />
        // <ProgressBar IsIndeterminate="True" Name="LoadingSpin" HorizontalAlignment="Stretch" VerticalAlignment="Center" MinHeight="30" Padding="2" />
        this.LoadingSpin = new ProgressBar()
        {
            IsIndeterminate = true,
            IsHitTestVisible = false,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            MinHeight = 30,
            Padding = new Thickness(10, 2, 10, 2)
        };
        this.BannerImage = new Image()
        {
            Stretch = Avalonia.Media.Stretch.Uniform,
            StretchDirection = Avalonia.Media.StretchDirection.Both
        };

        InitializeComponent();
        this.Content = this.LoadingSpin;

        // Clickable.On(this.BannerImage).Click += this.BannerImage_Click;
        this.BannerImage.OnClick(this.BannerImage_Click);
        this.BannerImage.PointerEntered += BannerImage_PointerEntered;
        this.BannerImage.PointerExited += BannerImage_OnPointerExited;
    }

    private void BannerImage_Click(Image sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            MainWindow.OpenURLWithDefaultBrowser(this.url);
        }
        catch { }
    }

    protected override async void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);

        this.BannerImage.Cursor = EmbedResources.Cursor_Hand.Value;

        var localFile = await RemoteResourcePersistentCache.Instance.GetResource(new Uri(this.img));
        this.BannerImage.Source = new Bitmap(localFile);
        this.Content = this.BannerImage;
    }

    private void BannerImage_PointerEntered(object? sender, PointerEventArgs e)
    {
        base.OnPointerEntered(e);
    }

    private void BannerImage_OnPointerExited(object? sender, PointerEventArgs e)
    {
        base.OnPointerExited(e);
    }

    protected override void OnUnloaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        this.Content = this.LoadingSpin;
        var oldImg = this.BannerImage.Source;
        this.BannerImage.Source = null;
        this.BannerImage.Cursor = Cursor.Default;
        (oldImg as IDisposable)?.Dispose();
        base.OnUnloaded(e);
    }
}