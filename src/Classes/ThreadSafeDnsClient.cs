using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net;
using TurnerSoftware.DinoDNS;

namespace Leayal.SnowBreakLauncher.Classes
{
    readonly struct ThreadSafeDnsClient : IDisposable
    {
        private readonly static ConcurrentBag<WeakReference<DnsClient>> bag;
        private readonly static NameServer[] _dnsservers;

        static ThreadSafeDnsClient()
        {
            bag = new ConcurrentBag<WeakReference<DnsClient>>();

            var dnsServers = new HashSet<NameServer>();
#if !NO_IPV6
            dnsServers.Add(NameServers.Cloudflare.IPv6.GetPrimary(ConnectionType.DoT));
            dnsServers.Add(NameServers.Google.IPv6.GetPrimary(ConnectionType.DoT));
            dnsServers.Add(NameServers.Cloudflare.IPv6.GetSecondary(ConnectionType.DoT));
            dnsServers.Add(NameServers.Google.IPv6.GetSecondary(ConnectionType.DoT));
#endif
            dnsServers.Add(NameServers.Cloudflare.IPv4.GetPrimary(ConnectionType.DoT));
            dnsServers.Add(NameServers.Google.IPv4.GetPrimary(ConnectionType.DoT));
            dnsServers.Add(NameServers.Cloudflare.IPv4.GetSecondary(ConnectionType.DoT));
            dnsServers.Add(NameServers.Google.IPv4.GetSecondary(ConnectionType.DoT));

            // Set fall-back
            foreach (var dnsServer in GetDnsAddresses())
            {
                if (dnsServer.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
#if !NO_IPV6
                    dnsServers.Add(new NameServer(dnsServer, ConnectionType.UdpWithTcpFallback));
#endif
                }
                else
                {
                    dnsServers.Add(new NameServer(dnsServer, ConnectionType.UdpWithTcpFallback));
                }
            }
            _dnsservers = new NameServer[dnsServers.Count];
            dnsServers.CopyTo(_dnsservers);
        }

        private static IEnumerable<IPAddress> GetDnsAddresses()
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    var ipProperties = networkInterface.GetIPProperties();
                    IPAddressCollection dnsAddresses = ipProperties.DnsAddresses;

                    foreach (IPAddress dnsAdress in dnsAddresses)
                    {
                        yield return dnsAdress;
                    }
                }
            }
        }

        public static DnsClient CreateNew() => new DnsClient(_dnsservers, DnsMessageOptions.Default);

        public static ThreadSafeDnsClient Rent()
        {
            // Loop the whole bag until we find one that working.
            while (bag.TryTake(out var client_reference))
            {
                if (client_reference.TryGetTarget(out var client))
                {
                    return new ThreadSafeDnsClient(client);
                }
            }

            // The whole bag is empty and still no valid vacant DnsClient found.
            return new ThreadSafeDnsClient(CreateNew());
        }

        public static void Return(in ThreadSafeDnsClient client)
        {
            bag.Add(new WeakReference<DnsClient>(client.Value));
        }

        public readonly DnsClient Value;

        public static readonly bool SupportIPv6 =
#if NO_IPV6
            false
#else
            true
#endif
            ;

        private ThreadSafeDnsClient(DnsClient client)
        {
            this.Value = client;
        }

        public void Dispose() => ThreadSafeDnsClient.Return(in this);
    }
}
