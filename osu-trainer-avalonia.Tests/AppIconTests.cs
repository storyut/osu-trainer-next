using System;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using osu_trainer_avalonia.Controls;
using Xunit;

namespace osu_trainer_avalonia.Tests
{
    public class AppIconTests
    {
        [AvaloniaTheory]
        [InlineData(AppIconKind.Minus)]
        [InlineData(AppIconKind.X)]
        [InlineData(AppIconKind.Lock)]
        [InlineData(AppIconKind.ChevronRight)]
        [InlineData(AppIconKind.ChevronDown)]
        [InlineData(AppIconKind.RotateCcw)]
        [InlineData(AppIconKind.Folder)]
        public void ResolveGeometry_EveryKind_YieldsNonNullGeometry(AppIconKind kind) =>
            Assert.NotNull(AppIcon.ResolveGeometry(kind));

        [AvaloniaFact]
        public void ResolveGeometry_OutOfRangeKind_ThrowsLoudly()
        {
            var bogus = (AppIconKind)999;
            Assert.Throws<ArgumentOutOfRangeException>(() => AppIcon.ResolveGeometry(bogus));
        }

        [AvaloniaFact]
        public void Size_Setter_SetsWidthAndHeight()
        {
            var icon = new AppIcon { Size = 12 };

            Assert.Equal(12, icon.Width);
            Assert.Equal(12, icon.Height);
        }

        [AvaloniaFact]
        public void Stroke_LeftNull_FallsBackToVisibleDefault()
        {
            var icon = new AppIcon();

            Assert.Null(icon.Stroke);
            Assert.NotNull(icon.IconPath.Stroke);
        }

        // The glyphs are only centred and consistently weighted because they are scaled as a
        // 24x24 Lucide grid rather than as their own ink. These two tests pin both halves of
        // that: the grid exists at the right size, and no icon's data escapes it.
        [AvaloniaFact]
        public void IconPath_SitsOnAFixed24x24Canvas()
        {
            var icon = new AppIcon();

            var canvas = Assert.IsType<Canvas>(icon.IconPath.Parent);
            Assert.Equal(24, canvas.Width);
            Assert.Equal(24, canvas.Height);
            Assert.IsType<Viewbox>(canvas.Parent);
        }

        [AvaloniaTheory]
        [InlineData(AppIconKind.Minus)]
        [InlineData(AppIconKind.X)]
        [InlineData(AppIconKind.Lock)]
        [InlineData(AppIconKind.ChevronRight)]
        [InlineData(AppIconKind.ChevronDown)]
        [InlineData(AppIconKind.RotateCcw)]
        [InlineData(AppIconKind.Folder)]
        public void ResolveGeometry_EveryKind_FitsInsideTheLucideGridIncludingStroke(AppIconKind kind)
        {
            // Half the StrokeThickness of 2 spills outside the path on each side.
            const double halfStroke = 1;
            var bounds = AppIcon.ResolveGeometry(kind).Bounds;

            Assert.True(bounds.Left - halfStroke >= 0 && bounds.Top - halfStroke >= 0
                && bounds.Right + halfStroke <= 24 && bounds.Bottom + halfStroke <= 24,
                $"{kind} spills outside Lucide's 24x24 grid: {bounds} inflated by {halfStroke}. "
                + "Icon data must be authored on the 24x24 grid - a path lifted from an SVG "
                + "with a different viewBox will render off-centre and mis-weighted.");
        }
    }
}

