using Leayal.Shared.Windows;
using Leayal.SnowBreakLauncher.Classes;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using SharpHDiffPatch.Core.Event;

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
                // As we deduplicated pak entries when writing manifest.json file after updating successfully.
                // The PakCount will likely always be different from the remote manifest one.
                // ONLY WHEN THE PUBLISHER FIXED THEIR MISTAKE TO NOT DUMPING DUPLICATE ENTRIES that this count comparing can be accurate.
                // if (localManifest.PakCount != manifestData.PakCount) return true;

                var localPaks = localManifest.GetPakDictionary();
                var mgrFile = mgr.Files;
                foreach (var pakInfo in manifestData.GetPaks())
                {
                    if (localPaks.TryGetValue(pakInfo.name, out var localPakInfo) && IsFastVerifyMatched(mgrFile.GetFullPath(pakInfo.name), localPakInfo.fastVerify))
                    {
                        if (!string.Equals(localPakInfo.hash, pakInfo.hash, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    else return true; // Immediately return
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
        if (!File.Exists(file)) return false;
        return IsFastVerifyMatchedUnsafe(file, fastVerifyValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool IsFastVerifyMatchedUnsafe(string file, long? fastVerifyValue)
    {
        if (!fastVerifyValue.HasValue) return false;
        return ((new DateTimeOffset(File.GetLastWriteTimeUtc(file))).ToUnixTimeSeconds() == fastVerifyValue.Value);
    }

    private readonly record struct DownloadResult(bool success, long fileLastWriteTimeInUnixSeconds);

    /* Sample
    {
      "name": "Game/Content/Paks/PAK_Game_Wwise_3-WindowsNoEditor.pak",
      "hash": "7fcce4b0f9859e527e4ca1b3f529b467",
      "sizeInBytes": 106001371,
      "bPrimary": true,
      "base": "bc14c6189f79cfcaabceccbb72fda8c1",
      "diff": "a77e423db993eea365e249ab557d490c",
      "diffSizeBytes": 19006788
    },
    
    For non-primary

    {
      "name": "Game/Content/Paks/PAK_Game_Wwise_3-WindowsNoEditor_0_P.pak",
      "hash": "8590e0e0c986fe6c53cc96991b4441c2",
      "sizeInBytes": 7165947,
      "bPrimary": true,
      "base": "",
      "diff": "",
      "diffSizeBytes": 0
    },
    */

    // maybe we don't need the .exe file at all.
    // in-process library rule!!

    public async Task UpdateGameClientAsync(GameClientManifestData? remote_manifest = null, bool fixMode = false, GameUpdaterProgressCallback? progressCallback = null, CancellationToken cancellationToken = default)
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

        FrozenDictionary<string, PakEntry> bufferedLocalFileTable = localManifest.HasValue ? localManifest.Value.GetPakDictionary() : FrozenDictionary<string, PakEntry>.Empty;

        var remoteManifest = await task_getRemoteManifest;
        var totalPak = remoteManifest.PakCount;

        // Determine which file needs to be "updated" (it's actually a redownload anyway)
        var needUpdatedOnes = new BlockingCollection<Tuple<PakEntry, bool>>(totalPak);
        var finishedOnes = new ConcurrentDictionary<PakEntry, DownloadResult>(3, totalPak);

        progressCallback?.OnDisplayableProgressBar?.Invoke();

        var remoteFilelist = remoteManifest.GetPakDictionary();

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

            using (var incrementalMD5 = IncrementalHash.CreateHash(HashAlgorithmName.MD5))
            {
                var borrowed = ArrayPool<byte>.Shared.Rent(1024 * 32);
                try
                {
                    foreach (var pak in remoteFilelist.Values)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        var path_localPak = mgr.Files.GetFullPath(pak.name);

                        // Would be nice if I do a hash table so that I can check if a file just need a rename instead of deleting and redownload the same file under a new name.
                        // But too lazy for that, will do something about it later.

                        var debugging_name = pak.name;

                        if (File.Exists(path_localPak))
                        {
                            string baseHash = pak.@base, diffHash = pak.diff;
                            bool supportBinaryPatching = (!string.IsNullOrEmpty(baseHash) && !string.IsNullOrEmpty(diffHash));

                            if (!fixMode && bufferedLocalFileTable.TryGetValue(pak.name, out var localPakInfo) && IsFastVerifyMatchedUnsafe(path_localPak, localPakInfo.fastVerify) && !string.IsNullOrEmpty(localPakInfo.hash))
                            {
                                if (supportBinaryPatching)
                                {
                                    if (string.Equals(localPakInfo.hash, baseHash, StringComparison.OrdinalIgnoreCase))
                                    {
                                        callback_downloadCount?.IncreaseTotalProgress();
                                        needUpdatedOnes.Add(new Tuple<PakEntry, bool>(pak, true));
                                        continue;
                                    }
                                }
                                if (!string.Equals(localPakInfo.hash, pak.hash, StringComparison.OrdinalIgnoreCase))
                                {
                                    callback_downloadCount?.IncreaseTotalProgress();
                                    needUpdatedOnes.Add(new Tuple<PakEntry, bool>(pak, false));
                                    continue;
                                }
                            }
                            else
                            {
                                using (var fs = new FileStream(path_localPak, FileMode.Open, FileAccess.Read, FileShare.Read, 0))
                                {
                                    incrementalMD5.FillDataFromStream(fs, borrowed);
                                    var hashLenInBytes = incrementalMD5.GetHashAndReset(borrowed);
                                    var hash = Convert.ToHexString(new ReadOnlySpan<byte>(borrowed, 0, hashLenInBytes));

                                    if (supportBinaryPatching)
                                    {
                                        if (string.Equals(hash, baseHash, StringComparison.OrdinalIgnoreCase))
                                        {
                                            callback_downloadCount?.IncreaseTotalProgress();
                                            needUpdatedOnes.Add(new Tuple<PakEntry, bool>(pak, true));
                                            continue;
                                        }
                                    }

                                    if (!string.Equals(hash, pak.hash, StringComparison.OrdinalIgnoreCase))
                                    {
                                        callback_downloadCount?.IncreaseTotalProgress();
                                        needUpdatedOnes.Add(new Tuple<PakEntry, bool>(pak, false));
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
                            needUpdatedOnes.Add(new Tuple<PakEntry, bool>(pak, false));
                        }

                        callback?.IncreaseCurrentProgress();
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(borrowed);
                }
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
            var borrowedBuffer = ArrayPool<byte>.Shared.Rent(1024 * 64); // Borrow a buffer with the size at least to be 64KB, it may returns a bigger buffer
            int maxBufferSize = Math.Min(borrowedBuffer.Length, 1024 * 64); // Should only use up to 64KB
            using (var md5Hasher = IncrementalHash.CreateHash(HashAlgorithmName.MD5))
            {
                try
                {
                    var pathToPreReleaseDirectory = mgr.Files.PathToPreReleaseDirectory;
                    while (!needUpdatedOnes.IsCompleted && !cancellationToken.IsCancellationRequested)
                    {
                        if (needUpdatedOnes.TryTake(out var tuple_downloadData))
                        {
                            var pak = tuple_downloadData.Item1;
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
                                string pathTo_LocalFile = mgr.Files.GetFullPath(pak.name), pathTo_LocalFileTmp;
                                bool isOkay = false;
                                long lastWriteTimeUnixSeconds = -1;

                                var pathTo_PreDownloadFile = Path.Join(pathToPreReleaseDirectory, pak.hash);
                                // Check if predownload file is complete and valid.
                                var isFilePreDownloaded = File.Exists(pathTo_PreDownloadFile);
                                if (isFilePreDownloaded)
                                {
                                    using (var preDownloadFile_Handle = File.OpenHandle(pathTo_PreDownloadFile, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan, 0))
                                    using (var preDownloadFile_fs = new FileStream(preDownloadFile_Handle, FileAccess.Read, 0))
                                    {
                                        var predownloadedLen = preDownloadFile_fs.Length;
                                        if (predownloadedLen == pak.sizeInBytes)
                                        {
                                            if (progressCallback != null) progressCallback.TotalProgress = predownloadedLen;
                                            int read = preDownloadFile_fs.Read(borrowedBuffer, 0, maxBufferSize);
                                            while (read > 0 && !cancellationToken.IsCancellationRequested)
                                            {
                                                md5Hasher.AppendData(borrowedBuffer.AsSpan(0, read));
                                                progressCallback?.IncreaseCurrentProgress(in read);
                                                read = preDownloadFile_fs.Read(borrowedBuffer, 0, maxBufferSize);
                                            }
                                        }
                                        else
                                        {
                                            isFilePreDownloaded = false;
                                        }
                                    }

                                    if (isFilePreDownloaded)
                                    {
                                        var hashLenInBytes = md5Hasher.GetHashAndReset(borrowedBuffer);
                                        var hashOfDownloaded = Convert.ToHexString(borrowedBuffer, 0, hashLenInBytes);
                                        if (string.Equals(hashOfDownloaded, pak.hash, StringComparison.OrdinalIgnoreCase))
                                        {
                                            lastWriteTimeUnixSeconds = (new DateTimeOffset(File.GetLastWriteTimeUtc(pathTo_PreDownloadFile))).ToUnixTimeSeconds();
                                            isOkay = true;
                                        }
                                        else
                                        {
                                            isFilePreDownloaded = false;
                                            try
                                            {
                                                File.Delete(pathTo_PreDownloadFile);
                                            }
                                            catch { }
                                        }
                                    }
                                    else
                                    {
                                        try
                                        {
                                            File.Delete(pathTo_PreDownloadFile);
                                        }
                                        catch { }
                                    }
                                }

                                if (!isFilePreDownloaded)
                                {
                                    pathTo_LocalFileTmp = pathTo_LocalFile + ".dl_ing";
                                    var httpClient = SnowBreakHttpClient.Instance;

                                    var isPatchingViaDiff = tuple_downloadData.Item2;
                                    string hashOfDownloaded;

                                    using (var response = await httpClient.GetFileDownloadResponseAsync(in remoteManifest, in pak, isPatchingViaDiff, cancellationToken))
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
                                            using (var fHandle = File.OpenHandle(isPatchingViaDiff ? (pathTo_LocalFileTmp + ".bin") : pathTo_LocalFileTmp, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, FileOptions.Asynchronous, contentLength))
                                            {
                                                if (isPatchingViaDiff)
                                                {
                                                    addRefSuccess = false;
                                                }
                                                else
                                                {
                                                    fHandle.DangerousAddRef(ref addRefSuccess);
                                                }
                                                using (var fs = new FileStream(fHandle, FileAccess.ReadWrite, 0 /* We use our big fat 64KB+ buffer above */))
                                                {
                                                    fs.Position = 0;
                                                    if (progressCallback != null) progressCallback.TotalProgress = contentLength;

                                                    int read = await responseStream.ReadAsync(borrowedBuffer, 0, maxBufferSize, cancellationToken);
                                                    while (read > 0 && !cancellationToken.IsCancellationRequested)
                                                    {
                                                        var task_write = fs.WriteAsync(borrowedBuffer, 0, read, cancellationToken);
                                                        md5Hasher.AppendData(borrowedBuffer.AsSpan(0, read));
                                                        await task_write;
                                                        progressCallback?.IncreaseCurrentProgress(in read);
                                                        read = await responseStream.ReadAsync(borrowedBuffer, 0, maxBufferSize, cancellationToken);
                                                    }

                                                    await fs.FlushAsync(cancellationToken);

                                                    var hashLenInBytes = md5Hasher.GetHashAndReset(borrowedBuffer);
                                                    hashOfDownloaded = Convert.ToHexString(borrowedBuffer, 0, hashLenInBytes);
                                                    if (!cancellationToken.IsCancellationRequested)
                                                    {
                                                        if (!isPatchingViaDiff)
                                                        {
                                                            if (string.Equals(hashOfDownloaded, pak.hash, StringComparison.OrdinalIgnoreCase))
                                                            {
                                                                isOkay = true;
                                                                // Fix the length if necessary
                                                                var currentLen = progressCallback != null ? progressCallback.GetCurrentProgress() : fs.Position;
                                                                if (currentLen != fs.Length)
                                                                {
                                                                    fs.SetLength(currentLen);
                                                                }
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
                                                if (!cancellationToken.IsCancellationRequested && isPatchingViaDiff)
                                                {
                                                    if (string.Equals(hashOfDownloaded, pak.diff, StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        var hPatchFile = new Hpatchz(pathTo_LocalFileTmp + ".bin");
                                                        EventHandler<PatchEvent>? hPatchprogressCallback = null;
                                                        if (progressCallback != null)
                                                        {
                                                            progressCallback.OnStartHPatchZ(hPatchFile.DiffSize);
                                                            hPatchprogressCallback = (eventDispatcher, ev) =>
                                                            {
                                                                progressCallback.SetHPatchZCurrentProgress(ev.CurrentSizePatched);
                                                            };
                                                        }
                                                        try
                                                        {
                                                            if (progressCallback != null) hPatchFile.PatchEvent += hPatchprogressCallback;
                                                            await hPatchFile.Patch(pathTo_LocalFile, pathTo_LocalFileTmp, cancellationToken);
                                                            isOkay = true;
                                                        }
                                                        catch
                                                        {
                                                            isOkay = false;
                                                        }
                                                        finally
                                                        {
                                                            if (progressCallback != null) hPatchFile.PatchEvent -= hPatchprogressCallback;
                                                            File.Delete(hPatchFile.HDiffPath);
                                                        }
                                                    }
                                                }

                                                lastWriteTimeUnixSeconds = (new DateTimeOffset(File.GetLastWriteTimeUtc(pathTo_LocalFileTmp))).ToUnixTimeSeconds();
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    pathTo_LocalFileTmp = pathTo_PreDownloadFile;
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
        }

        bool isEverythingDoneNicely = false;
        try
        {
            await Task.WhenAll(task_FileCheck,
                // 2 concurrent download streams
                StartLongRunningTask(DownloadFile, progressCallback?.Download1Progress, cancellationToken),
                StartLongRunningTask(DownloadFile, progressCallback?.Download2Progress, cancellationToken));

            isEverythingDoneNicely = !cancellationToken.CanBeCanceled || !cancellationToken.IsCancellationRequested;

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

            // Double ensure that we don't get any Pak archives left over from old major releases to cause data collisions.
            if (fixMode)
            {
                static void EncapsuleToEscapeAsyncRule_DeleteUnknownFiles(GameManager mgr, FrozenDictionary<string, PakEntry> remoteFilelist)
                {
                    var gameDirLength = mgr.FullPathOfGameDirectory.Length;
                    var dataDirectory = mgr.Files.GetFullPath(Path.Join("Game", "Content"));
                    ReadOnlySpan<char> directorySeparatorChars = stackalloc char[]
                    {
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar,
                        ' ',
                        char.MinValue
                    };
                    if (Directory.Exists(dataDirectory))
                    {
                        foreach (var filename in Directory.EnumerateFiles(dataDirectory, "*", new EnumerationOptions() { RecurseSubdirectories = true, ReturnSpecialDirectories = false, AttributesToSkip = FileAttributes.None, MaxRecursionDepth = 30 }))
                        {
                            var mem_relativePath = filename.AsMemory(gameDirLength).TrimStart(directorySeparatorChars); // Can optimize it by using "filename.AsMemory(gameDirLength + 1);" but we will lost sanitize
                            var relativePath = string.Create(mem_relativePath.Length, mem_relativePath, (c, obj) =>
                            {
                                obj.Span.CopyTo(c);
                                c.Replace('\\', '/');
                            });
                            if (!remoteFilelist.ContainsKey(relativePath))
                            {
                                FileSystem.ForceDelete(filename);
                            }
                        }
                    }
                }
                EncapsuleToEscapeAsyncRule_DeleteUnknownFiles(this.manager, remoteFilelist);
            }
            else if (bufferedLocalFileTable.Count != 0)
            {
                foreach (var pakName in bufferedLocalFileTable.Keys)
                {
                    if (!remoteFilelist.ContainsKey(pakName))
                    {
                        var path_localPak = mgr.Files.GetFullPath(pakName);
                        if (File.Exists(path_localPak))
                        {
                            FileSystem.ForceDelete(path_localPak);
                        }
                    }
                }
            }

            // Output new manifest.json in the local
            var localPath_Manifest = mgr.Files.PathToManifestJson;
            if (OperatingSystem.IsWindows())
            {
                var localPath_ManifestPopulating = localPath_Manifest + ".updating";
                using (var fs_manifest = File.Create(localPath_ManifestPopulating))
                using (var jsonwriter = new Utf8JsonWriter(fs_manifest, new JsonWriterOptions() { Indented = false }))
                {
                    WriteManifestDataTo(jsonwriter, in localManifest, needUpdatedOnes, in isEverythingDoneNicely, in remoteManifest, finishedOnes, bufferedLocalFileTable);
                }
                FileSystem.MoveOverwrite_AwareReadOnly(localPath_ManifestPopulating, localPath_Manifest);
            }
            else
            {
                var newJsonDataBuffering = new ArrayBufferWriter<byte>(1024 * 1024);
                using (var jsonwriter = new Utf8JsonWriter(newJsonDataBuffering, new JsonWriterOptions() { Indented = false }))
                {
                    WriteManifestDataTo(jsonwriter, in localManifest, needUpdatedOnes, in isEverythingDoneNicely, in remoteManifest, finishedOnes, bufferedLocalFileTable);
                }
                var broker = GameClientManifestData.OpenFile(localPath_Manifest);
                using (var writeStream = broker.OpenWrite())
                {
                    var size = newJsonDataBuffering.WrittenCount;
                    writeStream.SetLength(size);
                    writeStream.Position = 0;
                    writeStream.Write(newJsonDataBuffering.WrittenSpan);
                }
            }

            if (isEverythingDoneNicely)
                File.WriteAllText(Path.Join(mgr.FullPathOfInstallationDirectory, "version.cfg"), "[game.exe]\r\nUniformGameVersion=" + (remoteManifest.version ?? string.Empty), System.Text.Encoding.ASCII);

            needUpdatedOnes.Dispose();
        }
    }

    static void WriteManifestDataTo(Utf8JsonWriter jsonwriter, in GameClientManifestData? localManifest, BlockingCollection<Tuple<PakEntry, bool>> needUpdatedOnes, in bool isEverythingDoneNicely,
                in GameClientManifestData remoteManifest, ConcurrentDictionary<PakEntry, DownloadResult> finishedOnes, IReadOnlyDictionary<string, PakEntry> bufferedLocalFileTable)
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

        var outputedOnes = new HashSet<string>(remoteManifest.PakCount, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var pakInfo in remoteManifest.GetPaks())
        {
            if (!outputedOnes.Add(pakInfo.name)) continue;

            if (finishedOnes.TryGetValue(pakInfo, out var updateResult))
            {
                if (updateResult.success)
                {
                    jsonwriter.WriteStartObject();
                    jsonwriter.WriteString("hash", pakInfo.hash);
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
        outputedOnes.Clear();
        outputedOnes = null;
    }
}
