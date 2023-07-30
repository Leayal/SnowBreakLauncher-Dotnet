using Leayal.SnowBreakLauncher.Classes.Net.Dns;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher.Classes
{
    class HttpClientSecureDnsResolver : DelegatingHandler
    {
        private readonly DoHClient dnsClient;

        public HttpClientSecureDnsResolver(SocketsHttpHandler handler) : base(handler) 
        {
            this.dnsClient = new DoHClient()
            {
                RequestNoGeolocation = false,
                RequireDNSSEC = false
            };
            handler.ConnectCallback += this.HttpClient_HandleConnect;
        }

        private async ValueTask<Stream> HttpClient_HandleConnect(SocketsHttpConnectionContext ctx, CancellationToken cancelToken)
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static Socket CreateNew()
            {
                var s = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                // s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                // s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 10);
                // s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10);
                // s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 2);
                return s;
            }

            var hostname = ctx.DnsEndPoint.Host;
            if (IPAddress.TryParse(hostname, out var remoteIp))
            {
                Socket? s = null;
                try
                {
                    s = CreateNew();
                    await s.ConnectAsync(new IPEndPoint(remoteIp, ctx.DnsEndPoint.Port), cancelToken);
                    return new NetworkStream(s, ownsSocket: true);
                }
                catch
                {
                    s?.Dispose();
                    throw;
                }
            }
            else
            {
                var dnsAnswers = await this.dnsClient.LookupAsync(hostname, ResourceRecordType.A, cancelToken);
                if (dnsAnswers == null || dnsAnswers.Answers.Count == 0 || !dnsAnswers.HasAnyIP)
                {
                    Socket? s = null;
                    try
                    {
                        s = CreateNew();
                        await s.ConnectAsync(ctx.DnsEndPoint, cancelToken);
                        return new NetworkStream(s, ownsSocket: true);
                    }
                    catch
                    {
                        s?.Dispose();
                        throw;
                    }
                }
                else
                {
                    Exception? last_ex = null;
                    foreach (var ipaddr in dnsAnswers.GetAddress(hostname))
                    {
                        var newEndpoint = new IPEndPoint(ipaddr, ctx.DnsEndPoint.Port);
                        Socket? s = null;
                        try
                        {
                            s = CreateNew();
                            await s.ConnectAsync(newEndpoint, cancelToken);
                            return new NetworkStream(s, ownsSocket: true);
                        }
                        catch (Exception ex)
                        {
                            s?.Dispose();
                            last_ex = ex;
                        }
                    }
                    if (last_ex == null)
                    {
                        throw new DnsResolveFailureException();
                    }
                    else
                    {
                        throw last_ex;
                    }
                }
            }
        }

        /*
        private static HttpRequestMessage Clone(HttpRequestMessage req, Uri? uri = null)
        {
            var clone = new HttpRequestMessage(req.Method, uri ?? req.RequestUri);

            clone.Content = req.Content;
            clone.Version = req.Version;

            if (req.Headers.Host == null && req.RequestUri is Uri original)
            {
                clone.Headers.Host = original.Host;
            }

            foreach (var prop in req.Options)
            {
                clone.Options.TryAdd(prop.Key, prop.Value);
            }

            foreach (var header in req.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri;
            if (uri == null) return await base.SendAsync(request, cancellationToken);
            else if (IPAddress.TryParse(uri.Host, out _)) return await base.SendAsync(request, cancellationToken);
            else
            {
                var hostname = uri.DnsSafeHost;
                var records = await DnsCache.GetAsync(hostname, cancellationToken);
                if (records.Count == 0)
                {
                    return await base.SendAsync(request, cancellationToken);
                }
                else
                {
                    var uriBuilder = new UriBuilder(uri);
                    foreach (var ip in records)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        uriBuilder.Host = ip.ToString();
                        try
                        {
                            return await base.SendAsync(Clone(request, uriBuilder.Uri), cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                    }
                    return await base.SendAsync(request, cancellationToken);
                }
            }
        }
        */

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                this.InnerHandler?.Dispose();
                this.dnsClient.Dispose();
            }
        }
    }
}
