using Leayal.Shared.Windows;
using Leayal.SnowBreakLauncher.Classes;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Frozen;
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
        using (var localManifest = mgr.Files.GetLocalManifest())
        using (var manifestData = await httpClient.GetGameClientManifestAsync(cancellationToken))
        {
            return !string.Equals(localManifest.version, manifestData.version, StringComparison.Ordinal);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task StartLongRunningTask(Action action, CancellationToken cancellation) => Task.Factory.StartNew(action, cancellation, TaskCreationOptions.LongRunning, TaskScheduler.Current ?? TaskScheduler.Default);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task StartLongRunningTask(Func<object?, Task> action, GameUpdaterDownloadProgressValue? progressCallback, CancellationToken cancellation) => Task.Factory.StartNew(action, progressCallback, cancellation, TaskCreationOptions.LongRunning, TaskScheduler.Current ?? TaskScheduler.Default).Unwrap();

    public async Task UpdateGameClientAsync(GameClientManifestData? remote_manifest = null, GameUpdaterProgressCallback? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var mgr = this.manager;
        var httpClient = SnowBreakHttpClient.Instance;

        var localManifest = mgr.Files.GetLocalManifest();
        ValueTask<GameClientManifestData> task_getRemoteManifest;
        if (remote_manifest.HasValue)
        {
            task_getRemoteManifest = ValueTask.FromResult(remote_manifest.Value);
        }
        else
        {
            task_getRemoteManifest = new ValueTask<GameClientManifestData>(httpClient.GetGameClientManifestAsync(cancellationToken));
        }
        var bufferedLocalFileTable = FrozenDictionary.ToFrozenDictionary(localManifest.GetPaks(), pak => pak.name, StringComparer.OrdinalIgnoreCase);

        var remoteManifest = await task_getRemoteManifest;
        var totalPak = remoteManifest.PakCount;

        // Determine which file needs to be "updated" (it's actually a redownload anyway)
        var needUpdatedOnes = new BlockingCollection<PakEntry>(totalPak);
        var finishedOnes = new ConcurrentDictionary<PakEntry, bool>(3, totalPak);

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
                    if (bufferedLocalFileTable.TryGetValue(pak.name, out var localPakInfo))
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

                            finishedOnes.AddOrUpdate(pak, true, (p, oldValue) => true);
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
            try
            {
                while (!needUpdatedOnes.IsCompleted)
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

                            using (var response = await httpClient.GetFileDownloadResponseAsync(remoteManifest, pak.name, cancellationToken))
                            {
                                if (!response.IsSuccessStatusCode)
                                {
                                    finishedOnes.AddOrUpdate(pak, false, (p, oldValue) => false);
                                    continue;
                                }

                                using (var responseStream = response.Content.ReadAsStream())
                                {
                                    var header_contentLength = response.Content.Headers.ContentLength;
                                    long contentLength = header_contentLength.HasValue ? header_contentLength.Value : 0L;
                                    if (Path.GetDirectoryName(pathTo_LocalFileTmp) is string directory)
                                    {
                                        Directory.CreateDirectory(directory);
                                    }
                                    using (var fHandle = File.OpenHandle(pathTo_LocalFileTmp, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, FileOptions.None, contentLength))
                                    using (var fs = new FileStream(fHandle, FileAccess.ReadWrite, 0 /* We use our big fat 32KB+ buffer above */))
                                    {
                                        fs.Position = 0;
                                        if (progressCallback != null) progressCallback.TotalProgress = contentLength;

                                        int read = responseStream.Read(borrowedBuffer, 0, maxBufferSize);
                                        while (read > 0)
                                        {
                                            fs.Write(borrowedBuffer, 0, read);
                                            if (progressCallback != null) progressCallback?.IncreaseCurrentProgress(in read);
                                            read = responseStream.Read(borrowedBuffer, 0, maxBufferSize);
                                        }

                                        fs.Flush();
                                        isOkay = (fs.Length == pak.sizeInBytes);
                                    }
                                }
                            }

                            if (isOkay)
                            {
                                try
                                {
                                    if (FileSystem.PathExists(pathTo_LocalFile))
                                    {
                                        var attr = File.GetAttributes(pathTo_LocalFile);
                                        if ((attr & FileAttributes.ReadOnly) != 0)
                                        {
                                            File.SetAttributes(pathTo_LocalFile, attr & ~FileAttributes.ReadOnly);
                                        }
                                        File.Delete(pathTo_LocalFile);
                                    }
                                    File.Move(pathTo_LocalFileTmp, pathTo_LocalFile, true);
                                    finishedOnes.AddOrUpdate(pak, true, (p, oldValue) => true);
                                }
                                catch
                                {
                                    finishedOnes.AddOrUpdate(pak, false, (p, oldValue) => false);
                                }
                            }
                            else
                            {
                                try
                                {
                                    File.Delete(pathTo_LocalFileTmp);
                                }
                                catch { }
                                finishedOnes.AddOrUpdate(pak, false, (p, oldValue) => false);
                            }

                            progressCallback?.OnComplete();
                        }
                        catch
                        {
                            finishedOnes.AddOrUpdate(pak, false, (p, oldValue) => false);
                        }
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
            using (var jsonwriter = new Utf8JsonWriter(fs_manifest, new JsonWriterOptions() { Indented = true }))
            {
                jsonwriter.WriteStartObject();

                bool allFileHasBeenUpdated = true;

                // bufferedLocalFileTable, needUpdatedOnes, finishedOnes
                jsonwriter.WriteStartArray("paks");

                foreach (var pakInfo in remoteManifest.GetPaks())
                {
                    if (finishedOnes.TryGetValue(pakInfo, out var updateResult))
                    {
                        if (updateResult)
                        {
                            if (bufferedLocalFileTable.TryGetValue(pakInfo.name, out var oldEntry) && oldEntry.fastVerify.HasValue)
                            {
                                jsonwriter.WriteStartObject();
                                jsonwriter.WriteNumber("cRC", pakInfo.cRC);
                                jsonwriter.WriteString("name", pakInfo.name);
                                jsonwriter.WriteNumber("fastVerify", oldEntry.fastVerify.Value);
                                jsonwriter.WriteNumber("sizeInBytes", pakInfo.sizeInBytes);
                                jsonwriter.WriteEndObject();
                            }
                            else
                            {
                                // jsonwriter.WriteStartObject();
                                pakInfo.WriteJsonDataTo(jsonwriter);
                                // jsonwriter.WriteEndObject();
                            }
                        }
                        else
                        {
                            allFileHasBeenUpdated = false;
                            if (bufferedLocalFileTable.TryGetValue(pakInfo.name, out var oldEntry))
                            {
                                oldEntry.WriteJsonDataTo(jsonwriter);
                            }
                        }
                    }
                    else if (bufferedLocalFileTable.TryGetValue(pakInfo.name, out var unChangedPak))
                    {
                        unChangedPak.WriteJsonDataTo(jsonwriter);
                    }
                }

                jsonwriter.WriteEndArray();

                if (isEverythingDoneNicely && allFileHasBeenUpdated)
                {
                    jsonwriter.WriteString("version", remoteManifest.version);
                    // jsonwriter.WriteString("projectVersion", remoteManifest.projectVersion);
                    jsonwriter.WriteString("pathOffset", remoteManifest.pathOffset);
                }
                else
                {
                    jsonwriter.WriteString("version", localManifest.version);
                    // jsonwriter.WriteString("projectVersion", remoteManifest.projectVersion);
                    jsonwriter.WriteString("pathOffset", localManifest.pathOffset);
                }

                jsonwriter.WriteEndObject();
                jsonwriter.Flush();
            }

            if (FileSystem.PathExists(localPath_Manifest))
            {
                var attr = File.GetAttributes(localPath_Manifest);
                if ((attr & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(localPath_Manifest, attr & ~FileAttributes.ReadOnly);
                }
                File.Delete(localPath_Manifest);
            }
            File.Move(localPath_ManifestPopulating, localPath_Manifest, true);

            needUpdatedOnes.Dispose();
        }
    }
}
