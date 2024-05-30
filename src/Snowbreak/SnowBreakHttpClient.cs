﻿using Leayal.SnowBreakLauncher.Classes;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher.Snowbreak;

sealed class SnowBreakHttpClient : HttpClient
{
    private const string TemplateURL_RemoteDataResources = "https://snowbreak-dl.amazingseasuncdn.com/DLC{0}/PC/updates/"; // {0} was for string.Format method.
    private static readonly Uri URL_GameClientPredownloadManifest, URL_GameLauncherNews, URL_LauncherLatestVersion, URL_LauncherManifest;
    public static readonly SnowBreakHttpClient Instance;

    static SnowBreakHttpClient()
    {
        URL_GameClientPredownloadManifest = new Uri("https://snowbreak-dl.amazingseasuncdn.com/pre-release/PC/updates/");
        URL_GameLauncherNews = new Uri("https://snowbreak-content.amazingseasuncdn.com/ob202307/webfile/launcher/launcher-information.json");
        URL_LauncherLatestVersion = new Uri("https://snowbreak-content.amazingseasuncdn.com/ob202307/launcher/seasun/updates/latest");
        URL_LauncherManifest = new Uri("https://leayal.github.io/SnowBreakLauncher-Dotnet/publish/v1/launcher-manifest.json");

        // We put the config reading here so that the static class still follow .NET lazy static initialization mechanism.
        var useDoH = (App.Current is App app) ? app.LeaLauncherConfig.Networking_UseDoH : true;

        Instance = new SnowBreakHttpClient(new SocketsHttpHandler()
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            Proxy = null,
            UseProxy = false,
            UseCookies = true,
        })
        {
            EnableDnsOverHttps = useDoH
        };
        Instance.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Safari/537.36");
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
    }

    private static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
    {
        AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
        Instance.Dispose();
    }

    private readonly HttpClientSecureDnsResolver doHHandler;

    public bool EnableDnsOverHttps
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.doHHandler.IsEnabled;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => this.doHHandler.IsEnabled = value;
    }

    private SnowBreakHttpClient(SocketsHttpHandler handler) : this(new HttpClientSecureDnsResolver(handler), true)
    {
    }

    private SnowBreakHttpClient(HttpClientSecureDnsResolver handler, bool disposeBaseHandler) : base(handler, disposeBaseHandler)
    {
        this.doHHandler = handler;
    }

    public async Task<Uri> FetchResourceURL(CancellationToken cancellationToken = default)
    {
        var t_launcherLatestVersion = this.GetGameLauncherLatestVersionAsync(cancellationToken);

        using (var req = new HttpRequestMessage(HttpMethod.Get, URL_LauncherManifest))
        {
            req.Headers.Host = URL_LauncherManifest.Host;
            using (var response = await this.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var str_launcherVersion = await t_launcherLatestVersion;

                return CheatingThe_AsyncNoSpanRule(in str_launcherVersion, in jsonContent);

                static Uri CheatingThe_AsyncNoSpanRule(in string str_launcherVersion, in string jsonContent)
                {
                    var spanStr_launcherVersion = str_launcherVersion.AsSpan().Trim();
                    using (var jsonDoc = JsonDocument.Parse(jsonContent, new JsonDocumentOptions() { CommentHandling = JsonCommentHandling.Skip }))
                    {
                        var rootEle = jsonDoc.RootElement;

                        Uri? resourceSrc = null;

                        if ((rootEle.TryGetProperty("overrides", out var prop_targetOverridenList) && prop_targetOverridenList.ValueKind == JsonValueKind.Object)
                            && (prop_targetOverridenList.TryGetProperty(spanStr_launcherVersion, out var prop_targetOverriden) && prop_targetOverriden.ValueKind == JsonValueKind.String))
                        {
                            var overrideString = prop_targetOverriden.GetString();
                            if (!string.IsNullOrWhiteSpace(overrideString))
                            {
                                resourceSrc = new Uri(overrideString.EndsWith('/') ? overrideString : (overrideString + '/'));
                            }
                        }

                        // Attempt to craft the URL from the launcher version.
                        // As of writing this (27th April 2024), the minor version of launcher match with the DLC index of the URL.
                        // This should make the compatibility last for as long as they don't change this behavior
                        if (resourceSrc == null && Version.TryParse(spanStr_launcherVersion, out var versionObj) && versionObj.Minor > 0)
                        {
                            resourceSrc = new Uri(string.Format(TemplateURL_RemoteDataResources, versionObj.Minor));
                        }
                        
                        if (resourceSrc == null)
                        {
                            var defaultUrl = rootEle.GetProperty("default").GetString();
                            if (string.IsNullOrEmpty(defaultUrl)) throw new ArgumentOutOfRangeException();
                            resourceSrc = new Uri(defaultUrl.EndsWith('/') ? defaultUrl : (defaultUrl + '/'));
                        }

                        return resourceSrc;
                    }
                }
            }
        }
    }

    public async Task<GameClientManifestData> GetGameClientManifestAsync(CancellationToken cancellationToken = default)
    {
        var URL_GameClientPCData = await this.FetchResourceURL(cancellationToken);
        return await this.Inner_GetGameClientManifestAsync(URL_GameClientPCData, cancellationToken);
    }

    public Task<GameClientManifestData> GetGamePredownloadManifestAsync(CancellationToken cancellationToken = default)
        => this.Inner_GetGameClientManifestAsync(URL_GameClientPredownloadManifest, cancellationToken);

    private async Task<GameClientManifestData> Inner_GetGameClientManifestAsync(Uri uri_base, CancellationToken cancellationToken = default)
    {
        var uri_manifest = new Uri(uri_base, "manifest.json");
        using (var req = new HttpRequestMessage(HttpMethod.Get, uri_manifest))
        {
            req.Headers.Host = uri_manifest.Host;
            using (var response = await this.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonDoc = JsonDocument.Parse(jsonContent, new JsonDocumentOptions() { CommentHandling = JsonCommentHandling.Skip });
                return new GameClientManifestData(jsonDoc, uri_base);
            }
        }
    }

    private async Task<string> GetGameLauncherLatestVersionAsync(CancellationToken cancellationToken = default)
    {
        using (var req = new HttpRequestMessage(HttpMethod.Get, URL_LauncherLatestVersion))
        {
            req.Headers.Host = URL_LauncherLatestVersion.Host;
            using (var response = await this.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
        }
    }

    public Task<HttpResponseMessage> GetFileDownloadResponseFromFileHashAsync(in GameClientManifestData manifest, string fileHash, CancellationToken cancellationToken = default)
    {
        var URL_GameClientPCData = manifest.AssociatedUrl;
#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrWhiteSpace(fileHash);
        ArgumentNullException.ThrowIfNull(URL_GameClientPCData);
#else
        if (string.IsNullOrWhiteSpace(fileHash)) throw new ArgumentException(null, nameof(fileHash));
        if (URL_GameClientPCData == null) throw new ArgumentNullException();
#endif

        var url = new Uri(URL_GameClientPCData, Path.Join(manifest.pathOffset, fileHash));
        using (var req = new HttpRequestMessage(HttpMethod.Get, url))
        {
            req.Headers.Host = url.Host;
            return this.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }  
    }

    public Task<HttpResponseMessage> GetFileDownloadResponseAsync(in GameClientManifestData manifest, in PakEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry.Equals(PakEntry.Empty)) throw new ArgumentNullException(nameof(entry));
        // ArgumentException.ThrowIfNullOrWhiteSpace(entry.hash, nameof(entry));

        return this.GetFileDownloadResponseFromFileHashAsync(in manifest, entry.hash, cancellationToken);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.doHHandler.Dispose();
        }
        base.Dispose(disposing);
    }
}
