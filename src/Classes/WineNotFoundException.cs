using System;
using Tmds.DBus.Protocol;

namespace Leayal.SnowBreakLauncher.Classes
{
    class WineNotFoundException : Exception
    {
        const string ErrorMsg = "Cannot find Wine installation. Please specify the Wine location if you have it on your machine via 'Wine Settings' dialog.";
        public WineNotFoundException(string? message) : base(message) { }
        public WineNotFoundException(string? message, Exception? innerException) : base(message, innerException) { }
        public WineNotFoundException(Exception? innerException) : this(ErrorMsg, innerException) { }
        public WineNotFoundException() : this(ErrorMsg) { }
    }
}
