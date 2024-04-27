using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;

namespace Leayal.SnowBreakLauncher.Classes
{
    public sealed class LeaLauncherConfiguration : IDisposable
    {
        private readonly FileStream fs;

        public LeaLauncherConfiguration(string filepath)
        {
            this.fs = new FileStream(filepath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 4096, false);
#pragma warning disable CA1416 // Validate platform compatibility
            this.WinePath = string.Empty;
#pragma warning restore CA1416 // Validate platform compatibility
            this.Reload();
        }

        [UnsupportedOSPlatform("windows", "Wine isn't for Windows OSs")]
        /// <summary>Get or sets path to wine executable.</summary>
        public string WinePath { get; set; }

        [UnsupportedOSPlatform("windows", "Wine isn't for Windows OSs")]
        /// <summary>Get or sets 'wine start' switch: /unix.</summary>
        public bool WineUseUnixFileSystem { get; set; }

        /// <summary>Get or sets whether the launcher should use DNS over HTTPS.</summary>
        /// <remarks>Default is enabled. However, on system that have system-wide secure DNS, enabling this would make performance worse due to additional useless DNS resolve, disable this built-in DoH to make use of system-wide secure DNS instead.</remarks>
        public bool Networking_UseDoH { get; set; }

#pragma warning disable CA1416 // Validate platform compatibility

        public void Reload()
        {
            try
            {
                this.fs.Position = 0;
                using (var jsonConf = JsonDocument.Parse(this.fs))
                {
                    var rootEle = jsonConf.RootElement;
                    if (rootEle.TryGetProperty("winePath", out var prop_winePath) && prop_winePath.ValueKind == JsonValueKind.String)
                    {
                        this.WinePath = prop_winePath.GetString() ?? string.Empty;
                    }
                    else
                    {
                        this.WinePath = string.Empty;
                    }

                    if (rootEle.TryGetProperty("wineUseUnixFS", out var prop_wineUseUnixFS))
                    {
                        this.WineUseUnixFileSystem = ToBool(in prop_wineUseUnixFS, true);
                    }
                    else
                    {
                        this.WineUseUnixFileSystem = true;
                    }

                    if (rootEle.TryGetProperty("networking_useDoH", out var prop_networking_useDoH))
                    {
                        this.Networking_UseDoH = ToBool(in prop_wineUseUnixFS, true);
                    }
                    else
                    {
                        this.Networking_UseDoH = true;
                    }
                }
            }
            catch (JsonException)
            {
                this.WinePath = string.Empty;
                this.WineUseUnixFileSystem = true;
                this.Networking_UseDoH = true;
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
                if (!this.Networking_UseDoH)
                {
                    jsonWriter.WriteBoolean("networking_useDoH", false);
                }
                jsonWriter.WriteEndObject();
                jsonWriter.Flush();
                writtenLen = jsonWriter.BytesCommitted;
            }
            this.fs.SetLength(writtenLen);
        }
#pragma warning restore CA1416 // Validate platform compatibility

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
