using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Runtime.CompilerServices;

namespace Leayal.SnowBreakLauncher.Snowbreak;

public interface INewsItem
{
    public string link { get; }
}

public interface INewsBannerItem : INewsItem
{
    public string img { get; }
}

public interface INewsInlineTextItem : INewsItem
{
    public string time { get; }
    public string title { get; }
}

public readonly struct LauncherNewsHttpResponse : IDisposable
{
    private readonly JsonDocument doc;

    public LauncherNewsHttpResponse(string jsonData)
    {
        this.doc = JsonDocument.Parse(jsonData, new JsonDocumentOptions() { CommentHandling = JsonCommentHandling.Skip });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetChannel(JsonElement element, out JsonElement channel)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TryGetProperty(JsonElement element, string name, out JsonElement property) => (element.TryGetProperty(name, out property) && property.ValueKind == JsonValueKind.Object);

        JsonElement prop_languageChannel;
        if (TryGetProperty(element, "en_US", out prop_languageChannel))
        {
            channel = prop_languageChannel;
            return true;
        }
        else if (TryGetProperty(element, "default", out prop_languageChannel))
        {
            channel = prop_languageChannel;
            return true;
        }
        channel = default;
        return false;
    }

    public int BannerCount
    {
        get
        {
            if (TryGetChannel(this.doc.RootElement, out var prop_languageChannel)
                && prop_languageChannel.TryGetProperty("banner", out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                return prop.GetArrayLength();
            }
            return -1;
        }
    }

    public IEnumerable<INewsBannerItem> Banners
    {
        get
        {
            if (TryGetChannel(this.doc.RootElement, out var prop_languageChannel)
                && prop_languageChannel.TryGetProperty("banner", out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.EnumerateArray())
                {
                    yield return new Item(in item);
                }
            }
        }
    }

    public int NoticeCount
    {
        get
        {
            if (TryGetChannel(this.doc.RootElement, out var prop_languageChannel)
                && prop_languageChannel.TryGetProperty("announcement", out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                return prop.GetArrayLength();
            }
            return -1;
        }
    }

    public IEnumerable<INewsInlineTextItem> Notices
    {
        get
        {
            if (TryGetChannel(this.doc.RootElement, out var prop_languageChannel)
                && prop_languageChannel.TryGetProperty("announcement", out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.EnumerateArray())
                {
                    yield return new Item(in item);
                }
            }
        }
    }

    public int EventCount
    {
        get
        {
            if (TryGetChannel(this.doc.RootElement, out var prop_languageChannel)
                && prop_languageChannel.TryGetProperty("activities", out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                return prop.GetArrayLength();
            }
            return -1;
        }
    }

    public IEnumerable<INewsInlineTextItem> Events
    {
        get
        {
            if (TryGetChannel(this.doc.RootElement, out var prop_languageChannel)
                && prop_languageChannel.TryGetProperty("activities", out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.EnumerateArray())
                {
                    yield return new Item(in item);
                }
            }
        }
    }

    public void Dispose() => this.doc.Dispose();

    readonly record struct Item : INewsBannerItem, INewsInlineTextItem
    {
        private readonly JsonElement element;

        public Item(in JsonElement element)
        {
            this.element = element;
        }

        public readonly string img => this.GetString();

        public readonly string link => this.GetString();

        public readonly string time => this.GetString();

        public readonly string title => this.GetString();

        private readonly string GetString([CallerMemberName] string? propertyName = null)
        {
            if (this.element.TryGetProperty(propertyName ?? string.Empty, out var prop) && prop.ValueKind == JsonValueKind.String) return prop.GetString() ?? string.Empty;
            return string.Empty;
        }
    }
}
