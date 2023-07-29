using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net;
using Leayal.SnowBreakLauncher.Classes.Net.Dns;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Leayal.SnowBreakLauncher.Classes
{
    static class DnsCache
    {
        private static readonly ConcurrentDictionary<string, DnsRecords> cachedDnsResolutions;
        private static readonly DoHClient dnsClient;
        // private static readonly SemaphoreSlim asynclocker;

        static DnsCache()
        {
            cachedDnsResolutions = new ConcurrentDictionary<string, DnsRecords>(StringComparer.OrdinalIgnoreCase);
            dnsClient = new DoHClient()
            {
                // A trade between anonymous or the performance.
                // Some server will returns the IP of the closest servers to your location for better network handling.
                RequestNoGeolocation = false
            };
            // asynclocker = new SemaphoreSlim(1, 1);
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        private static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
            dnsClient.Dispose();
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

        public static Task<IReadOnlyList<IPAddress>> GetAsync(string hostname, CancellationToken cancellationToken)
        {
            var recordCache = new DnsRecords();
            var existingCache = cachedDnsResolutions.GetOrAdd(hostname, recordCache);
            if (existingCache != recordCache)
            {
                recordCache = null;
            }
            return existingCache.Get(hostname, cancellationToken);
        }

        class DnsRecords
        {
            private readonly WeakReference<Task<IReadOnlyList<IPAddress>>?> cachedRecords;
            private readonly object lockobj;
            private KeepAlive? keepItAlive;

            public DnsRecords()
            {
                this.cachedRecords = new WeakReference<Task<IReadOnlyList<IPAddress>>?>(null);
                this.lockobj = new object();
            }

            public Task<IReadOnlyList<IPAddress>> Get(string hostname, CancellationToken cancellationToken)
            {
                Task<IReadOnlyList<IPAddress>>? result;
                lock (this.lockobj)
                {
                    if (!cachedRecords.TryGetTarget(out result) || result.IsFaulted)
                    {
                        result = this.Resolve(hostname, cancellationToken);
                        this.keepItAlive = new KeepAlive(result, TimeSpan.FromSeconds(30)); // This should keep it alive "at least" 30 seconds, not at most.
                        cachedRecords.SetTarget(result);
                    }
                }
                return result;
            }

            ~DnsRecords()
            {
                this.keepItAlive = null;
            }

            class KeepAlive
            {
                public static KeepAlive On(Task<IReadOnlyList<IPAddress>> target, in TimeSpan timeToLive) => new KeepAlive(target, timeToLive);

                private Task<IReadOnlyList<IPAddress>>? target;
                public KeepAlive(Task<IReadOnlyList<IPAddress>> target, TimeSpan timeToLive) 
                {
                    this.target = target;
                    Task.Delay(timeToLive).ContinueWith(this.Yeet);
                }

                private void Yeet(Task t)
                {
                    t?.Dispose();
                    target = null;
                }

                ~KeepAlive()
                {
                    target = null;
                }
            }

            private async Task<IReadOnlyList<IPAddress>> Resolve(string hostname, CancellationToken cancellationToken)
            {
                /*
                var answers = await DnsAsync.Resolve(hostname, cancellationToken);
                var result = new List<IPAddress>(answers.Answers.Count);
                foreach (var answer in answers.Answers.WithARecords())
                {
                    result.Add(answer.ToIPAddress());
                }
                return result;
                */

                var answers = dnsClient.LookupAsync(hostname, ResourceRecordType.A, cancellationToken);
                var result = new List<IPAddress>(answers.Answers.Count);
                foreach (var answer in answers.Answers.WithARecords())
                {
                    result.Add(answer.ToIPAddress());
                }
                return result;
            }
        }

        public static void Clear() => cachedDnsResolutions.Clear();

        /*
        readonly struct DnsAsync
        {
            public static Task<DnsMessage> Resolve(string hostname, CancellationToken cancellationToken)
            {
                var packed = new DnsAsync(hostname);
                return packed.MakeTask(cancellationToken);
            }

            private readonly string hostname;

            public DnsAsync(string hostname)
            {
                this.hostname = hostname;
            }

            // This was to avoid same-thread
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private readonly Task<DnsMessage> MakeTask(CancellationToken cancellationToken) => Task.Factory.StartNew(ResolveDns, cancellationToken, cancellationToken).Unwrap();

            private readonly async Task<DnsMessage> ResolveDns(object? obj)
            {
                if (obj == null) throw new InvalidOperationException();
                var cancellationToken = Unsafe.Unbox<CancellationToken>(obj);
                await asynclocker.WaitAsync(cancellationToken);
                try
                {
                    return await dnsClient.QueryAsync(this.hostname, DnsQueryType.A, DnsClass.IN, cancellationToken);
                }
                finally
                {
                    asynclocker.Release();
                }
            }
        }
        */

        public static readonly bool SupportIPv6 =
#if NO_IPV6
            false
#else
            true
#endif
            ;
    }
}
