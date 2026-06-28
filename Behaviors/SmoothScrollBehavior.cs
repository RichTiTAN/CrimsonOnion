using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;

namespace CrimsonOnion.Behaviors
{
    public static class SmoothScrollBehavior
    {
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("IsEnabled", typeof(SmoothScrollBehavior), false);

        public static bool GetIsEnabled(AvaloniaObject element) => element.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(AvaloniaObject element, bool value) => element.SetValue(IsEnabledProperty, value);

        private static DispatcherTimer? _animTimer;
        private static double _targetOffset = 0;
        private static ScrollViewer? _currentScroller;
        private static double _scrollVelocity = 0;

        static SmoothScrollBehavior()
        {
            IsEnabledProperty.Changed.AddClassHandler<ScrollViewer>(OnIsEnabledChanged);
        }

        private static void OnIsEnabledChanged(ScrollViewer scroller, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                scroller.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            }
            else
            {
                scroller.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
            }
        }

        private static void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (sender is ScrollViewer scroller)
            {
                if (e.Handled || e.KeyModifiers.HasFlag(KeyModifiers.Shift) || e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    return;

                // Find the deepest scrollable ScrollViewer that can handle this delta
                var visual = e.Source as Avalonia.Visual;
                ScrollViewer? targetScroller = null;
                while (visual != null)
                {
                    if (visual is ScrollViewer sv && sv.Extent.Height > sv.Viewport.Height)
                    {
                        double min = 0;
                        double max = sv.Extent.Height - sv.Viewport.Height;
                        bool canScroll = !((sv.Offset.Y <= min && e.Delta.Y > 0) || (sv.Offset.Y >= max && e.Delta.Y < 0));
                        if (canScroll)
                        {
                            targetScroller = sv;
                            break;
                        }
                    }
                    visual = visual.GetVisualParent();
                }

                // If a deeper scroll viewer wants to handle this event, do not intercept it
                if (targetScroller != null && targetScroller != scroller)
                    return;

                // Don't intercept if THIS scrollviewer cannot scroll vertically in the requested direction
                if (targetScroller != scroller)
                    return;

                // Reset target offset if the scroll viewer changed, or if user was scrolling manually
                if (_currentScroller != scroller || (_animTimer != null && !_animTimer.IsEnabled))
                {
                    _currentScroller = scroller;
                    _targetOffset = scroller.Offset.Y;
                    _scrollVelocity = 0;
                }

                // If user scrolls opposite to current velocity, cancel out the velocity for responsiveness
                if (Math.Sign(e.Delta.Y) != Math.Sign(_scrollVelocity))
                {
                    _scrollVelocity = 0;
                }

                // Add to velocity
                double scrollAmount = 180; // Speed multiplier
                _scrollVelocity += e.Delta.Y * scrollAmount;
                
                // We already checked boundaries in the visual tree walk, so we just calculate the target


                // Calculate new target
                _targetOffset = _currentScroller.Offset.Y - _scrollVelocity;
                
                // Clamp target
                double maxOffset = scroller.Extent.Height - scroller.Viewport.Height;
                _targetOffset = Math.Max(0, Math.Min(_targetOffset, maxOffset));

                e.Handled = true;

                if (_animTimer == null)
                {
                    _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60fps
                    _animTimer.Tick += AnimTimer_Tick;
                }
                
                if (!_animTimer.IsEnabled)
                    _animTimer.Start();
            }
        }

        private static void AnimTimer_Tick(object? sender, EventArgs e)
        {
            if (_currentScroller == null)
            {
                _animTimer?.Stop();
                return;
            }

            double currentOffset = _currentScroller.Offset.Y;
            double diff = _targetOffset - currentOffset;

            // Apply friction to velocity so it decays
            _scrollVelocity *= 0.82; 

            // Edge-like spring
            if (Math.Abs(diff) < 1.0 && Math.Abs(_scrollVelocity) < 1.0)
            {
                _currentScroller.Offset = new Vector(_currentScroller.Offset.X, _targetOffset);
                _animTimer?.Stop();
            }
            else
            {
                // Smooth ease out based on diff
                double easeAmount = diff * 0.28; 
                _currentScroller.Offset = new Vector(_currentScroller.Offset.X, currentOffset + easeAmount);
            }
        }
    }
}
