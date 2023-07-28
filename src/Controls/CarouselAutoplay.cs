using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Leayal.SnowBreakLauncher.Controls
{
    class CarouselAutoplay : IDisposable
    {
        private readonly Carousel carousel;
        private readonly DispatcherTimer timer;
        private bool isPlaying;
        private readonly IDisposable observeProp;
        private WindowBase? topLevel;

        public bool KeepPlayingWhenWindowUnfocused;

        public CarouselAutoplay(Carousel carousel)
        {
            this.carousel = carousel;
            this.isPlaying = false;
            this.KeepPlayingWhenWindowUnfocused = false;
            carousel.PageTransition = new PageSlide(TimeSpan.FromMilliseconds(500), PageSlide.SlideAxis.Horizontal)
            {
                SlideInEasing = new CubicEaseOut(),
                SlideOutEasing = new CubicEaseIn()
            };

            carousel.Loaded += this.Carousel_Loaded;
            carousel.Unloaded += this.Carousel_Unloaded;

            this.timer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Normal, this.Timer_Tick);
            this.timer.Stop();
            this.carousel.ItemsView.CollectionChanged += this.ItemsView_CollectionChanged;
            var observable = this.carousel.GetObservable(Carousel.IsEffectivelyEnabledProperty);
            this.observeProp = observable.Subscribe(this.OnTargetPropChanged);
        }

        private void Carousel_Unloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var topLevel = Interlocked.Exchange(ref this.topLevel, null);
            if (topLevel != null)
            {
                topLevel.Activated -= this.TopLevel_Activated;
                topLevel.Deactivated -= this.TopLevel_Deactivated;
            }
        }

        private void Carousel_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var newTopLevel = TopLevel.GetTopLevel(this.carousel) as WindowBase;
            var oldTopLevel = Interlocked.Exchange(ref this.topLevel, newTopLevel);
            if (oldTopLevel != newTopLevel)
            {
                if (oldTopLevel != null)
                {
                    oldTopLevel.Activated -= this.TopLevel_Activated;
                    oldTopLevel.Deactivated -= this.TopLevel_Deactivated;
                }
                if (newTopLevel != null)
                {
                    newTopLevel.Activated += this.TopLevel_Activated;
                    newTopLevel.Deactivated += this.TopLevel_Deactivated;

                    if (newTopLevel.IsActive)
                        this.TopLevel_Activated(newTopLevel, EventArgs.Empty);
                    else
                        this.TopLevel_Deactivated(newTopLevel, EventArgs.Empty);
                }
            }
        }

        private void TopLevel_Activated(object? sender, EventArgs e)
        {
            if (!this.carousel.IsEffectivelyVisible) return;
            this.Unpause();
        }

        private void TopLevel_Deactivated(object? sender, EventArgs e) => this.Pause();

        private void OnTargetPropChanged(bool newValue)
        {
            if (newValue)
            {
                if (this.topLevel?.IsActive ?? true)
                {
                    this.Unpause();
                }
            }
            else
            {
                this.Pause();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Unpause()
        {
            if (this.isPlaying)
            {
                this.timer.Start();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Pause() => this.timer.Stop();

        private void ItemsView_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is Control ctl)
                    {
                        ctl.PointerEntered -= this.Carousel_PointerEntered;
                        ctl.PointerExited -= this.Carousel_PointerExited;
                    }
                }
            }
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is Control ctl)
                    {
                        ctl.PointerEntered += this.Carousel_PointerEntered;
                        ctl.PointerExited += this.Carousel_PointerExited;
                    }
                }
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var lastIndex = this.carousel.ItemsView.Count - 1;
            if (lastIndex == -1) return;
            if (this.carousel.SelectedIndex == lastIndex)
            {
                this.carousel.SelectedIndex = 0;
            }
            else
            {
                // this.carousel.Next();
                ++this.carousel.SelectedIndex;
            }
        }

        private void Carousel_PointerExited(object? sender, PointerEventArgs e) => this.Unpause();

        private void Carousel_PointerEntered(object? sender, PointerEventArgs e) => this.Pause();

        private void ResetTimer()
        {
            if (this.timer.IsEnabled)
            {
                // Recount the timer from the start(?)
                this.timer.Stop();
                this.timer.Start();
            }
        }

        public void GoLeft()
        {
            this.ResetTimer();
            this.carousel.Previous();
        }

        public void GoRight()
        {
            this.ResetTimer();
            this.carousel.Next();
        }

        public void StartAutoplay()
        {
            if (this.isPlaying) return;
            this.isPlaying = true;

            var carousel = this.carousel;
            carousel.PointerEntered += this.Carousel_PointerEntered;
            carousel.PointerExited += this.Carousel_PointerExited;

            if (carousel.IsPointerOver) return;
            foreach (var item in carousel.ItemsView)
            {
                if (item is Control ctl)
                {
                    if (ctl.IsPointerOver)
                    {
                        return;
                    }
                }
            }

            if (!(this.topLevel?.IsActive ?? false)) return;

            this.timer.Start();
        }

        public void StopAutoplay()
        {
            if (!this.isPlaying) return;
            this.isPlaying = false;
            this.carousel.PointerEntered -= this.Carousel_PointerEntered;
            this.carousel.PointerExited -= this.Carousel_PointerExited;
            this.timer.Stop();
        }

        public void Dispose()
        {
            this.StopAutoplay();
            this.observeProp?.Dispose();
            this.carousel.Loaded -= this.Carousel_Loaded;
            this.carousel.Unloaded -= this.Carousel_Unloaded;
            if (this.topLevel != null)
            {
                this.topLevel.Activated -= this.TopLevel_Activated;
                this.topLevel.Deactivated -= this.TopLevel_Deactivated;
            }
        }
    }
}
