using System;
using System.Collections.Generic;
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
