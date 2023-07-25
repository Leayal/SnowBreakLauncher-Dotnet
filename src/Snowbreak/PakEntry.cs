using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher.Snowbreak
{
    public readonly struct PakEntry
    {
        private readonly JsonElement element;

        public PakEntry(in JsonElement element)
        {
            this.element = element;
        }

        public readonly long sizeInBytes => this.GetNumber();
        public readonly long cRC => this.GetNumber();
        public readonly long? fastVerify => this.GetNullableNumber();
        public readonly string name => this.GetString();

        private readonly long GetNumber([CallerMemberName] string? name = null)
        {
            if (this.element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt64();
            }
            return 0;
        }

        private readonly long? GetNullableNumber([CallerMemberName] string? name = null)
        {
            if (this.element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt64();
            }
            return null;
        }

        private readonly string GetString([CallerMemberName] string? name = null)
        {
            if (this.element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? string.Empty;
            }
            return string.Empty;
        }

        public readonly void WriteJsonDataTo(Utf8JsonWriter writer) => this.element.WriteTo(writer);
    }
}
