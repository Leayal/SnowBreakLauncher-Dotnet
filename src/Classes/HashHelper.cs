// .NET 8.0.0-rc2 is another dumb design?
// So, for some reasons, "NET8_0_OR_GREATER" constant doesn't exist (older .NET8 SDK versions have it)
// Because they considered, years later, everyone will have time and be happy to read god-know-when source code and 'find and replace' constant "NET8_0" to "NET8_0_OR_GREATER" without having to debug.
#if NET8_0
#define NET8_0_OR_GREATER
#endif

using System;
using System.IO;
using System.Security.Cryptography;

namespace Leayal.SnowBreakLauncher.Classes
{
    static class HashHelper
    {
        /// <summary></summary>
        /// <param name="dataStream"></param>
        /// <param name="buffer">A buffer with size between 4096 and <seealso cref="ushort.MaxValue"/>.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static void FillDataFromStream(this IncrementalHash hashEngine, Stream dataStream, Span<byte> buffer)
        {
            ArgumentNullException.ThrowIfNull(dataStream);
            if (!dataStream.CanRead) throw new ArgumentException(null, nameof(dataStream));
            int bufferLen = buffer.Length;
#if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferLen, 4096, nameof(buffer));
#else
            if (bufferLen < 4096) throw new ArgumentOutOfRangeException(nameof(buffer));
#endif
            // ArgumentOutOfRangeException.ThrowIfGreaterThan(bufferLen, ushort.MaxValue, nameof(buffer));
            if (bufferLen > ushort.MaxValue) buffer = buffer.Slice(0, ushort.MaxValue);
            int read = dataStream.Read(buffer);
            while (read > 0)
            {
                hashEngine.AppendData(buffer.Slice(0, read));
                read = dataStream.Read(buffer);
            }
        }
    }
}
