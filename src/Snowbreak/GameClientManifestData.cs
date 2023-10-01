using System;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Leayal.SnowBreakLauncher.Snowbreak;

public readonly struct GameClientManifestData : IDisposable
{
    public static GameClientManifestData CreateFromLocalFile(string filepath) => new GameClientManifestData(JsonDocument.Parse(System.IO.File.ReadAllText(filepath)));

    private readonly JsonDocument _doc;

    public GameClientManifestData(JsonDocument doc) 
    {
        this._doc = doc;
    }

    public readonly string? projectVersion => this.GetString();
    public readonly string? version => this.GetString();

    public readonly string? pathOffset => this.GetString();

    public readonly IReadOnlyDictionary<string, PakEntry> GetPakDictionary()
    {
        var dictionary = new Dictionary<string, PakEntry>(this.PakCount, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        if (this._doc.RootElement.TryGetProperty("paks", out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
            foreach (var pak in prop.EnumerateArray())
            {
                var pakInfo = new PakEntry(in pak);
                var key = pakInfo.name;
                if (dictionary.TryGetValue(key, out var oldEntry))
                {
                    if (!string.IsNullOrEmpty(pakInfo.hash) && !string.Equals(oldEntry.hash, pakInfo.hash, StringComparison.OrdinalIgnoreCase))
                    {
                        if (pakInfo.bPrimary == true || string.IsNullOrEmpty(oldEntry.hash))
                        {
                            dictionary[pakInfo.name] = pakInfo;
                        }
                    }
                }
                else
                {
                    dictionary.Add(pakInfo.name, pakInfo);
                }
            }
        }
#if NET8_0_OR_GREATER
        return FrozenDictionary.ToFrozenDictionary(dictionary, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
#else
        return dictionary.AsReadOnly();
#endif
    }

    public readonly IEnumerable<PakEntry> GetPaks()
    {
        if (this._doc.RootElement.TryGetProperty("paks", out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
            foreach (var pak in prop.EnumerateArray())
            {
                yield return new PakEntry(in pak);
            }
        }
    }

    internal readonly JsonElement.ObjectEnumerator GetRawProperies()
    {
        var jsonElement = this._doc.RootElement;
        if (jsonElement.ValueKind == JsonValueKind.Object)
        {
            return jsonElement.EnumerateObject();
        }

        // Can't be here anyway unless the manifest file is a troll one.
        return default;
    }

    public readonly int PakCount
    {
        get
        {
            if (this._doc.RootElement.TryGetProperty("paks", out var prop) && prop.ValueKind == JsonValueKind.Array)
            {
                return prop.GetArrayLength();
            }

            return -1;
        }
    }

    private readonly string? GetString([CallerMemberName] string? name = null)
    {
        if (this._doc.RootElement.TryGetProperty(name ?? string.Empty, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    public readonly void Dispose() => this._doc.Dispose();
}
