using System;
using System.Collections.Concurrent;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
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
    // There is, however, some drawbacks from this: There can only one single write operation at a time (which is no problem as a file shouldn't be overlapped writing anyway)
    // and the File handle will always be opened with Read&Write access and OpenOrCreate mode, insignificant drawback is the extra cost of all the checks.

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
        private object locker;
        private bool _created;
        private MemoryMappedFile? memMappedFile;
        private readonly string filepath;
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

        private FileStream Factory() => new FileStream(this.filepath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 4096, true);

        private MemoryMappedFile OpenOrCreate(out FileStream writeStream)
        {
            writeStream = LazyInitializer.EnsureInitialized<FileStream>(ref this.fs, ref _created, ref this.locker, this.Factory);
            var mappedFile = MemoryMappedFile.CreateFromFile(writeStream, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);
            var exchangedOne = Interlocked.CompareExchange(ref this.memMappedFile, mappedFile, null);
            if (exchangedOne != null)
            {
                if (exchangedOne != mappedFile)
                {
                    // Can't be here anyway
                    mappedFile.Dispose();
                }
                return exchangedOne;
            }
            else
            {
                return mappedFile;
            }
        }

        public void Dispose() => this.memMappedFile?.Dispose();

        public Stream OpenRead()
        {
            var memMapped = this.OpenOrCreate(out _);
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

        public Stream OpenWrite()
        {
            var memMapped = this.OpenOrCreate(out var writeStream);
            this.ioLocker.EnterWriteLock();
            var syncContext = SynchronizationContext.Current;
            if (syncContext == null)
            {
                syncContext = new SynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(syncContext);
            }
            return new WrapperReadStream(writeStream, this.ioLocker, syncContext);
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
                if (obj is ReaderWriterLockSlim ioLocker)
                    ioLocker.ExitWriteLock();
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
                if (obj is ReaderWriterLockSlim ioLocker)
                    ioLocker.ExitReadLock();
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

    public static GameClientManifestData CreateFromLocalFile(string filepath)
    {
        if (OperatingSystem.IsWindows())
        {
            using (var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 0, false))
            using (var sr = new StreamReader(fs, bufferSize: 4096))
            {
                return new GameClientManifestData(JsonDocument.Parse(sr.ReadToEnd()));
            }
        }
        else
        {
            var broker = OpenFile(filepath);
            using (var reader = broker.OpenRead())
            using (var sr = new StreamReader(reader, bufferSize: 4096))
            {
                return new GameClientManifestData(JsonDocument.Parse(sr.ReadToEnd()));
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

    public GameClientManifestData(JsonDocument doc) 
    {
        this._doc = doc;
    }

    public readonly string? projectVersion => this.GetString();
    public readonly string? version => this.GetString();

    public readonly string? pathOffset => this.GetString();

    public readonly IReadOnlyDictionary<string, PakEntry> GetPakDictionary()
    {
        var dictionary = new Dictionary<string, PakEntry>(this.PakCount, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        if (this._doc.RootElement.TryGetProperty("paks", out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
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
        }
#if NET8_0_OR_GREATER
        return FrozenDictionary.ToFrozenDictionary(dictionary, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
#else
        return dictionary.AsReadOnly();
#endif
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
