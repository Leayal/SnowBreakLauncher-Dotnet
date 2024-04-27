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
    sealed class HttpClientSecureDnsResolver : DelegatingHandler
    {
        private readonly DoHClient dnsClient;
        private int state_isEnabled;

        public bool IsEnabled
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (Interlocked.CompareExchange(ref this.state_isEnabled, -1, -1) > 0);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Interlocked.Exchange(ref this.state_isEnabled, value ? 1 : 0);
        }

        public HttpClientSecureDnsResolver(SocketsHttpHandler handler) : base(handler) 
        {
            this.dnsClient = new DoHClient()
            {
                RequestNoGeolocation = false,
                RequireDNSSEC = false
            };
            handler.ConnectCallback += this.HttpClient_HandleConnect;
            this.IsEnabled = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Socket CreateNewSocket()
        {
            var s = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            // s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            // s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 10);
            // s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 10);
            // s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 2);
            return s;
        }

        private static async Task<Socket> AttemptConnectAsync(DoHClient dnsClient, DnsEndPoint endpointOriginal, string hostname, ResourceRecordType resourceRecordType, CancellationToken cancelToken)
        {
            var dnsAnswers = await dnsClient.LookupAsync(hostname, resourceRecordType, cancelToken);
            if (dnsAnswers == null || dnsAnswers.Answers.Count == 0 || !dnsAnswers.HasAnyIP)
            {
                Socket? s = null;
                try
                {
                    s = CreateNewSocket();
                    await s.ConnectAsync(endpointOriginal, cancelToken);
                    return s;
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
                    var newEndpoint = new IPEndPoint(ipaddr, endpointOriginal.Port);
                    Socket? s = null;
                    try
                    {
                        s = CreateNewSocket();
                        await s.ConnectAsync(newEndpoint, cancelToken);
                        return s;
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

        private static async Task<Stream> DefaultHandle_HandleConnect(DnsEndPoint endPoint, CancellationToken cancellationToken)
        {
            Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            try
            {
                await socket.ConnectAsync(endPoint, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private async ValueTask<Stream> HttpClient_HandleConnect(SocketsHttpConnectionContext ctx, CancellationToken cancelToken)
        {
            if (!this.IsEnabled)
                return await DefaultHandle_HandleConnect(ctx.DnsEndPoint, cancelToken);

            var hostname = ctx.DnsEndPoint.Host;
            if (IPAddress.TryParse(hostname, out var remoteIp))
            {
                Socket? s = null;
                try
                {
                    s = CreateNewSocket();
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
#if NO_IPV6
                try
                {
                    var socket = await AttemptConnectAsync(this.dnsClient, ctx.DnsEndPoint, hostname, ResourceRecordType.A, cancelToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    Socket? s = null;
                    try
                    {
                        s = CreateNewSocket();
                        await s.ConnectAsync(ctx.DnsEndPoint, cancelToken);
                        return new NetworkStream(s, ownsSocket: true);
                    }
                    catch
                    {
                        s?.Dispose();
                        throw;
                    }
                }
#else
                try
                {
                    if (Socket.OSSupportsIPv6)
                    {
                        try
                        {
                            var socket = await AttemptConnectAsync(this.dnsClient, ctx.DnsEndPoint, hostname, ResourceRecordType.AAAA, cancelToken);
                            return new NetworkStream(socket, ownsSocket: true);
                        }
                        catch
                        {
                            var socket = await AttemptConnectAsync(this.dnsClient, ctx.DnsEndPoint, hostname, ResourceRecordType.A, cancelToken);
                            return new NetworkStream(socket, ownsSocket: true);
                        }
                    }
                    else
                    {
                        var socket = await AttemptConnectAsync(this.dnsClient, ctx.DnsEndPoint, hostname, ResourceRecordType.A, cancelToken);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                }
                catch
                {
                    Socket? s = null;
                    try
                    {
                        s = CreateNewSocket();
                        await s.ConnectAsync(ctx.DnsEndPoint, cancelToken);
                        return new NetworkStream(s, ownsSocket: true);
                    }
                    catch
                    {
                        s?.Dispose();
                        throw;
                    }
                }
#endif
                }
        }

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
