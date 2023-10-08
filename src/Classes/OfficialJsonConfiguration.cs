using Avalonia.Controls.Converters;
using System;
using System.IO;
using System.Text.Json;

namespace Leayal.SnowBreakLauncher.Classes
{
    public sealed class OfficialJsonConfiguration : IDisposable
    {
        private readonly FileStream fs_config;

        public string GetPathToConfigFile() => this.fs_config.Name;

        public OfficialJsonConfiguration(string filepath)
        {
            this.fs_config = new FileStream(filepath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            this.fs_config.Position = 0;
        }

        /// <summary>Returns path to game client installation.</summary>
        public string GameClientInstallationPath
        {
            get => this.GetValue("dataPath");
            set => this.SetValue("dataPath", value);
        }

        private string GetValue(string key)
        {
            try
            {
                this.fs_config.Position = 0;
                using (var jsonConf = JsonDocument.Parse(this.fs_config))
                {
                    if (jsonConf.RootElement.TryGetProperty(key, out var prop_dataPath) && prop_dataPath.ValueKind == JsonValueKind.String)
                    {
                        return prop_dataPath.GetString() ?? string.Empty;
                    }
                }
            }
            catch (JsonException) { }
            return string.Empty;
        }

        private void SetValue(string key, string value)
        {
            ArgumentNullException.ThrowIfNull(value);

            // This whole mess below is to keep other configuration values intact.
            this.fs_config.Position = 0;
            bool shouldUpdate = false;
            JsonElement? cloned = null;
            try
            {
                using (var jsonConf = JsonDocument.Parse(this.fs_config))
                {
                    if (jsonConf.RootElement.TryGetProperty(key, out var prop_dataPath) && prop_dataPath.ValueKind == JsonValueKind.String)
                    {
                        if (!string.Equals(prop_dataPath.GetString() ?? string.Empty, value, StringComparison.OrdinalIgnoreCase))
                        {
                            shouldUpdate = true;
                            cloned = jsonConf.RootElement.Clone();
                        }
                    }
                }
            }
            catch (JsonException)
            {
                cloned = null;
                shouldUpdate = true;
            }
            if (shouldUpdate)
            {
                this.fs_config.Position = 0;
                long writtenLen = 0;
                using (var jsonWriter = new Utf8JsonWriter(this.fs_config, new JsonWriterOptions() { Indented = false }))
                {
                    jsonWriter.WriteStartObject();
                    if (cloned.HasValue)
                    {
                        foreach (var obj in cloned.Value.EnumerateObject())
                        {
                            if (string.Equals(obj.Name, key, StringComparison.Ordinal))
                            {
                                jsonWriter.WriteString(key, value);
                            }
                            else
                            {
                                obj.WriteTo(jsonWriter);
                            }
                        }
                    }
                    else
                    {
                        jsonWriter.WriteString(key, value);
                    }
                    jsonWriter.WriteEndObject();
                    jsonWriter.Flush();
                    writtenLen = jsonWriter.BytesCommitted;
                }
                this.fs_config.SetLength(writtenLen);
            }
        }

        public void Dispose() => this.fs_config.Dispose();
    }
}
