using System;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Leayal.SnowBreakLauncher.Snowbreak;

public readonly struct PakEntry
{
    public static readonly PakEntry Empty = default;

    private readonly JsonElement element;

    public PakEntry(in JsonElement element)
    {
        this.element = element;
    }

    /* Sample
    {
      "name": "Game/Content/Paks/PAK_Game_Wwise_3-WindowsNoEditor.pak",
      "hash": "7fcce4b0f9859e527e4ca1b3f529b467",
      "sizeInBytes": 106001371,
      "bPrimary": true,
      "base": "bc14c6189f79cfcaabceccbb72fda8c1",
      "diff": "a77e423db993eea365e249ab557d490c",
      "diffSizeBytes": 19006788
    },
    
    For non-primary

    {
      "name": "Game/Content/Paks/PAK_Game_Wwise_3-WindowsNoEditor_0_P.pak",
      "hash": "8590e0e0c986fe6c53cc96991b4441c2",
      "sizeInBytes": 7165947,
      "bPrimary": true,
      "base": "",
      "diff": "",
      "diffSizeBytes": 0
    },
    */

    // I assume this is whether to check if the file need to match hash, or simply existence is enough
    // true => requires hash check and exact match
    // false => file just needs to exist, hash mismatch allowed
    public readonly bool? bPrimary => this.GetNullableBoolean();

    public readonly long sizeInBytes => this.GetLongNumber();
    /// <summary>Gets the MD5 hash of the file.</summary>
    public readonly string hash => this.GetString();
    /// <summary>Unix time of file's modified date (expressed in seconds)</summary>
    /// <remarks>Use <seealso cref="DateTimeOffset.FromUnixTimeSeconds"/> to parse this number.</remarks>
    public readonly long? fastVerify => this.GetNullableNumber();
    public readonly string name => this.GetString();

    public readonly long diffSizeBytes => this.GetLongNumber();
    public readonly string @base => this.GetString("base");
    public readonly string diff => this.GetString();

    private readonly uint GetUIntNumber([CallerMemberName] string? name = null)
    {
        if (this.element.TryGetProperty(name ?? string.Empty, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetUInt32();
        }
        return 0;
    }

    private readonly bool? GetNullableBoolean([CallerMemberName] string? name = null)
    {
        if (!this.element.TryGetProperty(name ?? string.Empty, out var prop)) return null;
        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
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
