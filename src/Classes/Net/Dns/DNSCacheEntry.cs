using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml.Linq;

namespace Leayal.SnowBreakLauncher.Classes.Net.Dns
{
    public class DNSCacheEntry
    {
        internal readonly DateTime ExpireTime;
        public readonly FrozenSet<DNSAnswer> Answers;

        public DNSCacheEntry(FrozenSet<DNSAnswer> answers)
        {
            if (answers.Count != 0)
                this.ExpireTime = DateTime.Now + new TimeSpan(0, 0, answers.Min(a => a.TTL));
            else
                this.ExpireTime = DateTime.Now;
            
            this.Answers = answers;
        }

        public bool HasAnyIP
        {
            get
            {
                foreach (var answer in this.Answers)
                {
                    if (answer.RecordType == ResourceRecordType.A || answer.RecordType == ResourceRecordType.AAAA)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public IEnumerable<IPAddress> GetAddress(string name)
        {
            foreach (var answer in this.GetByName(name))
            {
                yield return IPAddress.Parse(answer.Data);
            }
        }

        private IEnumerable<DNSAnswer> GetByName(string name)
        {
            foreach (var answer in this.Get(name))
            {
                if (answer.RecordType == ResourceRecordType.A || answer.RecordType == ResourceRecordType.AAAA)
                {
                    yield return answer;
                }
                else
                {
                    foreach (var deeper in this.GetByName(answer.Data))
                    {
                        yield return deeper;
                    }
                }
            }
        }

        private IEnumerable<DNSAnswer> Get(string name)
        {
            foreach (var answer in this.Answers)
            {
                var duh = answer.Name.AsSpan();
                var duh2 = name.AsSpan();
                if (MemoryExtensions.Equals(duh, duh2, StringComparison.OrdinalIgnoreCase)
                    // In case the name has trailing dot.
                    || (duh[duh.Length - 1] == '.' && MemoryExtensions.Equals(duh.Slice(0, duh.Length - 1), duh2, StringComparison.OrdinalIgnoreCase)))
                    yield return answer;
            }
        }
    }
}