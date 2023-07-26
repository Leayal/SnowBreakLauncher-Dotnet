using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher.Snowbreak
{
    sealed class GameUpdater
    {
        private readonly GameManager manager;
        

        internal GameUpdater(GameManager manager)
        {
            this.manager = manager;
        }

        public async Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            var mgr = this.manager;
            var httpClient = SnowBreakHttpClient.Instance;
            using (var localManifest = mgr.Files.GetLocalManifest())
            using (var manifestData = await httpClient.GetGameClientManifestAsync(cancellationToken))
            {
                return !string.Equals(localManifest.version, manifestData.version, StringComparison.Ordinal);
            }
        }

        public async Task Task_UpdateClientAsync(CancellationToken cancellationToken = default)
        {
            var mgr = this.manager;
            var httpClient = SnowBreakHttpClient.Instance;

            var localManifest = mgr.Files.GetLocalManifest();
            var manifestData = await httpClient.GetGameClientManifestAsync(cancellationToken);

            var bufferedLocalFileTable = FrozenDictionary.ToFrozenDictionary(localManifest.GetPaks(), pak => pak.name, StringComparer.OrdinalIgnoreCase);

            // Determine which file needs to be "updated" (it's actually a redownload anyway)
            var needUpdatedOnes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pak in manifestData.GetPaks())
            {
                if (!bufferedLocalFileTable.TryGetValue(pak.name, out var localPakInfo))
                {
                    // Not found in dictionary, probe for physical file.
                    if (File.Exists(""))
                    {
                        // File is indeed exists, probe for its CRC and file size to determine if the file is okay or latest, or requires to be updated.

                    }
                    else
                    {
                        // File also doesn't exist on disk, add it to download list and then move on.
                        needUpdatedOnes.Add(pak.name);
                        continue;
                    }
                }
            }
        }
    }
}
