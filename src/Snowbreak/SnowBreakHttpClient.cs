using Leayal.SnowBreakLauncher.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TurnerSoftware.DinoDNS;
using TurnerSoftware.DinoDNS.Protocol;

namespace Leayal.SnowBreakLauncher.Snowbreak
{
    sealed class SnowBreakHttpClient : HttpClient
    {
        private static readonly Uri URL_GameClientPCData, URL_GameClientManifest, URL_GameLauncherNews;
        public static readonly SnowBreakHttpClient Instance;


        static SnowBreakHttpClient()
        {
            URL_GameClientPCData = new Uri($"https://snowbreak-dl.amazingseasuncdn.com/ob202307/PC/");
            URL_GameClientManifest = new Uri(URL_GameClientPCData, "manifest.json");
            URL_GameLauncherNews = new Uri("https://snowbreak-content.amazingseasuncdn.com/ob202307/webfile/launcher/launcher-information.json");
            Instance = new SnowBreakHttpClient(new SocketsHttpHandler()
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.All,
                Proxy = null,
                UseProxy = false,
                UseCookies = true,
            });
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        private static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
            Instance.Dispose();
        }

        public readonly DnsClient DnsClient;
        private readonly SemaphoreSlim semaphoreSlim;

        private SnowBreakHttpClient(SocketsHttpHandler handler) : base(handler, true)
        {
            this.DnsClient = ThreadSafeDnsClient.CreateNew();
            this.semaphoreSlim = new SemaphoreSlim(1, 1);
            handler.ConnectCallback = HttpClient_HandleConnect;
        }

        private async ValueTask<Stream> HttpClient_HandleConnect(SocketsHttpConnectionContext ctx, CancellationToken cancelToken)
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static Socket CreateNew()
            {
                var s = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                // s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                // s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 5);
                // s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);
                // s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 5);
                return s;
            }

            if (IPAddress.TryParse(ctx.DnsEndPoint.Host, out _))
            {
                Socket? s = null;
                try
                {
                    s = CreateNew();
                    await s.ConnectAsync(ctx.DnsEndPoint, cancelToken);
                }
                catch
                {
                    s?.Dispose();
                    throw;
                }
                return new NetworkStream(s, ownsSocket: true);
            }
            else
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static async ValueTask<DnsMessage> DnsResolve(SemaphoreSlim semaphoreSlim, DnsClient client, string hostname, CancellationToken cancelToken)
                {
                    await semaphoreSlim.WaitAsync();
                    try
                    {
#if NO_IPV6
                        return await client.QueryAsync(hostname, DnsQueryType.A, DnsClass.IN, cancelToken);
#else
                        try
                        {
                            return await client.QueryAsync(hostname, UseIPv6 ? DnsQueryType.AAAA : DnsQueryType.A, DnsClass.IN, cancelToken);
                        }
                        catch (System.Security.Authentication.AuthenticationException)
                        {
                            return await client.QueryAsync(hostname, DnsQueryType.A, DnsClass.IN, cancelToken);
                        }
#endif
                    }
                    finally
                    {
                        semaphoreSlim.Release();
                    }
                }
                var dnsMessage = await DnsResolve(this.semaphoreSlim, this.DnsClient, ctx.DnsEndPoint.Host, cancelToken);

                Socket? s = null;
                var walker = dnsMessage.Answers.WithARecords().GetEnumerator();
                if (walker.MoveNext())
                {
                    while (true)
                    {
                        var record = walker.Current;
                        var ipaddr = record.ToIPAddress();
                        var newEndpoint = new DnsEndPoint(ipaddr.ToString(), ctx.DnsEndPoint.Port, ipaddr.AddressFamily);
                        try
                        {
                            s = CreateNew();
                            await s.ConnectAsync(newEndpoint, cancelToken);
                            break;
                        }
                        catch
                        {
                            s?.Dispose();
                            if (!walker.MoveNext())
                            {
                                throw;
                            }
                        }
                    }
                    return new NetworkStream(s, ownsSocket: true);
                }
                throw new DnsResolveFailureException();
            }
        }

        // All the mess above is just for handling DNS Resolve with custom DNS Client.

        public static void SetUserAgent(HttpRequestMessage request)
        {
            if (request.Headers.Contains("User-Agent"))
            {
                request.Headers.UserAgent.Clear();
            }
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Safari/537.36");
        }

        public async Task<GameClientManifestData> GetGameClientManifestAsync(CancellationToken cancellationToken = default)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, URL_GameClientManifest))
            {
                SetUserAgent(req);
                req.Headers.Host = URL_GameClientManifest.Host;
                using (var response = await this.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();

                    var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    var jsonDoc = JsonDocument.Parse(jsonContent, new JsonDocumentOptions() { CommentHandling = JsonCommentHandling.Skip });
                    return new GameClientManifestData(jsonDoc);
                }  
            }
        }

        public Task<HttpResponseMessage> GetFileDownloadResponseAsync(in GameClientManifestData manifest, string filename, CancellationToken cancellationToken = default)
        {
            if (filename.Length == 0) ArgumentException.ThrowIfNullOrWhiteSpace(filename, nameof(filename));

            var url = new Uri(URL_GameClientPCData, Path.Join(manifest.pathOffset, filename));
            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                SetUserAgent(req);
                req.Headers.Host = url.Host;
                return this.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }  
        }

        public async Task<LauncherNewsHttpResponse> GetLauncherNewsAsync(CancellationToken cancellationToken = default)
        {
            using (var req = new HttpRequestMessage(HttpMethod.Get, URL_GameLauncherNews))
            {
                SetUserAgent(req);
                req.Headers.Host = URL_GameLauncherNews.Host;
                using (var response = await this.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();

                    var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    return new LauncherNewsHttpResponse(jsonContent);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                semaphoreSlim.Dispose();
            }
        }
    }
}
