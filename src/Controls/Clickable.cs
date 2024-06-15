using Avalonia.Controls;
using System;
using System.Diagnostics;
using System.Threading;

namespace Leayal.SnowBreakLauncher.Controls
{
    public static class Clickable
    {
        public static Clickable<T> On<T>(T control) where T : Control => new Clickable<T>(control);

        public static Clickable<T> OnClick<T>(this T control, Action<T, Avalonia.Interactivity.RoutedEventArgs> handler) where T : Control
        {
            var clickable = On(control);
            clickable.Click += handler;
            return clickable;
        }
    }

    public class Clickable<T> : IDisposable where T : Control
    {
        private static readonly TimeSpan validDurationForClicking = TimeSpan.FromMilliseconds(400);
        private bool _previouslyClickedOnMe;
        private readonly T target;
        private long _tickClicked;

        public Clickable(T control)
        {
            this._previouslyClickedOnMe = false;
            this.target = control;
            if (control is Button btn)
            {
                btn.Click += this.Btn_Click;
            }
            else
            {
                control.PointerExited += this.Control_PointerExited;
                control.PointerEntered += this.Control_PointerEntered;
            }
        }

        private void Control_PointerEntered(object? sender, Avalonia.Input.PointerEventArgs e)
        {
            this.target.PointerPressed += this.Control_PointerPressed;
            this.target.PointerReleased += this.Control_PointerReleased;
        }

        private void Control_PointerExited(object? sender, Avalonia.Input.PointerEventArgs e)
        {
            this._tickClicked = 0;
            this._previouslyClickedOnMe = false;
            this.target.PointerPressed -= this.Control_PointerPressed;
            this.target.PointerReleased -= this.Control_PointerReleased;
        }

        private void Control_PointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
        {
            if (e.Pointer.Type == Avalonia.Input.PointerType.Mouse && e.InitialPressMouseButton != Avalonia.Input.MouseButton.Left) return;
            var oldVal = this._previouslyClickedOnMe;
            this._previouslyClickedOnMe = false;
            var oldTickWhenPressed = Interlocked.Exchange(ref this._tickClicked, 0);
            if (oldVal && oldTickWhenPressed > 0)
            {
                TimeSpan duration;
                if (Stopwatch.IsHighResolution)
                {
                    duration = Stopwatch.GetElapsedTime(oldTickWhenPressed);
                }
                else
                {
                    duration = DateTime.Now - DateTime.FromBinary(oldTickWhenPressed);
                }
                if (duration <= validDurationForClicking)
                {
                    this.Click?.Invoke(this.target, e);
                }
            }
        }

        private void Control_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            if (e.Pointer.Type == Avalonia.Input.PointerType.Mouse)
            {
                this._previouslyClickedOnMe = e.GetCurrentPoint(sender as Control).Properties.IsLeftButtonPressed;
                Interlocked.Exchange(ref this._tickClicked, Stopwatch.IsHighResolution ? Stopwatch.GetTimestamp() : DateTime.Now.ToBinary());
                return;
            }

            this._previouslyClickedOnMe = true;
            Interlocked.Exchange(ref this._tickClicked, Stopwatch.IsHighResolution ? Stopwatch.GetTimestamp() : DateTime.Now.ToBinary());
        }

        private void Btn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => this.Click?.Invoke(this.target, e);

        public event Action<T, Avalonia.Interactivity.RoutedEventArgs>? Click;

        public void Dispose()
        {
            this.Click = null;
            if (this.target is Button btn)
            {
                btn.Click -= this.Btn_Click;
            }
            else
            {
                this.target.PointerPressed -= this.Control_PointerPressed;
                this.target.PointerReleased -= this.Control_PointerReleased;
                this.target.PointerExited -= this.Control_PointerExited;
                this.target.PointerEntered -= this.Control_PointerEntered;
            }
        }
    }
}
