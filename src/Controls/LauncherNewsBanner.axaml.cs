using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Leayal.SnowBreakLauncher.Classes;
using System;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher.Controls;

public partial class LauncherNewsBanner : UserControl
{
    private IDisposable? subbed;
    private readonly IObservable<bool> observer;
    private readonly string img, url;

    public LauncherNewsBanner(string img, string url)
    {
        this.img = img;
        this.url = url;
        InitializeComponent();
        this.observer = this.BannerImage.GetObservable(Image.IsVisibleProperty);
        this.BannerImage.PointerPressed += BannerImage_PointerPressed;
        this.BannerImage.PointerEntered += BannerImage_PointerEntered;
        this.BannerImage.PointerExited += BannerImage_OnPointerExited;
    }

    private void BannerImage_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        try
        {
            Leayal.Shared.Windows.WindowsExplorerHelper.OpenUrlWithDefaultBrowser(this.url);
        }
        catch { }
    }

    private void OnBannerImageVisibleChanged(bool isVisible)
    {
        this.LoadingSpin.IsVisible = !isVisible;
    }

    protected override async void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        this.OnBannerImageVisibleChanged(this.BannerImage.IsVisible);
        this.subbed = observer.Subscribe(this.OnBannerImageVisibleChanged);

        var localFile = await RemoteResourcePersistentCache.Instance.GetResource(new Uri(this.img));
        this.BannerImage.Source = new Bitmap(localFile);
        this.BannerImage.IsVisible = true;
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
        this.BannerImage.IsVisible = false;
        var oldImg = this.BannerImage.Source;
        this.BannerImage.Source = null;
        (oldImg as IDisposable)?.Dispose();
        this.subbed?.Dispose();
        base.OnUnloaded(e);
    }
}