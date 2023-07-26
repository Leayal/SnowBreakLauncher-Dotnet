using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher.Snowbreak;

class GameDataManager
{
    private readonly GameManager manager;
    internal GameDataManager(GameManager manager)
    {
        this.manager = manager;
    }

    public string GetFullPath(ReadOnlySpan<char> relativePath) => Path.Join(this.manager.FullPathOfInstallationDirectory, relativePath);

    public GameClientManifestData GetLocalManifest()
    {
        var localManifestPath = GetFullPath("manifest.json");
        if (!File.Exists(localManifestPath)) throw new FileNotFoundException(null, localManifestPath);
        return GameClientManifestData.CreateFromLocalFile(localManifestPath);
    }

}
