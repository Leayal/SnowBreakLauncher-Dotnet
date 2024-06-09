using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpHDiffPatch.Core;
using System.Runtime.CompilerServices;
using SharpHDiffPatch.Core.Event;
using SharpHDiffPatch.Core.Patch;
using Avalonia.Controls.Shapes;

namespace Leayal.SnowBreakLauncher.Classes
{
    sealed class Hpatchz
    {
        private readonly HDiffPatch hDiffPatchFile;

        public long DiffSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.hDiffPatchFile.NewDataSize;
        }

        public string HDiffPath
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.hDiffPatchFile.diffPath;
        }

        public Hpatchz(string hDiffFile)
        {
            this.hDiffPatchFile = new HDiffPatch(hDiffFile);
        }

        public event EventHandler<PatchEvent> PatchEvent
        {
            add => this.hDiffPatchFile.Event.PatchEvent += value;
            remove => this.hDiffPatchFile.Event.PatchEvent -= value;
        }

        // Currently, the internal code of HDiffPatch doesn't have instances of patching, so progress tracking per instance isn't possible.
        // I'm not even sure if the HDiffPatchCore is even thread-safe to use.
        public async Task Patch(string originalFile, string outputFile, CancellationToken cancellation = default)
        {
            if (!File.Exists(originalFile)) throw new FileNotFoundException(null, originalFile);

            await Task.Factory.StartNew((object? obj) =>
            {
                if (obj == null) throw new InvalidOperationException(); // Can't be here anyway
                var (myself, originalFile, outputFile, cancellation) = ((Tuple<Hpatchz, string, string, CancellationToken>)obj);

                myself.hDiffPatchFile.Patch(originalFile, outputFile, cancellation);

            }, new Tuple<Hpatchz, string, string, CancellationToken> (this, originalFile, outputFile, cancellation), cancellation, TaskCreationOptions.LongRunning, TaskScheduler.Current ?? TaskScheduler.Default);
        }
    }
}
