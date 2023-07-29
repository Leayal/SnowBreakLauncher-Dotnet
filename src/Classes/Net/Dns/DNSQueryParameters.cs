using System;

namespace Leayal.SnowBreakLauncher.Classes.Net.Dns
{
    internal class DNSQueryParameters : IEquatable<DNSQueryParameters>
    {
        private readonly string _name;
        private readonly ResourceRecordType _recordType;

        public DNSQueryParameters(string name, ResourceRecordType recordType)
        {
            _recordType = recordType;
            _name = name;
        }

        public override int GetHashCode() => HashCode.Combine(_name != null ? _name.GetHashCode() : 0, _recordType);

        public bool Equals(DNSQueryParameters? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(_name, other._name) && _recordType == other._recordType;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == this.GetType() && Equals((DNSQueryParameters) obj);
        }
    }
}