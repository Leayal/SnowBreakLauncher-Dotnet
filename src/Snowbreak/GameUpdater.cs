using DamienG.Security.Cryptography;
using Leayal.Shared.Windows;
using Leayal.SnowBreakLauncher.Classes;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher.Snowbreak;

sealed class GameUpdater
{
    private readonly GameManager manager;
    
    internal GameUpdater(GameManager manager)
    {
        this.manager = manager;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns><see langword="true"/> if the local client is older and needs to be updated. Otherwise, <see langword="false"/>.</returns>
    public async Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var mgr = this.manager;
        var httpClient = SnowBreakHttpClient.Instance;
        var _localManifest = mgr.Files.TryGetLocalManifest();

        // In case the local manifest.json file doesn't exist.
        // We will deceive that the client need updates regardless.
        // While the actual "update" will full scan/check the directory for existing files and update only the ones that is either missing or oudated.
        // In case there nothing at all, should result in redownload the whole game.
        if (!_localManifest.HasValue) return true;

        using (var localManifest = _localManifest.Value)
        using (var manifestData = await httpClient.GetGameClientManifestAsync(cancellationToken))
        {
            if (MemoryExtensions.Equals(localManifest.version.AsSpan().Trim(), manifestData.version.AsSpan().Trim(), StringComparison.Ordinal))
            {
                if (localManifest.PakCount != manifestData.PakCount) return true;

                var localPaks = localManifest.GetPaks().ToFrozenDictionary(pak => pak.name, StringComparer.OrdinalIgnoreCase);
                var mgrFile = mgr.Files;
                foreach (var pakInfo in manifestData.GetPaks())
                {
                    if (localPaks.TryGetValue(pakInfo.name, out var localPakInfo) && IsFastVerifyMatched(mgrFile.GetFullPath(pakInfo.name), localPakInfo.fastVerify))
                    {
                        if (localPakInfo.cRC != pakInfo.cRC) return true;
                    }
                    else return true; // Immediately return, not breaking out of loop.
                }
                return false;
            }
            else return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task StartLongRunningTask(Action action, CancellationToken cancellation) => Task.Factory.StartNew(action, cancellation, TaskCreationOptions.LongRunning, TaskScheduler.Current ?? TaskScheduler.Default);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task StartLongRunningTask(Func<object?, Task> action, GameUpdaterDownloadProgressValue? progressCallback, CancellationToken cancellation) => Task.Factory.StartNew(action, progressCallback, cancellation, TaskCreationOptions.LongRunning, TaskScheduler.Current ?? TaskScheduler.Default).Unwrap();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool IsFastVerifyMatched(string file, long? fastVerifyValue)
    {
        if (!FileSystem.PathExists(file)) return false;
        return IsFastVerifyMatchedUnsafe(file, fastVerifyValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool IsFastVerifyMatchedUnsafe(string file, long? fastVerifyValue)
    {
        if (!fastVerifyValue.HasValue) return false;
        return ((new DateTimeOffset(File.GetLastWriteTimeUtc(file))).ToUnixTimeSeconds() == fastVerifyValue.Value);
    }

    private record struct DownloadResult(bool success, long fileLastWriteTimeInUnixSeconds);

    public async Task UpdateGameClientAsync(GameClientManifestData? remote_manifest = null, bool skipCrcTableCache = false, GameUpdaterProgressCallback? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var mgr = this.manager;
        var httpClient = SnowBreakHttpClient.Instance;

        var localManifest = mgr.Files.TryGetLocalManifest();
        ValueTask<GameClientManifestData> task_getRemoteManifest;
        if (remote_manifest.HasValue)
        {
            task_getRemoteManifest = ValueTask.FromResult(remote_manifest.Value);
        }
        else
        {
            task_getRemoteManifest = new ValueTask<GameClientManifestData>(httpClient.GetGameClientManifestAsync(cancellationToken));
        }
        IReadOnlyDictionary<string, PakEntry> bufferedLocalFileTable = (localManifest.HasValue ?
            FrozenDictionary.ToFrozenDictionary(localManifest.Value.GetPaks(), pak => pak.name, StringComparer.OrdinalIgnoreCase)
            : System.Collections.Immutable.ImmutableDictionary<string, PakEntry>.Empty);

        var remoteManifest = await task_getRemoteManifest;
        var totalPak = remoteManifest.PakCount;

        // Determine which file needs to be "updated" (it's actually a redownload anyway)
        var needUpdatedOnes = new BlockingCollection<PakEntry>(totalPak);
        var finishedOnes = new ConcurrentDictionary<PakEntry, DownloadResult>(3, totalPak);

        progressCallback?.OnDisplayableProgressBar?.Invoke();

        var task_FileCheck = StartLongRunningTask(() =>
        {
            var callback = progressCallback?.FileCheckProgress;
            if (callback != null)
            {
                callback.IsDone = false;
                callback.CurrentProgress = 0;
                callback.TotalProgress = totalPak;
            }
            var callback_downloadCount = progressCallback?.TotalDownloadProgress;
            foreach (var pak in remoteManifest.GetPaks())
            {
                if (cancellationToken.IsCancellationRequested) break;
                var path_localPak = mgr.Files.GetFullPath(pak.name);
                if (FileSystem.PathExists(path_localPak))
                {
                    if (!skipCrcTableCache && bufferedLocalFileTable.TryGetValue(pak.name, out var localPakInfo) && IsFastVerifyMatchedUnsafe(path_localPak, localPakInfo.fastVerify))
                    {
                        if (localPakInfo.cRC != pak.cRC || localPakInfo.sizeInBytes != pak.sizeInBytes)
                        {
                            callback_downloadCount?.IncreaseTotalProgress();
                            needUpdatedOnes.Add(pak);
                            continue;
                        }
                    }
                    else
                    {
                        using (var fs = new FileStream(path_localPak, FileMode.Open, FileAccess.Read, FileShare.Read, 0))
                        {
                            if (fs.Length != pak.sizeInBytes)
                            {
                                callback_downloadCount?.IncreaseTotalProgress();
                                needUpdatedOnes.Add(pak);
                                continue;
                            }

                            var crc = Crc32HashHelper.ComputeFromStream(fs);
                            if (crc != pak.cRC)
                            {
                                callback_downloadCount?.IncreaseTotalProgress();
                                needUpdatedOnes.Add(pak);
                                continue;
                            }

                            var fileCreationTimeUnixSeconds = (new DateTimeOffset(File.GetLastWriteTimeUtc(fs.SafeFileHandle))).ToUnixTimeSeconds();

                            finishedOnes.AddOrUpdate(pak, new DownloadResult(true, fileCreationTimeUnixSeconds), (p, oldValue) => new DownloadResult(true, fileCreationTimeUnixSeconds));
                        }
                    }
                }
                else
                {
                    callback_downloadCount?.IncreaseTotalProgress();
                    needUpdatedOnes.Add(pak);
                }

                callback?.IncreaseCurrentProgress();
            }

            needUpdatedOnes.CompleteAdding();


            if (callback != null)
            {
                callback.IsDone = true;
            }
        }, cancellationToken);


        async Task DownloadFile(object? obj)
        {
            var progressCallback = obj as GameUpdaterDownloadProgressValue;
            var borrowedBuffer = ArrayPool<byte>.Shared.Rent(1024 * 32); // Borrow a buffer with the size at least to be 32KB, it may returns a bigger buffer
            int maxBufferSize = Math.Min(borrowedBuffer.Length, 1024 * 64); // Should only use up to 64KB
            var crcHasher = new IncrementalCrc32Hash();
            try
            {
                while (!needUpdatedOnes.IsCompleted && !cancellationToken.IsCancellationRequested)
                {
                    if (needUpdatedOnes.TryTake(out var pak))
                    {
                        try
                        {
                            var relativeFile = pak.name;
                            if (progressCallback != null)
                            {
                                progressCallback.Filename = relativeFile;
                                progressCallback.CurrentProgress = 0;
                                progressCallback.TotalProgress = 1;
                                progressCallback.IsDone = false;
                            }
                            var pathTo_LocalFile = mgr.Files.GetFullPath(pak.name);
                            var pathTo_LocalFileTmp = pathTo_LocalFile + ".dl_ing";
                            var httpClient = SnowBreakHttpClient.Instance;

                            bool isOkay = false;
                            long lastWriteTimeUnixSeconds = -1;

                            using (var response = await httpClient.GetFileDownloadResponseAsync(remoteManifest, pak.name, cancellationToken))
                            {
                                if (!response.IsSuccessStatusCode)
                                {
                                    finishedOnes.AddOrUpdate(pak, new DownloadResult(false, -1), (p, oldValue) => new DownloadResult(false, -1));
                                    continue;
                                }

                                using (var responseStream = response.Content.ReadAsStream())
                                {
                                    var header_contentLength = response.Content.Headers.ContentLength;
                                    long contentLength = header_contentLength.HasValue ? header_contentLength.Value : 0L;
                                    if (Path.GetDirectoryName(pathTo_LocalFileTmp) is string directoryPath)
                                    {
                                        Directory.CreateDirectory(directoryPath);
                                    }
                                    bool addRefSuccess = false;
                                    using (var fHandle = File.OpenHandle(pathTo_LocalFileTmp, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, FileOptions.None, contentLength))
                                    {  
                                        fHandle.DangerousAddRef(ref addRefSuccess);
                                        using (var fs = new FileStream(fHandle, FileAccess.ReadWrite, 0 /* We use our big fat 32KB+ buffer above */))
                                        {
                                            fs.Position = 0;
                                            if (progressCallback != null) progressCallback.TotalProgress = contentLength;

                                            crcHasher.Reset();
                                            int read = responseStream.Read(borrowedBuffer, 0, maxBufferSize);
                                            while (read > 0 && !cancellationToken.IsCancellationRequested)
                                            {
                                                fs.Write(borrowedBuffer, 0, read);
                                                crcHasher.Append(borrowedBuffer.AsSpan(0, read));
                                                if (progressCallback != null) progressCallback?.IncreaseCurrentProgress(in read);
                                                read = responseStream.Read(borrowedBuffer, 0, maxBufferSize);
                                            }

                                            fs.Flush();
                                            if (!cancellationToken.IsCancellationRequested)
                                            {
                                                if (crcHasher.HashRaw == pak.cRC)
                                                {
                                                    isOkay = true;
                                                    // Fix the length if necessary
                                                    var currentLen = fs.Position;
                                                    if (currentLen != fs.Length)
                                                    {
                                                        fs.SetLength(currentLen);
                                                    }
                                                }
                                            }
                                        }
                                        if (addRefSuccess)
                                        {
                                            try
                                            {
                                                lastWriteTimeUnixSeconds = (new DateTimeOffset(File.GetLastWriteTimeUtc(fHandle))).ToUnixTimeSeconds();
                                            }
                                            finally
                                            {
                                                fHandle.DangerousRelease();
                                            }
                                        }
                                    }
                                    if (!addRefSuccess)
                                    {
                                        lastWriteTimeUnixSeconds = (new DateTimeOffset(File.GetLastWriteTimeUtc(pathTo_LocalFileTmp))).ToUnixTimeSeconds();
                                    }
                                }
                            }

                            if (isOkay)
                            {
                                try
                                {
                                    FileSystem.MoveOverwrite_AwareReadOnly(pathTo_LocalFileTmp, pathTo_LocalFile);
                                    finishedOnes.AddOrUpdate(pak, new DownloadResult(true, lastWriteTimeUnixSeconds), (p, oldValue) => new DownloadResult(true, lastWriteTimeUnixSeconds));
                                }
                                catch
                                {
                                    finishedOnes.AddOrUpdate(pak, new DownloadResult(false, -1), (p, oldValue) => new DownloadResult(false, -1));
                                }
                            }
                            else
                            {
                                try
                                {
                                    File.Delete(pathTo_LocalFileTmp);
                                }
                                catch { }
                                finishedOnes.AddOrUpdate(pak, new DownloadResult(false, -1), (p, oldValue) => new DownloadResult(false, -1));
                            }

                            progressCallback?.OnComplete();
                        }
                        catch
                        {
                            finishedOnes.AddOrUpdate(pak, new DownloadResult(false, -1), (p, oldValue) => new DownloadResult(false, -1));
                        }
                    }
                    else
                    {
                        await Task.Delay(10);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(borrowedBuffer);
            }
        }

        bool isEverythingDoneNicely = false;
        try
        {
            await Task.WhenAll(task_FileCheck,
                // 2 concurrent download streams
                StartLongRunningTask(DownloadFile, progressCallback?.Download1Progress, cancellationToken),
                StartLongRunningTask(DownloadFile, progressCallback?.Download2Progress, cancellationToken));
            isEverythingDoneNicely = true;

            if (progressCallback != null)
            {
                progressCallback.FileCheckProgress.IsDone = true;
                progressCallback.TotalDownloadProgress.IsDone = true;
                progressCallback.Download1Progress.IsDone = true;
                progressCallback.Download2Progress.IsDone = true;
            }
        }
        catch
        {
            isEverythingDoneNicely = false;
            throw;
        }
        finally
        {
            // Just want to ensure we finalize things

            // Output new manifest.json in the local
            var localPath_Manifest = mgr.Files.PathToManifestJson;
            var localPath_ManifestPopulating = localPath_Manifest + ".updating";
            using (var fs_manifest = File.Create(localPath_ManifestPopulating))
            using (var jsonwriter = new Utf8JsonWriter(fs_manifest, new JsonWriterOptions() { Indented = false }))
            {
                jsonwriter.WriteStartObject();

                // We're gonna keep all old data/values of the existing manifest.json by re-output it to the new JSON
                // Except these property with case-sensitive names: paks, version, pathOffset
                string exclude_paks = "paks", exclude_version = "version", exclude_pathOffset = "pathOffset";

                // Loop through the existing prop if the file exists
                if (localManifest.HasValue)
                {
                    foreach (var oldProp in localManifest.Value.GetRawProperies())
                    {
                        if (string.Equals(oldProp.Name, exclude_paks, StringComparison.Ordinal)
                            || string.Equals(oldProp.Name, exclude_version, StringComparison.Ordinal)
                            || string.Equals(oldProp.Name, exclude_pathOffset, StringComparison.Ordinal))
                            continue;
                        oldProp.WriteTo(jsonwriter);
                    }
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static bool HasNoFailures(ICollection<DownloadResult> collection)
                {
                    foreach (var result in collection)
                    {
                        if (!result.success) return false;
                    }
                    return true;
                }

                bool allFileHasBeenUpdated = needUpdatedOnes.IsCompleted && HasNoFailures(finishedOnes.Values);
                if (isEverythingDoneNicely && allFileHasBeenUpdated)
                {
                    jsonwriter.WriteString("version", remoteManifest.version);
                    // jsonwriter.WriteString("projectVersion", remoteManifest.projectVersion);
                    jsonwriter.WriteString("pathOffset", remoteManifest.pathOffset);
                }
                else
                {
                    if (localManifest.HasValue)
                    {
                        var _localManifest = localManifest.Value;
                        jsonwriter.WriteString("version", _localManifest.version);
                        // jsonwriter.WriteString("projectVersion", remoteManifest.projectVersion);
                        jsonwriter.WriteString("pathOffset", _localManifest.pathOffset);
                    }
                    else
                    {
                        jsonwriter.WriteString("version", "--"); // Yup, official launcher uses this value to indicate the client's version is unknown
                        jsonwriter.WriteString("pathOffset", remoteManifest.pathOffset);
                    }
                }

                jsonwriter.WriteStartArray("paks");

                foreach (var pakInfo in remoteManifest.GetPaks())
                {
                    if (finishedOnes.TryGetValue(pakInfo, out var updateResult))
                    {
                        if (updateResult.success)
                        {
                            // I still can't figure what the "fastVerify" field implies or meaning.
                            jsonwriter.WriteStartObject();
                            jsonwriter.WriteNumber("cRC", pakInfo.cRC);
                            jsonwriter.WriteString("name", pakInfo.name);
                            jsonwriter.WriteNumber("fastVerify", updateResult.fileLastWriteTimeInUnixSeconds);
                            jsonwriter.WriteNumber("sizeInBytes", pakInfo.sizeInBytes);
                            jsonwriter.WriteEndObject();
                        }
                        else if (bufferedLocalFileTable.TryGetValue(pakInfo.name, out var oldEntry))
                        {
                            oldEntry.WriteJsonDataTo(jsonwriter);
                        }
                    }
                    else if (bufferedLocalFileTable.TryGetValue(pakInfo.name, out var unChangedPak))
                    {
                        unChangedPak.WriteJsonDataTo(jsonwriter);
                    }
                }

                jsonwriter.WriteEndArray();

                jsonwriter.WriteEndObject();
                jsonwriter.Flush();
            }

            FileSystem.MoveOverwrite_AwareReadOnly(localPath_ManifestPopulating, localPath_Manifest);

            needUpdatedOnes.Dispose();
        }
    }
}
