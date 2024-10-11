using Leayal.SnowBreakLauncher.Classes;
using Leayal.SnowBreakLauncher.LeaHelpers;
using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher.Snowbreak;

sealed class SnowBreakHttpClient : HttpClient
{
    private const int ScanSettings_DownloadSizeLimit = 1024 * 1024 * 500, // Limit 500MB as we don't want potential risk of bombing.

        // 1MB chunk each pull, if this is too big, around 500KB is optimal choice, too.
        // This isn't really matter unless your internet is unstable and timeout may happend before 1MB is fully downloaded.
        // By reducing the size, we can meet the time limit per each "Read" operation.
        DownloadChunkSize = 1024 * 1024;

    private const string TemplateURL_RemoteDataResource = "https://{0}/{1}/PC/updates/", // {0}, {1} was for string.Format method.
        TemplateURL_LauncherBaseUrl = "https://snowbreak-content.amazingseasuncdn.com/ob202307",
        TemplateURL_LauncherBinary = TemplateURL_LauncherBaseUrl + "/launcher/seasun/updates/{0}.exe",
        
        Hardcoded_ScanSettings_StartString = "{\"appUpdateURL\":\"https://snowbreak-content.amazingseasuncdn.com/ob202307/launcher/seasun/updates/\"",
        Hardcoded_ScanSettings_EndString = "}";

    
    private static readonly Uri URL_GameClientPredownloadManifest, URL_GameLauncherNews, URL_LauncherLatestVersion, URL_LauncherManifest;
    public static readonly SnowBreakHttpClient Instance;
    private static readonly string[] TemplateURL_RemoteResourceDomainNames =
    {
        "snowbreak-dl.amazingseasuncdn.com",
        "snowbreak-dl-cy.amazingseasuncdn.com",
        "snowbreak-dl-akm.amazingseasuncdn.com"
    };

