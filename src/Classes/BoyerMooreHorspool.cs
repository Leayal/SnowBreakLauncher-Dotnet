using System;
using System.Buffers;
using System.IO;

namespace Leayal.SnowBreakLauncher.Classes
{
    sealed class BoyerMooreHorspool
    {
        private readonly byte[] pattern;
        private readonly int[] badCharacters;

        public BoyerMooreHorspool(byte[] pattern)
        {
            this.pattern = pattern;
            this.badCharacters = MakeBadCharArray(pattern);
        }

        /// <summary>
        /// Finds the first occurrence in a stream
        /// </summary>
        /// <param name="s">The input stream</param>
        /// <returns>The index of the first occurrence, or -1 if the pattern has not been found</returns>
        public long IndexOf(Stream s)
        {
            var patternLength = this.pattern.Length;
            // We now repeatedly read the stream into a buffer and apply the Boyer-Moore-Horspool algorithm on the buffer until we get a match
            var arr = ArrayPool<byte>.Shared.Rent(Math.Max(2 * patternLength, 4096));
            try
            {
                Span<byte> buffer = arr;
                long offset = 0; // keep track of the offset in the input stream
                while (true)
                {
                    int dataLength;
                    if (offset == 0)
                    {
                        // the first time we fill the whole buffer
                        dataLength = s.Read(buffer);
                    }
                    else
                    {
                        // Later, copy the last patternLength bytes from the previous buffer to the start and fill up from the stream
                        // This is important so we can also find matches which are partly in the old buffer
                        buffer.Slice(buffer.Length - patternLength, patternLength).CopyTo(buffer.Slice(0, patternLength));
                        dataLength = s.Read(buffer.Slice(patternLength, buffer.Length - patternLength)) + patternLength;
                    }

                    var index = IndexOf(buffer, dataLength, pattern, badCharacters);
                    if (index >= 0)
                        return offset + index; // found!
                    if (dataLength < buffer.Length)
                        break;
                    offset += dataLength - patternLength;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(arr);
            }

            return -1;
        }

        /// <summary>Finds the first occurrence in data</summary>
        /// <param name="buffer">The input data</param>
        /// <returns>The index of the first occurrence, or -1 if the pattern has not been found</returns>
        public long IndexOf(ReadOnlySpan<byte> buffer)
            => IndexOf(buffer, buffer.Length, pattern, badCharacters);

        /// <summary>Finds the first occurrence in data</summary>
        /// <param name="buffer">The input data</param>
        /// <param name="index">The starting index to start scanning</param>
        /// <param name="count">The length counting from the <paramref name="index"/> to stop scanning</param>
        /// <returns>The index of the first occurrence, or -1 if the pattern has not been found</returns>
        public long IndexOf(byte[] buffer, int index, int count)
            => IndexOf(new ReadOnlySpan<byte>(buffer, index, count));

        // --- Boyer-Moore-Horspool algorithm ---
        // (Slightly modified code from
        // https://stackoverflow.com/questions/16252518/boyer-moore-horspool-algorithm-for-all-matches-find-byte-array-inside-byte-arra)
        // Prepare the bad character array is done once in a separate step:
        private static int[] MakeBadCharArray(ReadOnlySpan<byte> pattern)
        {
            var badCharacters = new int[256];

            for (long i = 0; i < 256; ++i)
                badCharacters[i] = pattern.Length;

            for (var i = 0; i < pattern.Length - 1; ++i)
                badCharacters[pattern[i]] = pattern.Length - 1 - i;

            return badCharacters;
        }

        // Core of the BMH algorithm
        private static int IndexOf(ReadOnlySpan<byte> value, int valueLength, ReadOnlySpan<byte> pattern, ReadOnlySpan<int> badCharacters)
        {
            int index = 0;

            while (index <= valueLength - pattern.Length)
            {
                for (var i = pattern.Length - 1; value[index + i] == pattern[i]; --i)
                {
                    if (i == 0)
                        return index;
                }

                index += badCharacters[value[index + pattern.Length - 1]];
            }

            return -1;
        }
    }
}
