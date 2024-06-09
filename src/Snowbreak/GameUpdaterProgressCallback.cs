using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Leayal.SnowBreakLauncher.Snowbreak
{
    sealed class GameUpdaterProgressCallback
    {
        public readonly GameUpdaterDownloadProgressValue FileCheckProgress;
        public readonly GameUpdaterDownloadProgressValue TotalDownloadProgress;
        public readonly GameUpdaterDownloadProgressValue Download1Progress;
        public readonly GameUpdaterDownloadProgressValue Download2Progress;

        public readonly Action? OnDisplayableProgressBar;

        public GameUpdaterProgressCallback(Action? onDisplayableProgressBar = null)
        {
            this.OnDisplayableProgressBar = onDisplayableProgressBar;
            this.TotalDownloadProgress = new GameUpdaterDownloadProgressValue(this);
            this.FileCheckProgress = new GameUpdaterDownloadProgressValue(this);
            this.Download1Progress = new GameUpdaterDownloadProgressValue(this);
            this.Download2Progress = new GameUpdaterDownloadProgressValue(this);
        }
    }

    sealed class GameUpdaterDownloadProgressValue
    {
        private readonly GameUpdaterProgressCallback AttachedParent;
        private long _CurrentProgress, _TotalProgress;
        private string _Filename = string.Empty;
        public bool HasChange { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; private set; }

        public bool IsDone { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; set; }

        public bool IsInHPatchZ { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; private set; }

        public string Filename
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Interlocked.CompareExchange(ref this._Filename, string.Empty, string.Empty);
            internal set
            {
                var oldVal = Interlocked.Exchange(ref this._Filename, value);
                this.HasChange = !string.Equals(oldVal, value, StringComparison.Ordinal);
            }
        }
        public long CurrentProgress
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Interlocked.Read(ref this._CurrentProgress);
            internal set
            {
                var oldVal = Interlocked.Exchange(ref this._CurrentProgress, value);
                this.HasChange = (oldVal != value);
            }
        }
        public long TotalProgress
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Interlocked.Read(ref this._TotalProgress);
            internal set
            {
                var oldVal = Interlocked.Exchange(ref this._TotalProgress, value);
                this.HasChange = (oldVal != value);
            }
        }

        public int GetPercentile(int targetTotalPercentile = 0)
        {
#if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfLessThan(targetTotalPercentile, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(targetTotalPercentile, 100);
#else
            if (targetTotalPercentile < 0 || targetTotalPercentile > 100) throw new ArgumentOutOfRangeException();
#endif
            var currentProg = this.CurrentProgress * 100d;
            var percent = currentProg / this.TotalProgress;
            if (targetTotalPercentile > 0)
            {
                var convertedToFraction = targetTotalPercentile / 100d; // Highest is 1
                return Convert.ToInt32(Math.Ceiling(percent * convertedToFraction));
            }
            else
            {
                return Convert.ToInt32(Math.Ceiling(percent));
            }
        }

        internal GameUpdaterDownloadProgressValue(GameUpdaterProgressCallback parent)
        {
            this.AttachedParent = parent;
        }

        internal void IncreaseCurrentProgress() => Interlocked.Increment(ref this._CurrentProgress);
        internal void IncreaseCurrentProgress(in int increment) => Interlocked.Add(ref this._CurrentProgress, increment);
        internal void IncreaseTotalProgress() => Interlocked.Increment(ref this._TotalProgress);
        internal void IncreaseTotalProgress(in int increment) => Interlocked.Add(ref this._TotalProgress, increment);

        internal long GetCurrentProgress() => Interlocked.Read(in this._CurrentProgress);

        internal void OnStartHPatchZ(in long totalProgress)
        {
            Interlocked.Exchange(ref this._CurrentProgress, 0);
            Interlocked.Exchange(ref this._TotalProgress, totalProgress);
            this.IsInHPatchZ = true;
        }

        internal void SetHPatchZCurrentProgress(in long currentProgress)
        {
            Interlocked.Exchange(ref this._CurrentProgress, currentProgress);
        }

        internal void OnComplete()
        {
            this.AttachedParent.TotalDownloadProgress.IncreaseCurrentProgress();
            this.IsDone = true;
            this.IsInHPatchZ = false;
        }
    }
}
