using System;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Leayal.SnowBreakLauncher.Snowbreak;

public readonly struct PakEntry
{
    private readonly JsonElement element;

    public PakEntry(in JsonElement element)
    {
        this.element = element;
    }

    public readonly long sizeInBytes => this.GetLongNumber();
    public readonly uint cRC => this.GetUIntNumber();
    /// <summary>Unix time of file's modified date (expressed in seconds)</summary>
    /// <remarks>Use <seealso cref="DateTimeOffset.FromUnixTimeSeconds"/> to parse this number.</remarks>
    public readonly long? fastVerify => this.GetNullableNumber();
    public readonly string name => this.GetString();

    private readonly uint GetUIntNumber([CallerMemberName] string? name = null)
    {
        if (this.element.TryGetProperty(name ?? string.Empty, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetUInt32();
        }
        return 0;
    }

    private readonly long GetLongNumber([CallerMemberName] string? name = null)
    {
        if (this.element.TryGetProperty(name ?? string.Empty, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetInt64();
        }
        return 0;
    }

    private readonly long? GetNullableNumber([CallerMemberName] string? name = null)
    {
        if (this.element.TryGetProperty(name ?? string.Empty, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetInt64();
        }
        return null;
    }

    private readonly string GetString([CallerMemberName] string? name = null)
    {
        if (this.element.TryGetProperty(name ?? string.Empty, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    public readonly void WriteJsonDataTo(Utf8JsonWriter writer) => this.element.WriteTo(writer);
}
