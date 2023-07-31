using System;
using System.IO;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;

namespace Leayal.SnowBreakLauncher.Classes
{
    public sealed class OfficialJsonConfiguration : IDisposable
    {
        private readonly FileStream fs_config;

        public string GetPathToConfigFile() => this.fs_config.Name;

        /// <remarks>This thing shouldn't be used to prevent multiple process instances on Windows. Better using Pipes or Mutexes.</remarks>
        private static bool TrySingleInstance(string filepath, [NotNullWhen(true)] out OfficialJsonConfiguration? instance)
        {
            OfficialJsonConfiguration? result = null;
            try
            {
                result = new OfficialJsonConfiguration(filepath);
                result.fs_config.Lock(0, 0);

                instance = result;
                return true;
            }
            catch (Exception ex)
            {
                result?.Dispose();
                result = null;
                if (ex is not IOException) throw;
            }
            instance = result;
            return false;
        }

        public OfficialJsonConfiguration(string filepath)
        {
            this.fs_config = new FileStream(filepath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            this.fs_config.Position = 0;
        }

        /// <summary>Returns path to game client installation.</summary>
        public string GameClientInstallationPath
        {
            get
            {
                try
                {
                    this.fs_config.Position = 0;
                    using (var jsonConf = JsonDocument.Parse(this.fs_config))
                    {
                        if (jsonConf.RootElement.TryGetProperty("dataPath", out var prop_dataPath) && prop_dataPath.ValueKind == JsonValueKind.String)
                        {
                            return prop_dataPath.GetString() ?? string.Empty;
                        }
                    }
                }
                catch (JsonException) { }
                return string.Empty;
            }
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                // This whole mess below is to keep other configuration values intact.
                // Only changing "dataPath" value.
                this.fs_config.Position = 0;
                bool shouldUpdate = false;
                JsonElement? cloned = null;
                try
                {
                    using (var jsonConf = JsonDocument.Parse(this.fs_config))
                    {
                        if (jsonConf.RootElement.TryGetProperty("dataPath", out var prop_dataPath) && prop_dataPath.ValueKind == JsonValueKind.String)
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
                    using (var jsonWriter = new Utf8JsonWriter(this.fs_config, new JsonWriterOptions() { Indented = false }))
                    {
                        jsonWriter.WriteStartObject();
                        if (cloned.HasValue)
                        {
                            foreach (var obj in cloned.Value.EnumerateObject())
                            {
                                if (string.Equals(obj.Name, "dataPath", StringComparison.Ordinal))
                                {
                                    jsonWriter.WriteString("dataPath", value);
                                }
                                else
                                {
                                    obj.WriteTo(jsonWriter);
                                }
                            }
                        }
                        else
                        {
                            jsonWriter.WriteString("dataPath", value);
                        }
                        jsonWriter.WriteEndObject();
                        jsonWriter.Flush();
                    }
                }
            }
        }

        public void Dispose() => this.fs_config.Dispose();
    }
}
