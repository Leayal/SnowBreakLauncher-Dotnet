using System;
using System.IO;
using System.Buffers;
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
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferLen, 4096, nameof(buffer));
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
