using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;

namespace Leayal.SnowBreakLauncher.Snowbreak;

class GameDataManager
{
    private readonly GameManager manager;
    internal GameDataManager(GameManager manager)
    {
        this.manager = manager;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void ReplaceAltDirectorySeparatorCharToDirectorySeparatorChar(in Span<char> buffer)
    {
#if NET8_0_OR_GREATER
        buffer.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
#else
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] == Path.AltDirectorySeparatorChar) buffer[i] = Path.DirectorySeparatorChar;
        }
#endif
    }

    public string GetFullPath(ReadOnlySpan<char> relativePath)
    {
        if (relativePath.Contains(Path.AltDirectorySeparatorChar))
        {
            if (relativePath.Length < 260)
            {
                Span<char> buffer = stackalloc char[relativePath.Length];
                relativePath.CopyTo(buffer);
                buffer = buffer.Slice(0, relativePath.Length);
                ReplaceAltDirectorySeparatorCharToDirectorySeparatorChar(in buffer);
                return Path.Join(this.manager.FullPathOfGameDirectory, buffer);
            }
            else
            {
                var borrowed = ArrayPool<char>.Shared.Rent(relativePath.Length);
                try
                {
                    var buffer = new Span<char>(borrowed, 0, relativePath.Length);
                    relativePath.CopyTo(buffer);
                    buffer = buffer.Slice(0, relativePath.Length);
                    ReplaceAltDirectorySeparatorCharToDirectorySeparatorChar(in buffer);
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

    /// <summary>
    /// 
    /// </summary>
    /// <returns><see langword="null"/> if the local manifest file doesn't exist. Or a <seealso cref="GameClientManifestData"/> contains the data of the local manifest file.</returns>
    public GameClientManifestData? TryGetLocalManifest()
    {
        var localManifestPath = this.PathToManifestJson;
        if (!File.Exists(localManifestPath)) return null;
        return GameClientManifestData.CreateFromLocalFile(localManifestPath);
    }

}