    static SnowBreakHttpClient()
    {
        URL_GameClientPredownloadManifest = new Uri("https://snowbreak-dl.amazingseasuncdn.com/pre-release/PC/updates/");
        URL_GameLauncherNews = new Uri(UrlHelper.MakeAbsoluteUrl(TemplateURL_LauncherBaseUrl, "webfile/launcher/launcher-information.json", true));
        URL_LauncherLatestVersion = new Uri(UrlHelper.MakeAbsoluteUrl(TemplateURL_LauncherBaseUrl, "launcher/seasun/updates/latest", true));
        URL_LauncherManifest = new Uri("https://leayal.github.io/SnowBreakLauncher-Dotnet/publish/v2/launcher-manifest.json");

        // We put the config reading here so that the static class still follow .NET lazy static initialization mechanism.
        var useDoH = true;
        string? proxyUrl = null;
        if (App.Current is App app)
        {
            useDoH = app.LeaLauncherConfig.Networking_UseDoH;
            proxyUrl = app.proxyUrl;
        }

        bool hasProxy = !string.IsNullOrWhiteSpace(proxyUrl);

        Instance = new SnowBreakHttpClient(new SocketsHttpHandler()
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip,
            Proxy = hasProxy ? new WebProxy(proxyUrl) : null,
            UseProxy = hasProxy,
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static JsonDocumentOptions GetDefaultJsonDocumentReaderSettings() => new JsonDocumentOptions() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    // This is an ugly mess, but I'm too lazy to make it human-readable.
    // The main purpose is that, if allowed:
    // This launcher will download a small part of the launcher, then search for the manifest data inside the downloaded data, if the search yields no results, continue download another small part.
    // This repeat either until the whole launcher is downloaded or the search found result(s).
    // This provides transparent compatibility and allow this launcher to survive longer without code changes....(again) as long as the publisher doesn't change their way.
    public async Task<Uri[]> FetchResourceURL(bool allowOfficialFetchingFromLauncher, bool allowMemoryHunryMode, CancellationToken cancellationToken = default)
    {
        var t_launcherLatestVersion = this.GetGameLauncherLatestVersionAsync(cancellationToken);

        string thislauncherManifestData;
        using (var req = new HttpRequestMessage(HttpMethod.Get, URL_LauncherManifest))
        {
            req.Headers.Host = URL_LauncherManifest.Host;
            using (var response = await this.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                thislauncherManifestData = await response.Content.ReadAsStringAsync(cancellationToken);
            }
        }

        var launcherLatestVersion = await t_launcherLatestVersion;

        if (!allowOfficialFetchingFromLauncher)
        {
            OfficialLauncherManifestJsonParseAndMerge(thislauncherManifestData, null, launcherLatestVersion, out _, out var resourceSrc, null, null, cancellationToken);
            return resourceSrc;
        }

        string scannerSettings_start = Hardcoded_ScanSettings_StartString,
            scannerSettings_end = Hardcoded_ScanSettings_EndString;
        using (var scanSettingParser = JsonDocument.Parse(thislauncherManifestData, GetDefaultJsonDocumentReaderSettings()))
        {
            if (scanSettingParser.RootElement.TryGetProperty("scannerSettings", out var prop_scannerSettings) && prop_scannerSettings.ValueKind == JsonValueKind.Object)
            {
                if (prop_scannerSettings.TryGetProperty("start", out var prop_scannerSettings_start) && prop_scannerSettings_start.ValueKind == JsonValueKind.String)
                {
                    var val_scannerSettings_start = prop_scannerSettings_start.GetString();
                    if (!string.IsNullOrEmpty(val_scannerSettings_start))
                    {
                        scannerSettings_start = val_scannerSettings_start;
                    }
                }
                if (prop_scannerSettings.TryGetProperty("end", out var prop_scannerSettings_end) && prop_scannerSettings_end.ValueKind == JsonValueKind.String)
                {
                    var val_scannerSettings_end = prop_scannerSettings_end.GetString();
                    if (!string.IsNullOrEmpty(val_scannerSettings_end))
                    {
                        scannerSettings_end = val_scannerSettings_end;
                    }
                }
            }
        }

        var path_cacheDir = Path.GetFullPath("cacheData", AppContext.BaseDirectory);
        var path_launcherManifestCachedData = Path.Join(path_cacheDir, "officiallaunchermanifest-latest.json");

        bool isOkay = false;
        if (File.Exists(path_launcherManifestCachedData))
        {
            using (var streamReader = new StreamReader(path_launcherManifestCachedData, Encoding.UTF8))
            {
                var cachedOfficialLauncherManifestData = streamReader.ReadLine();
                if (!string.IsNullOrWhiteSpace(cachedOfficialLauncherManifestData))
                {
                    using (var jsonDoc = JsonDocument.Parse(cachedOfficialLauncherManifestData, GetDefaultJsonDocumentReaderSettings()))
                    {
                        if (OfficialLauncherManifestJsonParseAndMerge(thislauncherManifestData, jsonDoc, launcherLatestVersion, out _, out var resourceSrc, null, null, cancellationToken))
                        {
                            return resourceSrc;
                        }
                    }
                }
            }
        }

        string? officialJsonDataRaw = null;

        // This whole thing is even more expensive
        if (!isOkay)
        {
            _ = Directory.CreateDirectory(path_cacheDir);

            var url = new Uri(string.Format(TemplateURL_LauncherBinary, launcherLatestVersion));

            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                using (var response = await this.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    var header_contentLength = response.Content.Headers.ContentLength;
                    
                    long contentLength;
                    int byteToDownload;
                    TaskAllocatedResources_FetchResourceURL? resource;
                    if (header_contentLength.HasValue && (contentLength = header_contentLength.Value) > 0)
                    {
                        byteToDownload = (int)Math.Min(contentLength, ScanSettings_DownloadSizeLimit); // If the known size exceeds our limit, restrain it.
                        resource = new TaskAllocatedResources_FetchResourceURL(allowMemoryHunryMode, byteToDownload);
                    }
                    else
                    {
                        byteToDownload = ScanSettings_DownloadSizeLimit; // Since we don't know the size of launcher before downloading. Follow our upper limit and abort if the download exceeds limit..

                        /* We can fall-back to use disk in case the download size is unknown
                         * And because we're using disk without knowing file size, pre-allocating 0 byte is prefered.
                        resource = new TaskAllocatedResources_FetchResourceURL(false, 0);
                        */
                        // Or we will respect our user's choice with full sincere: Still use memory space.
                        // This will take a huge hit because we will allocate a "ScanSettings_DownloadSizeLimit" worth of memory. (500MB if you don't change settings, it's really huge and we may not even use 10% of it)
                        // In case you don't want this behavior, either comment the next line while uncomment the line above to change behavior.
                        resource = new TaskAllocatedResources_FetchResourceURL(allowMemoryHunryMode, allowMemoryHunryMode ? ScanSettings_DownloadSizeLimit : 0);
                        // resource = new TaskAllocatedResources_FetchResourceURL(false, 0); // Uncomment this line while comment the line above to change behavior.
                    }

                    var encodingANSICompat = Encoding.GetEncoding(28591); // 28591 is ANSI-compatible
                    // var searchBytes = encodingANSICompat.GetBytes($"\"appVersion\":\"{launcherLatestVersion}\"");
                    var searchStartingBytes = encodingANSICompat.GetBytes(scannerSettings_start);
                    var searchEngine_Start = new BoyerMooreHorspool(searchStartingBytes);
                    var searchEndingBytes = encodingANSICompat.GetBytes(scannerSettings_end);
                    var searchEngine_End = new BoyerMooreHorspool(searchEndingBytes);

                    byte[]? tmpbuffer = null;
                    try
                    {
                        int chunkRead = 0, byteRead = 0;
                        long lastScanPosStart = 0, lastScanPosEnd = 0, scanFoundStart = -1, scanFoundEnd = -1;

                        using (var responseContentStream = response.Content.ReadAsStream(cancellationToken))
                        {
                            // We can risk ourselves when the contentlength is unknown because it could be over our limit
                            if (resource.IsMemoryHungry)
                            {
                                var arr = resource.arr;
                                try
                                {
                                    while (!cancellationToken.IsCancellationRequested && (chunkRead = responseContentStream.Read(arr, byteRead, Math.Min(byteToDownload, DownloadChunkSize))) != 0)
                                    {
                                        byteRead += chunkRead;
                                        byteToDownload -= chunkRead;
                                        if (((byteRead - lastScanPosStart) >= DownloadChunkSize) || byteToDownload <= 0)
                                        {
                                            var startScanPos = (int)(lastScanPosStart == 0 ? 0 : (lastScanPosStart - searchStartingBytes.Length));
                                            scanFoundStart = searchEngine_Start.IndexOf(arr, startScanPos, byteRead);

                                            if (scanFoundStart != -1)
                                            {
                                                scanFoundStart += startScanPos;
                                                lastScanPosStart = scanFoundStart + 1;
                                                break;
                                            }
                                            else
                                            {
                                                lastScanPosStart = byteRead;
                                            }
                                        }
                                    }
                                    if (lastScanPosStart < byteRead)
                                    {
                                        var startScanPos = (int)(lastScanPosEnd == 0 ? lastScanPosStart : (lastScanPosStart - searchEndingBytes.Length));
                                        scanFoundEnd = searchEngine_End.IndexOf(arr, startScanPos, byteRead);

                                        if (scanFoundEnd != -1)
                                        {
                                            scanFoundEnd += startScanPos;
                                            lastScanPosEnd = scanFoundEnd + 1;
                                        }
                                        else
                                        {
                                            lastScanPosEnd = byteRead;
                                        }
                                    }
                                    if (scanFoundEnd == -1)
                                    {
                                        while (!cancellationToken.IsCancellationRequested && (chunkRead = responseContentStream.Read(arr, byteRead, Math.Min(byteToDownload, DownloadChunkSize))) != 0)
                                        {
                                            byteRead += chunkRead;
                                            byteToDownload -= chunkRead;
                                            if (((byteRead - lastScanPosEnd) >= DownloadChunkSize) || byteToDownload <= 0)
                                            {
                                                var startScanPos = (int)(lastScanPosEnd == 0 ? lastScanPosStart : (lastScanPosStart - searchEndingBytes.Length));
                                                scanFoundEnd = searchEngine_End.IndexOf(arr, startScanPos, byteRead);

                                                if (scanFoundEnd != -1)
                                                {
                                                    scanFoundEnd += startScanPos;
                                                    lastScanPosEnd = scanFoundEnd + 1;
                                                    break;
                                                }
                                                else
                                                {
                                                    lastScanPosEnd = byteRead;
                                                }
                                            }
                                        }
                                    }

                                    if (scanFoundStart != -1 && scanFoundEnd != -1)
                                    {
                                        var jsonLengthInBytes = (int)((scanFoundEnd + 1) - scanFoundStart);
                                        officialJsonDataRaw = encodingANSICompat.GetString(arr, (int)scanFoundStart, jsonLengthInBytes);
                                    }
                                }
                                finally
                                {
                                    arr = null;
                                }
                            }
                            else
                            {
                                var localStream = resource.contentStream;
                                localStream.Position = 0;
                                tmpbuffer = ArrayPool<byte>.Shared.Rent(DownloadChunkSize);
                                while (!cancellationToken.IsCancellationRequested && (chunkRead = responseContentStream.Read(tmpbuffer, 0, Math.Min(tmpbuffer.Length, DownloadChunkSize))) != 0)
                                {
                                    localStream.Position = byteRead;
                                    byteRead += chunkRead;
                                    byteToDownload -= chunkRead;
                                    localStream.Write(tmpbuffer, 0, chunkRead);
                                    if (((byteRead - lastScanPosStart) >= DownloadChunkSize) || byteToDownload <= 0)
                                    {
                                        var startScanPos = lastScanPosStart == 0 ? 0 : (lastScanPosStart - searchStartingBytes.Length);
                                        localStream.Position = startScanPos;
                                        scanFoundStart = searchEngine_Start.IndexOf(localStream);

                                        if (scanFoundStart != -1)
                                        {
                                            scanFoundStart += startScanPos;
                                            lastScanPosStart = scanFoundStart + 1;
                                            break;
                                        }
                                        else
                                        {
                                            lastScanPosStart = byteRead;
                                        }
                                    }
                                }
                                if (lastScanPosStart < byteRead)
                                {
                                    var startScanPos = lastScanPosEnd == 0 ? lastScanPosStart : (lastScanPosStart - searchEndingBytes.Length);
                                    localStream.Position = startScanPos;
                                    scanFoundEnd = searchEngine_End.IndexOf(localStream);

                                    if (scanFoundEnd != -1)
                                    {
                                        scanFoundEnd += startScanPos;
                                        lastScanPosEnd = scanFoundEnd + 1;
                                    }
                                    else
                                    {
                                        lastScanPosEnd = byteRead;
                                    }
                                }
                                if (scanFoundEnd == -1)
                                {
                                    while (!cancellationToken.IsCancellationRequested && (chunkRead = responseContentStream.Read(tmpbuffer, 0, Math.Min(tmpbuffer.Length, DownloadChunkSize))) != 0)
                                    {
                                        localStream.Position = byteRead;
                                        byteRead += chunkRead;
                                        byteToDownload -= chunkRead;
                                        localStream.Write(tmpbuffer, 0, chunkRead);
                                        if (((byteRead - lastScanPosEnd) >= DownloadChunkSize) || byteToDownload <= 0)
                                        {
                                            var startScanPos = lastScanPosEnd == 0 ? lastScanPosStart : (lastScanPosStart - searchEndingBytes.Length);
                                            localStream.Position = startScanPos;
                                            scanFoundEnd = searchEngine_End.IndexOf(localStream);

                                            if (scanFoundEnd != -1)
                                            {
                                                scanFoundEnd += startScanPos;
                                                lastScanPosEnd = scanFoundEnd + 1;
                                                break;
                                            }
                                            else
                                            {
                                                lastScanPosEnd = byteRead;
                                            }
                                        }
                                    }
                                }

                                if (scanFoundStart != -1 && scanFoundEnd != -1)
                                {
                                    var jsonLengthInBytes = (int)((scanFoundEnd + 1) - scanFoundStart);
                                    if (tmpbuffer.Length >= jsonLengthInBytes)
                                    {
                                        localStream.Position = scanFoundStart;
                                        await localStream.ReadExactlyAsync(tmpbuffer, 0, jsonLengthInBytes, cancellationToken);
                                        officialJsonDataRaw = encodingANSICompat.GetString(tmpbuffer, 0, jsonLengthInBytes);
                                    }
                                    else
                                    {
                                        var encodeBuffer = ArrayPool<byte>.Shared.Rent(jsonLengthInBytes);
                                        try
                                        {
                                            localStream.Position = scanFoundStart;
                                            await localStream.ReadExactlyAsync(encodeBuffer, 0, jsonLengthInBytes, cancellationToken);
                                            officialJsonDataRaw = encodingANSICompat.GetString(encodeBuffer, 0, jsonLengthInBytes);
                                        }
                                        finally
                                        {
                                            ArrayPool<byte>.Shared.Return(encodeBuffer);
                                            encodeBuffer = null;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (tmpbuffer != null)
                        {
                            ArrayPool<byte>.Shared.Return(tmpbuffer);
                            tmpbuffer = null;
                        }
                        Interlocked.Exchange(ref resource, null).Dispose();
                    }
                }
            }
        }

        if (officialJsonDataRaw == null)
        {
            // In case we still can't find manifest from the official launcher.
            // Fall-back to reading the data from the .json file which exists on the repo.
            OfficialLauncherManifestJsonParseAndMerge(thislauncherManifestData, null, launcherLatestVersion, out _, out var resourceSrc, path_launcherManifestCachedData, null, cancellationToken);
            return resourceSrc;
        }
        else
        {
            using (var jsonDoc = JsonDocument.Parse(officialJsonDataRaw, GetDefaultJsonDocumentReaderSettings()))
            {
                OfficialLauncherManifestJsonParseAndMerge(thislauncherManifestData, jsonDoc, launcherLatestVersion, out var task_writeCache, out var resourceSrc, path_launcherManifestCachedData, officialJsonDataRaw, cancellationToken);
                if (task_writeCache != null) await task_writeCache;
                return resourceSrc;
            }
        }
    }

    private static bool OfficialLauncherManifestJsonParseAndMerge(string thislauncherManifestData, JsonDocument? jsonDoc, string launcherLatestVersion, out Task? writeCache, out Uri[] resourceUris, string? path_launcherManifestCachedData, string? jsonDataRaw, CancellationToken cancellationToken)
    {
        string strTemplate_RemoteDataResource;
        string[]? domainNames = null;

        bool officialLauncherManifestDataIsOkay = false;
        string? hashOfBranch = null;

        if (jsonDoc != null && jsonDoc.RootElement.TryGetProperty("appVersion", out var prop_appVersion) && prop_appVersion.ValueKind == JsonValueKind.String)
        {
            var manifestVersionString = prop_appVersion.GetString();
            if (!string.IsNullOrEmpty(manifestVersionString) && MemoryExtensions.Equals(manifestVersionString.AsSpan().Trim(), launcherLatestVersion, StringComparison.OrdinalIgnoreCase))
            {
                officialLauncherManifestDataIsOkay = true;
                if (!string.IsNullOrEmpty(path_launcherManifestCachedData) && jsonDataRaw != null && jsonDataRaw.Length != 0)
                {
                    writeCache = File.WriteAllTextAsync(path_launcherManifestCachedData, jsonDataRaw, cancellationToken);
                }
                else
                {
                    writeCache = null;
                }
            }
            else
            {
                officialLauncherManifestDataIsOkay = false;
                writeCache = null;
            }
        }
        else
        {
            writeCache = null;
        }

        if (jsonDoc != null && officialLauncherManifestDataIsOkay && jsonDoc.RootElement.TryGetProperty("branch", out var prop_branch) && prop_branch.ValueKind == JsonValueKind.String)
        {
            var manifestHashBranch = prop_branch.GetString();
            if (!string.IsNullOrEmpty(manifestHashBranch))
            {
                hashOfBranch = manifestHashBranch;
                officialLauncherManifestDataIsOkay = true;
            }
            else
            {
                officialLauncherManifestDataIsOkay = false;
            }
        }
        else
        {
            officialLauncherManifestDataIsOkay = false;
        }

        using (var thislauncherManifestJson = JsonDocument.Parse(thislauncherManifestData, GetDefaultJsonDocumentReaderSettings()))
        {
            var rootEle = thislauncherManifestJson.RootElement;
            if (rootEle.TryGetProperty("templateURL_RemoteDataResource", out var prop_templateURL_RemoteDataResource) && prop_templateURL_RemoteDataResource.ValueKind == JsonValueKind.String)
            {
                strTemplate_RemoteDataResource = prop_templateURL_RemoteDataResource.GetString() ?? TemplateURL_RemoteDataResource;
            }
            else
            {
                strTemplate_RemoteDataResource = TemplateURL_RemoteDataResource;
            }
            if (officialLauncherManifestDataIsOkay && jsonDoc != null && jsonDoc.RootElement.TryGetProperty("gameUpdateCDN", out var prop_gameUpdateCDN) && prop_gameUpdateCDN.ValueKind == JsonValueKind.Array)
            {
                var totalUrlCount = prop_gameUpdateCDN.GetArrayLength();
                if (totalUrlCount != 0)
                {
                    domainNames = new string[totalUrlCount];
                    int i = 0;
                    foreach (var element_domain in prop_gameUpdateCDN.EnumerateArray())
                    {
                        if (element_domain.ValueKind == JsonValueKind.String)
                        {
                            var url = element_domain.GetString();
                            if (!string.IsNullOrEmpty(url))
                            {
                                if (Uri.TryCreate(url, UriKind.Absolute, out var createdUri))
                                {
                                    domainNames[i++] = createdUri.Host;
                                }
                                else
                                {
                                    domainNames[i++] = url;
                                }
                            }
                        }
                    }
                    if (i != domainNames.Length) Array.Resize(ref domainNames, i);
                }
            }
            else if (rootEle.TryGetProperty("domains", out var prop_domains) && prop_domains.ValueKind == JsonValueKind.Array)
            {
                var totalUrlCount = prop_domains.GetArrayLength();
                if (totalUrlCount != 0)
                {
                    domainNames = new string[totalUrlCount];
                    int i = 0;
                    foreach (var element_domain in prop_domains.EnumerateArray())
                    {
                        if (element_domain.ValueKind == JsonValueKind.String)
                        {
                            var domainName = element_domain.GetString();
                            if (!string.IsNullOrEmpty(domainName)) domainNames[i++] = domainName;
                        }
                    }
                    if (i != domainNames.Length) Array.Resize(ref domainNames, i);
                }
            }
            if (domainNames == null)
            {
                domainNames = TemplateURL_RemoteResourceDomainNames;
            }

            Uri[]? resourceSrc = null;

            if (officialLauncherManifestDataIsOkay && !string.IsNullOrEmpty(hashOfBranch))
            {
                // This whole thing is expensive
                var urlDeDuplication = new HashSet<string>();
                int i;
                for (i = 0; i < domainNames.Length; i++)
                {
                    urlDeDuplication.Add(string.Format(strTemplate_RemoteDataResource, domainNames[i], hashOfBranch));
                }
                resourceSrc = new Uri[urlDeDuplication.Count];
                i = 0;
                foreach (var remoteResourceUrl in urlDeDuplication)
                {
                    resourceSrc[i++] = new Uri(remoteResourceUrl.EndsWith('/') ? remoteResourceUrl : (remoteResourceUrl + '/'));
                }
            }
            else if ((rootEle.TryGetProperty("overrides", out var prop_targetOverridenList) && prop_targetOverridenList.ValueKind == JsonValueKind.Object)
                && (prop_targetOverridenList.TryGetProperty(launcherLatestVersion, out var prop_targetOverriden)) && prop_targetOverriden.ValueKind == JsonValueKind.String)
            {
                var overrideString = prop_targetOverriden.GetString();
                if (!string.IsNullOrWhiteSpace(overrideString))
                {
                    // This whole thing is expensive
                    var urlDeDuplication = new HashSet<string>();
                    int i;
                    for (i = 0; i < domainNames.Length; i++)
                    {
                        urlDeDuplication.Add(string.Format(overrideString, domainNames[i]));
                    }
                    resourceSrc = new Uri[urlDeDuplication.Count];
                    i = 0;
                    foreach (var remoteResourceUrl in urlDeDuplication)
                    {
                        resourceSrc[i++] = new Uri(remoteResourceUrl.EndsWith('/') ? remoteResourceUrl : (remoteResourceUrl + '/'));
                    }
                }
            }

            if (resourceSrc == null)
            {
                var defaultUrl = rootEle.GetProperty("default").GetString();
                if (string.IsNullOrEmpty(defaultUrl)) throw new ArgumentOutOfRangeException();
                var urlDeDuplication = new HashSet<string>();
                int i;
                for (i = 0; i < domainNames.Length; i++)
                {
                    urlDeDuplication.Add(string.Format(defaultUrl, domainNames[i]));
                }
                resourceSrc = new Uri[urlDeDuplication.Count];
                i = 0;
                foreach (var remoteResourceUrl in urlDeDuplication)
                {
                    resourceSrc[i++] = new Uri(remoteResourceUrl.EndsWith('/') ? remoteResourceUrl : (remoteResourceUrl + '/'));
                }
            }

            resourceUris = resourceSrc;
        }

        return officialLauncherManifestDataIsOkay;
    }

    sealed class TaskAllocatedResources_FetchResourceURL : IDisposable
    {
        [MemberNotNullWhen(true, nameof(arr)), MemberNotNullWhen(false, nameof(contentStream), nameof(fHandle))]
        public bool IsMemoryHungry { get; }
        public readonly byte[]? arr;
        public readonly FileStream? contentStream;
        public readonly SafeFileHandle? fHandle;

        public TaskAllocatedResources_FetchResourceURL(bool isMemoryHungry, int sizeHint)
        {
            this.IsMemoryHungry = isMemoryHungry;
            if (isMemoryHungry)
            {
                this.arr = ArrayPool<byte>.Shared.Rent(sizeHint);
            }
            else
            {
                var tmpFile = Path.GetFullPath(Path.GetRandomFileName(), Path.GetTempPath());
                this.fHandle = File.OpenHandle(tmpFile, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.DeleteOnClose, sizeHint);
                this.contentStream = new FileStream(fHandle, FileAccess.ReadWrite, 0) { Position = 0 };
            }
        }

        public void Dispose()
        {
            if (this.arr != null)
            {
                ArrayPool<byte>.Shared.Return(arr);
            }
            this.contentStream?.Dispose();
            this.fHandle?.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    public async Task<GameClientManifestData> GetGameClientManifestAsync(CancellationToken cancellationToken = default)
    {
        bool allowFetchingOfficialLauncherManifestData = true, allowFetchingOfficialLauncherManifestDataInMemory = false;
        if (App.Current is App app)
        {
            allowFetchingOfficialLauncherManifestData = app.LeaLauncherConfig.AllowFetchingOfficialLauncherManifestData;
            allowFetchingOfficialLauncherManifestDataInMemory = app.LeaLauncherConfig.AllowFetchingOfficialLauncherManifestDataInMemory;
        }
        var URLs_GameClientPCData = await this.FetchResourceURL(allowFetchingOfficialLauncherManifestData, allowFetchingOfficialLauncherManifestDataInMemory, cancellationToken);
        foreach (var URL_GameClientPCData in URLs_GameClientPCData)
        {
            try
            {
                return await this.Inner_GetGameClientManifestAsync(URL_GameClientPCData, cancellationToken);
            }
            // If error 404, attempt to try on the different domain name with the same resource path.
            // Otherwise, throw error immediately
            catch (HttpRequestException ex) when (ex.StatusCode != HttpStatusCode.NotFound)
            {
                throw;
            }
        }
        // If we reach here, meaning all the domain name gives the same error 404.
        throw new HttpRequestException(null, null, HttpStatusCode.NotFound);
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
                var result = await response.Content.ReadAsStringAsync(cancellationToken);
                var trimmedLen = result.AsSpan().Trim().Length;
                if (trimmedLen == result.Length) return result;
                return result.Trim();
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

    public Task<HttpResponseMessage> GetFileDownloadResponseAsync(in GameClientManifestData manifest, in PakEntry entry, bool downloadDiffOnly, CancellationToken cancellationToken = default)
    {
        if (entry.Equals(PakEntry.Empty)) throw new ArgumentNullException(nameof(entry));
        // ArgumentException.ThrowIfNullOrWhiteSpace(entry.hash, nameof(entry));

        return this.GetFileDownloadResponseFromFileHashAsync(in manifest, downloadDiffOnly ? entry.diff : entry.hash, cancellationToken);
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
