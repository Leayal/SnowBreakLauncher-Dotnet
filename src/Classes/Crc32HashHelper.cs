using DamienG.Security.Cryptography;
using System;
using System.Buffers;
using System.IO;

namespace Leayal.SnowBreakLauncher.Classes
{
    static class Crc32HashHelper
    {
        private static readonly uint[] crcTable = Crc32.InitializeTable(Crc32.DefaultPolynomial);

        /// <summary></summary>
        /// <param name="pathToFile"></param>
        /// <param name="bufferSize">A number between 4096 and <seealso cref="ushort.MaxValue"/>.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static uint ComputeFromFile(string pathToFile, int bufferSize = 4096)
        {
            using (var fs = new FileStream(pathToFile, FileMode.Open, FileAccess.Read, FileShare.Read, 0 /* ComputeFromStream already have buffering */))
                return ComputeFromStream(fs, bufferSize);
        }

        /// <summary></summary>
        /// <param name="dataStream"></param>
        /// <param name="bufferSize">A number between 4096 and <seealso cref="ushort.MaxValue"/>.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static uint ComputeFromStream(Stream dataStream, int bufferSize = 4096)
        {
            ArgumentNullException.ThrowIfNull(dataStream);
            if (!dataStream.CanRead) throw new ArgumentException(null, nameof(dataStream));

            var currentHash = Crc32.DefaultSeed;
            var borrowed = ArrayPool<byte>.Shared.Rent(Math.Clamp(bufferSize, 4096, ushort.MaxValue));
            try
            {
                int read = dataStream.Read(borrowed, 0, borrowed.Length);
                while (read > 0)
                {
                    currentHash = Crc32.CalculateHash(crcTable, currentHash, borrowed, 0, read);
                    read = dataStream.Read(borrowed, 0, borrowed.Length);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(borrowed);
            }
            return ~currentHash;
        }
    }
}
