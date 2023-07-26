using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
