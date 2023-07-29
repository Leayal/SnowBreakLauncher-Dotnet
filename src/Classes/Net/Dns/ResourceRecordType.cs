using System;
// ReSharper disable InconsistentNaming

namespace Leayal.SnowBreakLauncher.Classes.Net.Dns
{
    public enum ResourceRecordType : Int16
    {
        A = 1,
        AAAA = 28,
        A6 = 38,
        AFSDB = 18,
        CNAME = 5,
        DNAME = 39,
        DNSKEY = 48,
        DS = 43,
        EUI48 = 108,
        EUI64 = 109,
        HINFO = 13,
        ISDN = 20,
        KEY = 25,
        LOC = 29,
        MX = 15,
        NAPTR = 35,
        NS = 2,
        NSEC = 47,
        NXT = 30,
        PTR = 12,
        RP = 17,
        RRSIG = 46,
        RT = 21,
        SIG = 24,
        SOA = 6,
        SPF = 99,
        SRV = 33,
        TXT = 16,
        URI = 256,
        WKS = 11,
        X25 = 19,
        ALL = 255
    }
}