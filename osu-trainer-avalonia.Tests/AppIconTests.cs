using System;
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
    }
}
