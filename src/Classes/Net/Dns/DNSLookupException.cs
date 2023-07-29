using System;
using System.Collections.Generic;
using System.Linq;

namespace Leayal.SnowBreakLauncher.Classes.Net.Dns
{
    public class DNSLookupException : Exception
    {
        public DNSLookupException[]? InnerExceptions;

        public DNSLookupException(string? message) : base(message)
        {
        }

        
        public DNSLookupException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        public DNSLookupException(string? message, IEnumerable<DNSLookupException> innerExceptions) : this(message)
        {
            InnerExceptions = innerExceptions.ToArray();
        }
    }
}
