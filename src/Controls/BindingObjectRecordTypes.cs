using Leayal.SnowBreakLauncher.Snowbreak;

namespace Leayal.SnowBreakLauncher.Controls
{
    public class NewsInlineTextWrapper : INewsInlineTextItem
    {
        public string time { get; init; } = string.Empty;

        public string title { get; init; } = string.Empty;

        public string link { get; init; } = string.Empty;
    }
}
