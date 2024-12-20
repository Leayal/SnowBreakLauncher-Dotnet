using System;
using System.Collections.Generic;
using System.Threading;

namespace Leayal.SnowBreakLauncher.Classes
{
    static class ObservableExtensions
    {
        sealed class Stub_OnTargetPropChanged<T> : IObserver<T>
        {
            public readonly Action<T> _onTargetPropChanged;
            private T? lastValue;

            public Stub_OnTargetPropChanged(Action<T> onTargetPropChanged)
            {
                this._onTargetPropChanged = onTargetPropChanged;
                this.lastValue = default;
            }

            public void OnCompleted()
            {
                // Do nothing
            }

            public void OnError(Exception error)
            {
                // Do nothing
            }

            public void OnNext(T value)
            {
                if (!EqualityComparer<T>.Default.Equals(this.lastValue, value))
                {
                    this.lastValue = value;
                    this._onTargetPropChanged(value);
                }
            }
        }

        public static IDisposable Subscribe<T>(this IObservable<T> observable, Action<T> onTargetPropChanged)
        {
            var obj = new Stub_OnTargetPropChanged<T>(onTargetPropChanged);
            return observable.Subscribe(obj);
        }

        public static void Subscribe<T>(this IObservable<T> observable, Action<T> onTargetPropChanged, CancellationToken cancellationToken)
        {
            var canceller = Subscribe(observable, onTargetPropChanged);
            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(canceller.Dispose);
            }
        }
    }
}
