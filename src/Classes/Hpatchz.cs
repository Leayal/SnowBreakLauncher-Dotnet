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
    /// <summary>Just a facade</summary>
    sealed class Hpatchz
    {
        /* Already Quiet by default
        static Hpatchz()
        {
            HDiffPatch.LogVerbosity = Verbosity.Quiet;
        }
        */
        // Have to self-fix it

        private readonly HeaderInfo headerInfo;
        private readonly DataReferenceInfo referenceInfo;
        public readonly string diffPath;
        private readonly bool isPatchDir;

        public long DiffSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this.headerInfo.newDataSize;
        }

        public Hpatchz(string hDiffFile)
        {
            this.diffPath = hDiffFile;
            
            using (var diffStream = new FileStream(hDiffFile, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))
            {
                this.isPatchDir = Header.TryParseHeaderInfo(diffStream, hDiffFile, out HeaderInfo headerInfo, out DataReferenceInfo referenceInfo);

                this.headerInfo = headerInfo;
                this.referenceInfo = referenceInfo;
            }
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

                var headerInfo = myself.headerInfo;

                IPatch patcher = (myself.isPatchDir && headerInfo.isInputDir && headerInfo.isOutputDir) ?
                new PatchDir(headerInfo, myself.referenceInfo, myself.diffPath, cancellation) 
                : new PatchSingle(myself.headerInfo, cancellation);

                patcher.Patch(originalFile, outputFile, true, false, true);

            }, new Tuple<Hpatchz, string, string,CancellationToken> (this, originalFile, outputFile, cancellation), cancellation, TaskCreationOptions.LongRunning, TaskScheduler.Current ?? TaskScheduler.Default);
        }
    }
}
