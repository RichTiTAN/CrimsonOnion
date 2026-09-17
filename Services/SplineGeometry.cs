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
internal static class SplineGeometry
{
    private const double MinimumScale = 1024;
    public static global::Avalonia.Media.StreamGeometry Smooth(
        System.Collections.Generic.IReadOnlyList<global::Avalonia.Point> points,
        bool isFill,
        double width,
        double height)
    {
        var geom = new global::Avalonia.Media.StreamGeometry();
        using (var ctx = geom.Open())
        {
            if (points.Count == 0) return geom;

            if (isFill)
            {
                ctx.BeginFigure(new global::Avalonia.Point(points[0].X, height), true);
                ctx.LineTo(points[0]);
            }
            else
            {
                ctx.BeginFigure(points[0], false);
            }

            for (int i = 1; i < points.Count; i++)
            {
                var p0 = i >= 2 ? points[i - 2] : points[i - 1];
                var p1 = points[i - 1];
                var p2 = points[i];
                var p3 = i + 1 < points.Count ? points[i + 1] : points[i];

                double t = 0.25;
                var cp1 = new global::Avalonia.Point(p1.X + (p2.X - p0.X) * t, p1.Y + (p2.Y - p0.Y) * t);
                var cp2 = new global::Avalonia.Point(p2.X - (p3.X - p1.X) * t, p2.Y - (p3.Y - p1.Y) * t);

                ctx.CubicBezierTo(cp1, cp2, p2);
            }

            if (isFill)
            {
                ctx.LineTo(new global::Avalonia.Point(points[points.Count - 1].X, height));
            }
        }
        return geom;
    }
    public static (int Count, double Step) BuildSeries(
        System.Collections.Generic.IReadOnlyCollection<double> up,
        System.Collections.Generic.IReadOnlyCollection<double> down,
        System.Collections.Generic.List<global::Avalonia.Point> upPoints,
        System.Collections.Generic.List<global::Avalonia.Point> downPoints,
        int slots,
        double width,
        double height,
        double topPadding,
        double bottomPadding)
    {
        int count = Math.Min(up.Count, down.Count);
        double step       = width / (slots - 1);
        double baseline   = height - bottomPadding;
        double drawHeight = baseline - topPadding;

        upPoints.Clear();
        downPoints.Clear();

        if (count <= 0) return (0, step);

        double maxVal = 0;
        foreach (double sample in up)   if (sample > maxVal) maxVal = sample;
        foreach (double sample in down) if (sample > maxVal) maxVal = sample;
        if (maxVal < MinimumScale) maxVal = MinimumScale;

        int startIdx = slots - count;

        using var upEnum = up.GetEnumerator();
        using var dnEnum = down.GetEnumerator();

        for (int i = 0; i < count; i++)
        {
            if (!upEnum.MoveNext() || !dnEnum.MoveNext()) break;
            double x   = (startIdx + i) * step;
            double yUp = baseline - (upEnum.Current / maxVal * drawHeight);
            double yDn = baseline - (dnEnum.Current / maxVal * drawHeight);
            upPoints.Add(new global::Avalonia.Point(x, yUp));
            downPoints.Add(new global::Avalonia.Point(x, yDn));
        }

        return (count, step);
    }
}