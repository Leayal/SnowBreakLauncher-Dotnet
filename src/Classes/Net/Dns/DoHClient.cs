using System;
using System.Collections.Concurrent;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Collections.Generic;
using System.Net.Http;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Leayal.SnowBreakLauncher.Classes.Net.Dns
{
    public class DoHClient : IDisposable
    {
        public const string CloudflareURI = "https://cloudflare-dns.com/dns-query";
        public const string GoogleURI = "https://dns.google/resolve";
        public bool UseRandomPadding = true;
        public bool RequireDNSSEC = true;
        public bool RequestNoGeolocation = true;
        private const string JsonContentType = "application/dns-json";
        private readonly HttpClient _client;
        private readonly ConcurrentDictionary<DNSQueryParameters, DNSCacheEntry> _answersCache = new ConcurrentDictionary<DNSQueryParameters, DNSCacheEntry>();

        private static readonly IReadOnlyDictionary<int, string> DNSCodes = new Dictionary<int, string>
        {
            { 1, "Format Error"},
            { 2, "Server Failure"},
            { 3, "Non-Existent Domain" },
            { 4, "Not Implemented" },
            { 5, "Query Refused" },
            { 6, "Name Exists when it should not" },
            { 7, "RR Set Exists when it should not" },
            { 8, "RR Set that should exist does not" },
            { 9, "Server Not Authoritative for zone" },
            { 10, "Name not contained in zone" },
            { 16, "Bad OPT Version / TSIG Signature Failure" },
            { 17, "Key not recognized" },
            { 18, "Signature out of time window" },
            { 19, "Bad TKEY Mode" },
            { 20, "Duplicate key name" },
            { 21, "Algorithm not supported" },
            { 22, "Bad Truncation" },
            { 23, "Bad/missing Server Cookie" }
        }
        // As of .NET8 preview 7, Frozen Dictionary implies OptimizedReading, no longer OptimizedCreating.
#if NET8_0_OR_GREATER
        .ToFrozenDictionary()
#endif
        ;

        private string[] _endpointList = 
        {
            CloudflareURI, GoogleURI
        };

        // ReSharper disable once ParameterTypeCanBeEnumerable.Global
        public void SetEndpoints(string[] serverList)
        {
            foreach (var s in serverList)
            {
                if (!s.StartsWith("https://"))
                    throw new ArgumentException("Server URI not https", nameof(serverList));
            }
            _endpointList = serverList;
        }

        public DoHClient()
        {
            // We use a separate HTTP Client and Socket Pool for it.
            // Using same HTTP Client in SnowBreakHttpClient.Instance will leads to endless loop.
            _client = new HttpClient(new SocketsHttpHandler()
            {
                UseProxy = false,
                AutomaticDecompression = System.Net.DecompressionMethods.All
            }, true);
        }

        public void ClearCache()
        {
            _answersCache.Clear();
        }

        public async Task<DNSCacheEntry> LookupAsync(string name, ResourceRecordType recordType, CancellationToken cancellationToken)
        {
            var queryParams = new DNSQueryParameters(name, recordType);
            if (_answersCache.TryGetValue(queryParams, out var hit))
            {
                if (hit.ExpireTime <= DateTime.Now)
                    _answersCache.TryRemove(queryParams, out _);
                else
                    return hit;
            }

            var storedExceptions = new ConcurrentBag<DNSLookupException>();
            foreach (string endpoint in _endpointList)
            {
                try
                {
                    var answers = await SingleLookup(name, recordType, endpoint, cancellationToken);
                    if (answers != null && answers.Count != 0)
                    {
                        var cached = new DNSCacheEntry(answers);
                        return _answersCache.GetOrAdd(queryParams, cached);
                    }
                }
                catch (DNSLookupException ex)
                {
                    storedExceptions.Add(ex);
                }
            }
            throw new DNSLookupException("Unable to perform DNS lookup due to lookup errors", storedExceptions);
        }

        private async Task<IReadOnlyCollection<DNSAnswer>?> SingleLookup(string name, ResourceRecordType recordType, string serverURI, CancellationToken cancellationToken)
        {
            string uri = GenerateQuery(name, serverURI, recordType);

            using (var response = await _client.GetAsync(uri, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    throw new DNSLookupException($"Error contacting DNS server (HTTP {response.StatusCode} {response.ReasonPhrase})");

                string content = await response.Content.ReadAsStringAsync();
                return HandleJSONResponse(content, RequireDNSSEC);
            }
        }

        private static string? GetString(in JsonElement element, string name)
        {
            if (name == null) return null;
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString();
            }
            return null;
        }

        private static int? GetInt(in JsonElement element, string name)
        {
            if (name == null) return null;
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt32();
            }
            return null;
        }

        private static bool? GetBool(in JsonElement element, string name)
        {
            if (name == null) return null;
            if (element.TryGetProperty(name, out var prop))
            {
                return prop.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null
                };
            }
            return null;
        }

        private static IReadOnlyCollection<DNSAnswer>? HandleJSONResponse(string content, bool requireVerified)
        {
            JsonDocument json;
            try
            {
                json = JsonDocument.Parse(content);
            }
            catch (JsonException ex)
            {
                throw new DNSLookupException("Unable to parse JSON when retrieving DNS entry", ex);
            }

            var element = json.RootElement;
            string? comment = GetString(in element, "Comment");

            int status = GetInt(in element, "Status") ?? 0;
            if (status != 0)
                HandleDNSError(status, comment);

            bool? truncatedBit = GetBool(in element, "TC");
            bool? recursiveDesiredBit = GetBool(in element, "RD");
            bool? recursionAvailableBit = GetBool(in element, "RA");
            bool? verifiedAnswers = GetBool(in element, "AD");

            if (requireVerified && (!verifiedAnswers.HasValue || !verifiedAnswers.Value))
                throw new DNSLookupException("DNS lookup could not be verified as using DNSSEC but DNSSEC was required");

            //var questions = (JArray) json["Question"];

            if (element.TryGetProperty("Answer", out var prop_Answer) && prop_Answer.ValueKind == JsonValueKind.Array)
            {
                var list = new List<DNSAnswer>(prop_Answer.GetArrayLength());
                foreach (var item in prop_Answer.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        list.Add(new DNSAnswer(in item));
                    }
                }
#if NET8_0_OR_GREATER
                return list.ToFrozenSet();
#else
                return list;
#endif
            }
            else
            {
                return null;
            }
        }

        private string GenerateQuery(string name, string serverURI, ResourceRecordType queryType, string? customContentType = null)
        {
            var sb = new StringBuilder(serverURI, name.Length + serverURI.Length + (UseRandomPadding ? 256 : 128));

            sb.Append("?name=").Append(Uri.EscapeDataString(name));
            sb.Append("&type=").Append((int)queryType);
            sb.Append("&ct=").Append(Uri.EscapeDataString(string.IsNullOrWhiteSpace(customContentType) ? JsonContentType : customContentType)); // CT impies Content-Type
            sb.Append("&cd=").Append(RequireDNSSEC ? "false" : "true"); // CD implies "Check disabled"
            sb.Append("&do=0"); // DO implies "DNSSEC OK", however, it actually meant to including verbose DNSSEC info in the response or not. It does not force DNSSEC to work.

            if (RequestNoGeolocation)
                sb.Append("&edns_client_subnet=").Append(Uri.EscapeDataString("0.0.0.0/0"));

            const int padtoLength = 250;
            if ((sb.Length - 16) < padtoLength && UseRandomPadding)
            {
                var padLen = padtoLength - sb.Length - 16;
                var padStr = string.Create<object?>(padLen, null, (buffer, obj) =>
                {
                    GeneratePadding(in buffer);
                });
                sb.Append("&random_padding=").Append(Uri.EscapeDataString(padStr));
            }

            return sb.ToString();
        }

        private static void HandleDNSError(int statusCode, string? comment)
        {
            string commentText = comment != null ? $"{Environment.NewLine}Server Comment: ({comment})" : string.Empty;
            if (DNSCodes.TryGetValue(statusCode, out var dnsMsg))
                throw new DNSLookupException($"Received DNS RCode {statusCode} when performing lookup: {dnsMsg}{commentText}");

            throw new DNSLookupException($"Received DNS RCode {statusCode} when performing lookup{commentText}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GeneratePadding(in Span<char> bufferToWrite)
        {
            var sample = "abcddefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVXYZ012456789-._~".AsSpan();
#if NET8_0_OR_GREATER
            Random.Shared.GetItems(sample, bufferToWrite);
#else
            GetItems(Random.Shared, sample, bufferToWrite);
#endif
        }

#if !NET8_0_OR_GREATER
        /// <remarks>https://github.com/dotnet/runtime/blob/29a2ad14eeb25304dd0938eb60939fa83786408e/src/libraries/System.Private.CoreLib/src/System/Random.cs#L193</remarks>
        private static void GetItems<T>(Random random, ReadOnlySpan<T> choices, Span<T> destination)
        {
            if (choices.IsEmpty)
            {
                throw new ArgumentException(null, nameof(choices));
            }

            // The most expensive part of this operation is the call to get random data. We can
            // do so potentially many fewer times if:
            // - the number of choices is <= 256. This let's us get a single byte per choice.
            // - the number of choices is a power of two. This let's us use a byte and simply mask off
            //   unnecessary bits cheaply rather than needing to use rejection sampling.
            // In such a case, we can grab a bunch of random bytes in one call.
            if (BitOperations.IsPow2(choices.Length) && choices.Length <= 256)
            {
                Span<byte> randomBytes = stackalloc byte[512]; // arbitrary size, a balance between stack consumed and number of random calls required
                while (!destination.IsEmpty)
                {
                    if (destination.Length < randomBytes.Length)
                    {
                        randomBytes = randomBytes.Slice(0, destination.Length);
                    }

                    random.NextBytes(randomBytes);

                    int mask = choices.Length - 1;
                    for (int i = 0; i < randomBytes.Length; i++)
                    {
                        destination[i] = choices[randomBytes[i] & mask];
                    }

                    destination = destination.Slice(randomBytes.Length);
                }

                return;
            }

            // Simple fallback: get each item individually, generating a new random Int32 for each
            // item. This is slower than the above, but it works for all types and sizes of choices.
            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] = choices[random.Next(choices.Length)];
            }
        }
#endif

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}
