using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher.Classes
{
    class RemoteResourcePersistentCache : IDisposable
    {
        public readonly static RemoteResourcePersistentCache Instance;

        static RemoteResourcePersistentCache()
        {
            Instance = new RemoteResourcePersistentCache(Path.GetFullPath("cacheData", AppContext.BaseDirectory));
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        private static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
            Instance.Dispose();
        }

        private readonly string _directoryPath;
        private readonly ConcurrentDictionary<string, Lazy<ResourceTask>> lockers;

        private RemoteResourcePersistentCache(string dataDirectoryPath)
        {
            this._directoryPath = dataDirectoryPath;
            Directory.CreateDirectory(dataDirectoryPath);
            this.lockers = new ConcurrentDictionary<string, Lazy<ResourceTask>>();
        }

        public Task<string> GetResource(Uri remoteURL)
        {
            var theOnlyOne = this.lockers.GetOrAdd(remoteURL.AbsoluteUri, new Lazy<ResourceTask>(() => new ResourceTask(this._directoryPath, remoteURL)));
            return theOnlyOne.Value.task;
        }

        class ResourceTask
        {
            public readonly object lockObj;
            public readonly Uri url;
            public readonly Task<string> task;
            public readonly string dataDir;

            public ResourceTask(string dataDir, Uri url)
            {
                this.dataDir = dataDir;
                this.lockObj = new object();
                this.url = url;
                this.task = Task.Factory.StartNew(this.DoWork, TaskCreationOptions.LongRunning).Unwrap();
            }

            private async Task<string> DoWork()
            {
                var localFilename = Path.GetFullPath(GetSha1OfFilename(this.url.AbsoluteUri), this.dataDir);
                if (File.Exists(localFilename))
                {
                    return localFilename;
                }
                else
                {
                    var tempFile = localFilename + ".dl_ing";
                    try
                    {
                        var httpClient = Snowbreak.SnowBreakHttpClient.Instance;
                        using (var request = new HttpRequestMessage(HttpMethod.Get, this.url))
                        {
                            Snowbreak.SnowBreakHttpClient.SetUserAgent(request);
                            request.Headers.Host = this.url.Host;
                            using (var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                            {
                                response.EnsureSuccessStatusCode();

                                using (var contentStream = response.Content.ReadAsStream())
                                {
                                    var knownLength = response.Content.Headers.ContentLength;

                                    using (var fh = File.OpenHandle(tempFile, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, FileOptions.None, knownLength.HasValue ? knownLength.Value : 0))
                                    using (var fs = new FileStream(fh, FileAccess.ReadWrite, 0))
                                    {
                                        fs.Position = 0;
                                        var buffer = ArrayPool<byte>.Shared.Rent(1024 * 12); // 12KB
                                        try
                                        {
                                            int actualLengthToRead = Math.Min(buffer.Length, 1024 * 32); // Only read 32KB max, at a time
                                            int read = contentStream.Read(buffer, 0, actualLengthToRead);
                                            while (read > 0)
                                            {
                                                fs.Write(buffer, 0, read);
                                                read = contentStream.Read(buffer, 0, actualLengthToRead);
                                            }
                                        }
                                        finally
                                        {
                                            ArrayPool<byte>.Shared.Return(buffer);
                                        }
                                        fs.Flush();
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Evil!!!! But necessary?
                        try
                        {
                            File.Delete(tempFile);
                        }
                        catch
                        {

                        }
                        throw;
                    }
                    // If we reached here, meaning the file has been downloaded successfully.
                    // Change the temporary file to actual one.

                    // It can't exist but in case the user is pasting the file using File Explorer or something messed up happening outside of this process.
                    if (File.Exists(localFilename)) File.Delete(localFilename); 

                    // Finally it's done.
                    File.Move(tempFile, localFilename);

                    return localFilename;
                }
            }
        }

        private static string GetSha1OfFilename(ReadOnlySpan<char> url)
        {
            using (var sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1))
            {
                var bytes = MemoryMarshal.AsBytes(url);
                sha1.AppendData(bytes);
                var requiredLength = sha1.HashLengthInBytes;
                Span<byte> alloc = stackalloc byte[requiredLength];
                sha1.GetCurrentHash(alloc);
                return Convert.ToHexString(alloc);
            }
        }

        public void Dispose() => this.lockers.Clear();
    }
}
