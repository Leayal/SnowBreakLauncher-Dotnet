using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;
using System.Xml.Linq;

namespace Leayal.SnowBreakLauncher.Classes.Net.Dns
{
    public class DNSAnswer
    {
        public readonly string Name;
        public readonly ResourceRecordType RecordType;
        public readonly int TTL;
        public readonly string Data;

        public DNSAnswer(in JsonElement jsonAnswer)
        {
            this.Name = GetString(in jsonAnswer, "name");
            this.Data = GetString(in jsonAnswer, "data");
            if (jsonAnswer.TryGetProperty("type", out var prop_type))
            {
                this.RecordType = prop_type.ValueKind switch
                {
                    JsonValueKind.Number => (ResourceRecordType)prop_type.GetInt32(),
                    JsonValueKind.String => int.TryParse(prop_type.GetString(), out var _numAsStr) ? (ResourceRecordType)_numAsStr : ResourceRecordType.A,
                    _ => ResourceRecordType.A
                };
            }
            if (jsonAnswer.TryGetProperty("TTL", out var prop_TTL) && prop_TTL.ValueKind == JsonValueKind.Number)
            {
                this.TTL = prop_type.GetInt32();
            }
        }

        private static string GetString(in JsonElement element, string name)
        {
            if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? string.Empty;
            }
            return string.Empty;
        }
    }
}
