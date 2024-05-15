using System;
using System.Collections.Concurrent;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;

namespace Leayal.SnowBreakLauncher.Snowbreak;

public readonly struct GameClientManifestData : IDisposable
{
    #region "| Attempt to workaround the 'File is opened by another process' issue, it's a strange error |"

    // The workaround is simple: Ensure there is only one single File Handle of one particular file.
    // If the same error still occurs, that means either this launcher is running multiple instances (which is questionable...), or there's a process outside of the user's machine using the file.
    // ....Or the FileSystem of the running OS is questionable, my speculation was that .NET runtime couldn't close the file handle properly, leaving it lingering/hanging and any subsequent opening would result in File-In-Use due to overlapped file handle.
    // There is, however, some drawbacks from this: There can only one single write operation at a time (which is no problem as a file shouldn't be overlapping writing anyway)
    // and the File handle will always be opened with Read&Write access and OpenOrCreate mode, and another insignificant drawback is the extra cost of all the checks.

    private static readonly ConcurrentDictionary<string, FileStreamBroker> CachedBrokers = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    // This is a dirty way to clean up.
    [UnsupportedOSPlatform("windows")]
    public static void CloseAllHandles()
    {
        var count = CachedBrokers.Values.Count; // Do not abuse this property, it will walkthrough the whole dictionary per call to count.
        if (count == 0) return;
        var brokers = new FileStreamBroker[count];
        CachedBrokers.Values.CopyTo(brokers, 0);

        CachedBrokers.Clear();

        for (int i = 0; i < count; i++)
        {
            brokers[i].Dispose();
        }
    }

    [UnsupportedOSPlatform("windows")]
    internal static FileStreamBroker OpenFile(string filepath)
    {
        return CachedBrokers.AddOrUpdate(filepath, path => new FileStreamBroker(path), (path, old) =>
        {
            if (old.IsValid) return old;
            old.Dispose();
            return new FileStreamBroker(path);
        });
    }

    internal class FileStreamBroker : IDisposable
    {
        private FileStream? fs;
#if NET8_0_OR_GREATER
        private object locker;
#else
        private readonly object locker;
#endif
        private bool _created, _mapped;
        private MemoryMappedFile? memMappedFile;
        private readonly string filepath;
        // This is "expensive", as the execution may have to wait due to locks. Expensive cost here is the time execution length prolonged due to waitings, not memory.
        private readonly ReaderWriterLockSlim ioLocker;

        public FileStreamBroker(string filepath)
        {
            this.filepath = filepath;
            this.locker = new object();
            this.ioLocker = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        }

        public bool IsValid
        {
            get
            {
                if (!this._created) return false;

                if (this.fs is FileStream writeStream)
                {
                    try
                    {
                        return !writeStream.SafeFileHandle.IsInvalid;
                    }
                    catch
                    {
                        return false;
                    }
                }
                return false;
            }
        }

        private FileStream FactoryFS() => new FileStream(this.filepath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 4096, true);

        private MemoryMappedFile FactoryMap()
        {
            ArgumentNullException.ThrowIfNull(this.fs);
            return MemoryMappedFile.CreateFromFile(this.fs, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);
        }

        private void OpenOrCreate(out FileStream writeStream)
        {
#if NET8_0_OR_GREATER
            writeStream = LazyInitializer.EnsureInitialized<FileStream>(ref this.fs, ref this._created, ref this.locker, this.FactoryFS);
#else
            writeStream = LazyInitializer.EnsureInitialized<FileStream>(ref this.fs, ref this._created, ref Unsafe.AsRef(in this.locker), this.FactoryFS);
#endif
        }

        private bool TryGetMap([NotNullWhen(true)] out MemoryMappedFile? map)
        {
            if (this.fs == null || this.fs.Length == 0)
            {
                map = null;
                return false;
            }
#if NET8_0_OR_GREATER
            map = LazyInitializer.EnsureInitialized<MemoryMappedFile>(ref this.memMappedFile, ref this._mapped, ref this.locker, this.FactoryMap);
#else
            map = LazyInitializer.EnsureInitialized<MemoryMappedFile>(ref this.memMappedFile, ref this._mapped, ref Unsafe.AsRef(in this.locker), this.FactoryMap);
#endif
            try
            {
                var handle = map.SafeMemoryMappedFileHandle;
                if (handle.IsInvalid || handle.IsClosed)
                {
                    map = RefreshMap();
                }
            }
            catch
            {
                map = RefreshMap();
            }
            return true;
        }

        private MemoryMappedFile RefreshMap()
        {
            if (this.fs == null || this.fs.Length == 0) throw new InvalidOperationException();

            var mappedFile = MemoryMappedFile.CreateFromFile(this.fs, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);
            Interlocked.Exchange(ref this.memMappedFile, mappedFile)?.Dispose();
            return mappedFile;
        }

        public void Dispose()
        {
            this.fs?.Dispose();
            this.memMappedFile?.Dispose();
        }

        public Stream OpenRead()
        {
            this.OpenOrCreate(out _);
            if (TryGetMap(out var memMapped))
            {
                this.ioLocker.EnterReadLock();
                var syncContext = SynchronizationContext.Current;
                if (syncContext == null)
                {
                    syncContext = new SynchronizationContext();
                    SynchronizationContext.SetSynchronizationContext(syncContext);
                }
                var readStream = memMapped.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
                return new WrapperReadStream(readStream, this.ioLocker, syncContext);
            }
            else
            {
                return new MemoryStream(Array.Empty<byte>(), 0, 0, false, false);
            }
        }

        public Stream OpenWrite()
        {
            this.OpenOrCreate(out var writeStream);
            this.ioLocker.EnterWriteLock();
            Interlocked.Exchange(ref this.memMappedFile, null)?.Dispose();
            this._mapped = false;
            var syncContext = SynchronizationContext.Current;
            if (syncContext == null)
            {
                syncContext = new SynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(syncContext);
            }
            return new WrapperWriteStream(writeStream, this.ioLocker, syncContext);
        }

        class WrapperWriteStream : WrapperReadStream
        {
            public WrapperWriteStream(Stream baseStream, ReaderWriterLockSlim lockSlim, SynchronizationContext syncContext)
                : base(baseStream, lockSlim, syncContext) { }

            public override void Close()
            {
                base.OriginalClose();
                // We don't close the Filestream, keep it open.
                this.syncContext.Post(new SendOrPostCallback(AttempGoingBackToTheThreadCreatedThisStreamForRelease), this.lockSlim);
            }

            private static void AttempGoingBackToTheThreadCreatedThisStreamForRelease(object? obj)
            {
                if (obj is ReaderWriterLockSlim ioLocker && ioLocker.IsWriteLockHeld)
                {
                    ioLocker.ExitWriteLock();
                }
            }
        }

        class WrapperReadStream : Stream
        {
            public readonly Stream BaseStream;
            protected readonly ReaderWriterLockSlim lockSlim;
            protected readonly SynchronizationContext syncContext;

            public WrapperReadStream(Stream baseStream, ReaderWriterLockSlim lockSlim, SynchronizationContext syncContext)
            {
                this.lockSlim = lockSlim;
                this.BaseStream = baseStream;
                this.syncContext = syncContext;
            }

            protected void OriginalClose() => base.Close();

            public override void Close()
            {
                base.Close();
                this.BaseStream.Close();
                this.syncContext.Post(new SendOrPostCallback(AttempGoingBackToTheThreadCreatedThisStreamForRelease), this.lockSlim);
            }

            private static void AttempGoingBackToTheThreadCreatedThisStreamForRelease(object? obj)
            {
                if (obj is ReaderWriterLockSlim ioLocker && ioLocker.IsReadLockHeld)
                {
                    ioLocker.ExitReadLock();
                }
            }

            public override bool CanRead => BaseStream.CanRead;

            public override bool CanSeek => BaseStream.CanSeek;

            public override bool CanWrite => BaseStream.CanWrite;

            public override long Length => BaseStream.Length;

            public override long Position { get => BaseStream.Position; set => BaseStream.Position = value; }

            public override void Flush()
            {
                BaseStream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return BaseStream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return BaseStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                BaseStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                BaseStream.Write(buffer, offset, count);
            }
        }
    }

#endregion

    public static GameClientManifestData? CreateFromLocalFile(string filepath)
    {
        if (OperatingSystem.IsWindows())
        {
            using (var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 0, false))
            {
                if (fs.Length == 0) return null;
                using (var sr = new StreamReader(fs, bufferSize: 4096))
                {
                    var jsonstring = sr.ReadToEnd();
                    if (string.IsNullOrEmpty(jsonstring)) return null;
                    return new GameClientManifestData(JsonDocument.Parse(jsonstring));
                }
            }
        }
        else
        {
            var broker = OpenFile(filepath);
            using (var reader = broker.OpenRead())
            {
                if (reader.Length == 0) return null;
                using (var sr = new StreamReader(reader, bufferSize: 4096))
                {
                    var jsonstring = sr.ReadToEnd();
                    if (string.IsNullOrEmpty(jsonstring)) return null;
                    return new GameClientManifestData(JsonDocument.Parse(jsonstring));
                }
            }
        }
    }

    /* Old code, which was supposed to work fine, but didn't.
    public static GameClientManifestData CreateFromLocalFile(string filepath)
    {
        using (var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 0, false))
        using (var sr = new StreamReader(fs, bufferSize: 4096))
        {
            return new GameClientManifestData(JsonDocument.Parse(sr.ReadToEnd()));
        }
    }
    */

    private readonly JsonDocument _doc;
    public readonly Uri? AssociatedUrl;

    private GameClientManifestData(JsonDocument doc) : this(doc, null) { }

    public GameClientManifestData(JsonDocument doc, Uri? associatedUrl) 
    {
        this._doc = doc;
        this.AssociatedUrl = associatedUrl;
    }

    public readonly string? projectVersion => this.GetString();
    public readonly string? version => this.GetString();

    public readonly string? pathOffset => this.GetString();

    public readonly FrozenDictionary<string, PakEntry> GetPakDictionary()
    {
        if (this._doc.RootElement.TryGetProperty("paks", out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
            var dictionary = new Dictionary<string, PakEntry>(this.PakCount, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
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
            return FrozenDictionary.ToFrozenDictionary(dictionary, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        }
        return FrozenDictionary<string, PakEntry>.Empty;
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
