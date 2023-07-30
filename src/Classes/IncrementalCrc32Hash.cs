using DamienG.Security.Cryptography;
using System;

namespace Leayal.SnowBreakLauncher.Classes
{
    sealed class IncrementalCrc32Hash
    {
        private uint _hashing;

        public IncrementalCrc32Hash() => this.Reset();

        public void Reset() => this._hashing = Crc32.DefaultSeed;

        public void Append(ReadOnlySpan<byte> data) => this._hashing = Crc32.CalculateHash(this._hashing, data);

        public uint HashRaw => ~this._hashing;
        public int Hash => unchecked((int)(~this._hashing));
    }
}
