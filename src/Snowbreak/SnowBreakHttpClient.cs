using Leayal.SnowBreakLauncher.Classes;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher.Snowbreak;

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
        Instance.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Safari/537.36");
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
    }

    private static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
    {
        AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
        Instance.Dispose();
    }

    private SnowBreakHttpClient(SocketsHttpHandler handler) : base(new HttpClientSecureDnsResolver(handler), true)
    {
    }

    public async Task<GameClientManifestData> GetGameClientManifestAsync(CancellationToken cancellationToken = default)
    {
        using (var req = new HttpRequestMessage(HttpMethod.Get, URL_GameClientManifest))
        {
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
            req.Headers.Host = url.Host;
            return this.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }  
    }

    public async Task<LauncherNewsHttpResponse> GetLauncherNewsAsync(CancellationToken cancellationToken = default)
    {
        using (var req = new HttpRequestMessage(HttpMethod.Get, URL_GameLauncherNews))
        {
            req.Headers.Host = URL_GameLauncherNews.Host;
            using (var response = await this.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return new LauncherNewsHttpResponse(jsonContent);
            }
        }
    }
}
