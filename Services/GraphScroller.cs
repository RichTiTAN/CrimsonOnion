/*
 * CrimsonOnion - A GUI client that runs multiple Tor instances and load-balances them.
 * Copyright (C) 2026 RichTiTAN
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

namespace CrimsonOnion.Services;
internal sealed class GraphScroller
{
    private const int FrameMilliseconds = 33;
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(FrameMilliseconds);

    private readonly Func<bool> _canScroll;

    private global::Avalonia.Threading.DispatcherTimer? _timer;
    private global::Avalonia.Media.TranslateTransform? _translate;
    private double _targetX;
    private double _stepX;
    public GraphScroller(Func<bool> canScroll)
    {
        _canScroll = canScroll;
    }
    public bool IsRunning => _timer != null;
    public void Restart(global::Avalonia.Media.TranslateTransform translate, double step)
    {
        _translate  = translate;
        translate.X = 0;
        _targetX    = -step;
        _stepX      = step / (1000.0 / FrameMilliseconds);

        if (_timer != null) return;

        _timer = new global::Avalonia.Threading.DispatcherTimer { Interval = FrameInterval };
        _timer.Tick += (s, e) => Advance();
        _timer.Start();
    }
    public void Advance()
    {
        if (!_canScroll()) return;
        if (_translate == null || _translate.X <= _targetX) return;

        _translate.X -= _stepX;
        if (_translate.X < _targetX) _translate.X = _targetX;
    }
    public void Stop()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer = null;
        }

        _translate = null;
    }
}
