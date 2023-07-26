using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using System;

namespace Leayal.SnowBreakLauncher.Controls
{
    class CarouselAutoplay
    {
        private readonly Carousel carousel;
        private readonly DispatcherTimer timer;
        private bool isPausing;

        public CarouselAutoplay(Carousel carousel)
        {
            this.carousel = carousel;
            this.isPausing = false;
            carousel.PageTransition = new PageSlide(TimeSpan.FromMilliseconds(500), PageSlide.SlideAxis.Horizontal)
            {
                SlideInEasing = new CubicEaseIn(),
                SlideOutEasing = new CubicEaseOut()
            };

            this.timer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Render, this.Timer_Tick);
            this.timer.Stop();
            this.carousel.ItemsView.CollectionChanged += ItemsView_CollectionChanged;
        }

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
            if (this.isPausing) return;
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

        private void Carousel_PointerExited(object? sender, PointerEventArgs e)
        {
            this.isPausing = false;
        }

        private void Carousel_PointerEntered(object? sender, PointerEventArgs e)
        {
            this.isPausing = true;
        }

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
            this.isPausing = this.carousel.IsPointerOver;
            if (!this.isPausing)
            {
                foreach (var item in this.carousel.ItemsView)
                {
                    if (item is Control ctl)
                    {
                        if (ctl.IsPointerOver)
                        {
                            this.isPausing = true;
                            break;
                        }
                    }
                }
            }
            this.carousel.PointerEntered += this.Carousel_PointerEntered;
            this.carousel.PointerExited += this.Carousel_PointerExited;
            this.timer.Start();
        }

        public void StopAutoplay()
        {
            this.carousel.PointerEntered -= this.Carousel_PointerEntered;
            this.carousel.PointerExited -= this.Carousel_PointerExited;
            this.timer.Stop();
        }
    }
}
