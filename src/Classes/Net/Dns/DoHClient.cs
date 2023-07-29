using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static System.String;

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

        private static readonly FrozenDictionary<int, string> DNSCodes = new Dictionary<int, string>
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
        }.ToFrozenDictionary(null, true);
        private string[] _endpointList = 
        {
            CloudflareURI, GoogleURI
        };

        // ReSharper disable once ParameterTypeCanBeEnumerable.Global
        public void SetEndpoints(string[] serverList)
        {
            if (serverList.Any(s => !s.StartsWith("https://")))
                throw new ArgumentException("Server URI not https", nameof(serverList));
            _endpointList = serverList.ToArray();
        }

        public DoHClient()
        {
            _client = new HttpClient();
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

        private async Task<FrozenSet<DNSAnswer>?> SingleLookup(string name, ResourceRecordType recordType, string serverURI, CancellationToken cancellationToken)
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

        private static FrozenSet<DNSAnswer>? HandleJSONResponse(string content, bool requireVerified)
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
                return list.ToFrozenSet();
            }
            else
            {
                return null;
            }
        }

        private string GenerateQuery(string name, string serverURI, ResourceRecordType queryType, string? customContentType = null)
        {
            var fields = new Dictionary<string, string>()
            {
                {"name", Uri.EscapeDataString(name)},
                {"type", ((int)queryType).ToString()},
                {"ct", Uri.EscapeDataString(customContentType ?? JsonContentType)},
                {"cd", RequireDNSSEC ? "false" : "true"},
            };

            if (RequestNoGeolocation)
                fields.Add("edns_client_subnet", Uri.EscapeDataString("0.0.0.0/0"));

            const int padtoLength = 250;

            string uri = $"{serverURI}?{Join("&", fields.Select(f => f.Key + "=" + f.Value))}";
            if (uri.Length - 16 < padtoLength && UseRandomPadding)
                uri += $"&random_padding={Uri.EscapeDataString(GeneratePadding(padtoLength - uri.Length - 16))}";
            return uri;
        }

        private static void HandleDNSError(int statusCode, string? comment)
        {
            string commentText = comment != null ? $"{Environment.NewLine}Server Comment: ({comment})" : "";
            if (DNSCodes.TryGetValue(statusCode, out var dnsMsg))
                throw new DNSLookupException($"Received DNS RCode {statusCode} when performing lookup: {dnsMsg}{commentText}");

            throw new DNSLookupException($"Received DNS RCode {statusCode} when performing lookup{commentText}");
        }

        private string GeneratePadding(int paddingLength)
        {
            const string paddingChars = "abcddefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVXYZ012456789-._~";
            return new string(Random.Shared.GetItems(paddingChars.AsSpan(), paddingLength));
        }

        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}
