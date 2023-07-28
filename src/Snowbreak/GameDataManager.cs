using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher.Snowbreak;

class GameDataManager
{
    private readonly GameManager manager;
    internal GameDataManager(GameManager manager)
    {
        this.manager = manager;
    }

    public string GetFullPath(ReadOnlySpan<char> relativePath)
    {
        if (relativePath.Contains(Path.AltDirectorySeparatorChar))
        {
            if (relativePath.Length < 260)
            {
                Span<char> buffer = stackalloc char[relativePath.Length];
                relativePath.CopyTo(buffer);
                buffer.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                return Path.Join(this.manager.FullPathOfGameDirectory, buffer);
            }
            else
            {
                var borrowed = ArrayPool<char>.Shared.Rent(relativePath.Length);
                try
                {
                    var buffer = new Span<char>(borrowed, 0, relativePath.Length);
                    relativePath.CopyTo(buffer);
                    buffer.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                    return Path.Join(this.manager.FullPathOfGameDirectory, buffer);
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(borrowed, true);
                }
            }
        }

        return Path.Join(this.manager.FullPathOfGameDirectory, relativePath);
    }

    public string PathToManifestJson => Path.Join(this.manager.FullPathOfInstallationDirectory, "manifest.json");

    public GameClientManifestData GetLocalManifest()
    {
        var localManifestPath = this.PathToManifestJson;
        if (!File.Exists(localManifestPath)) throw new FileNotFoundException(null, localManifestPath);
        return GameClientManifestData.CreateFromLocalFile(localManifestPath);
    }

}
