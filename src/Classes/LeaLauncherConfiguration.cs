using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;

namespace Leayal.SnowBreakLauncher.Classes
{
    [UnsupportedOSPlatform("windows")]
    public sealed class LeaLauncherConfiguration : IDisposable
    {
        private readonly FileStream fs;

        public LeaLauncherConfiguration(string filepath)
        {
            this.fs = new FileStream(filepath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 4096, false);
            this.WinePath = string.Empty;
            this.Reload();
        }

        /// <summary>Get or sets path to wine executable.</summary>
        public string WinePath { get; set; }

        /// <summary>Get or sets 'wine start' switch: /unix.</summary>
        public bool WineUseUnixFileSystem { get; set; }

        public void Reload()
        {
            try
            {
                this.fs.Position = 0;
                using (var jsonConf = JsonDocument.Parse(this.fs))
                {
                    if (jsonConf.RootElement.TryGetProperty("winePath", out var prop_winePath) && prop_winePath.ValueKind == JsonValueKind.String)
                    {
                        this.WinePath = prop_winePath.GetString() ?? string.Empty;
                    }
                    else
                    {
                        this.WinePath = string.Empty;
                    }

                    if (jsonConf.RootElement.TryGetProperty("wineUseUnixFS", out var prop_wineUseUnixFS))
                    {
                        this.WineUseUnixFileSystem = ToBool(in prop_wineUseUnixFS, true);
                    }
                    else
                    {
                        this.WineUseUnixFileSystem = true;
                    }
                }
            }
            catch (JsonException)
            {
                this.WinePath = string.Empty;
                this.WineUseUnixFileSystem = true;
            }
        }

        public void Save()
        {
            this.fs.Position = 0;
            long writtenLen = 0;
            using (var jsonWriter = new Utf8JsonWriter(this.fs, new JsonWriterOptions() { Indented = false }))
            {
                jsonWriter.WriteStartObject();
                if (!string.IsNullOrWhiteSpace(this.WinePath))
                {
                    jsonWriter.WriteString("winePath", this.WinePath);
                }
                if (!this.WineUseUnixFileSystem)
                {
                    jsonWriter.WriteBoolean("wineUseUnixFS", false);
                }
                jsonWriter.WriteEndObject();
                jsonWriter.Flush();
                writtenLen = jsonWriter.BytesCommitted;
            }
            this.fs.SetLength(writtenLen);
        }

        private static bool ToBool(in JsonElement element, bool defaultValue)
        {
            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => defaultValue
            };
        }

        public void Dispose() => this.fs.Dispose();
    }
}
