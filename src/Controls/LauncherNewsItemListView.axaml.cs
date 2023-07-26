using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using System;

namespace Leayal.SnowBreakLauncher.Controls;

public partial class LauncherNewsItemListView : UserControl
{
    public LauncherNewsItemListView()
    {
        InitializeComponent();
    }

    public System.Collections.Generic.IReadOnlyList<NewsInlineTextWrapper> ItemsSource
    {
        set
        {
            if (this.Content is ListBox lb)
            {
                lb.ItemsSource = value;
            }
        }
    }

    private void Listbox_BlockSelection(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems != null && e.AddedItems.Count != 0 && sender is ListBox lb)
        {
            lb.UnselectAll();
        }
    }

    private void NewsItem_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is TextBlock tblock1 && tblock1.DataContext is NewsInlineTextWrapper data1)
        {
            this.NewsItemPressed?.Invoke(data1);
        }
        else if (e.Source is TextBlock tblock2 && tblock2.DataContext is NewsInlineTextWrapper data2)
        {
            this.NewsItemPressed?.Invoke(data2);
        }
    }

    public event Action<NewsInlineTextWrapper>? NewsItemPressed;
}