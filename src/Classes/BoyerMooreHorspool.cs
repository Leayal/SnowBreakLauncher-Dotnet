﻿using System;
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
            // We now repeatedly read the stream into a buffer and apply the Boyer-Moore-Horspool algorithm on the buffer until we get a match
            var buffer = ArrayPool<byte>.Shared.Rent(Math.Max(2 * pattern.Length, 4096));
            try
            {
                long offset = 0; // keep track of the offset in the input stream
                while (true)
                {
                    int dataLength;
                    if (offset == 0)
                    {
                        // the first time we fill the whole buffer
                        dataLength = s.Read(buffer, 0, buffer.Length);
                    }
                    else
                    {
                        // Later, copy the last pattern.Length bytes from the previous buffer to the start and fill up from the stream
                        // This is important so we can also find matches which are partly in the old buffer
                        Array.Copy(buffer, buffer.Length - pattern.Length, buffer, 0, pattern.Length);
                        dataLength = s.Read(buffer, pattern.Length, buffer.Length - pattern.Length) + pattern.Length;
                    }

                    var index = IndexOf(buffer, dataLength, pattern, badCharacters);
                    if (index >= 0)
                        return offset + index; // found!
                    if (dataLength < buffer.Length)
                        break;
                    offset += dataLength - pattern.Length;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return -1;
        }

        // --- Boyer-Moore-Horspool algorithm ---
        // (Slightly modified code from
        // https://stackoverflow.com/questions/16252518/boyer-moore-horspool-algorithm-for-all-matches-find-byte-array-inside-byte-arra)
        // Prepare the bad character array is done once in a separate step:
        private static int[] MakeBadCharArray(byte[] pattern)
        {
            var badCharacters = new int[256];

            for (long i = 0; i < 256; ++i)
                badCharacters[i] = pattern.Length;

            for (var i = 0; i < pattern.Length - 1; ++i)
                badCharacters[pattern[i]] = pattern.Length - 1 - i;

            return badCharacters;
        }

        // Core of the BMH algorithm
        private static int IndexOf(byte[] value, int valueLength, byte[] pattern, int[] badCharacters)
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
