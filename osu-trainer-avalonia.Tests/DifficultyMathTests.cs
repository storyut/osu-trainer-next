using osu_trainer_avalonia.Controls;
using Xunit;

namespace osu_trainer_avalonia.Tests
{
    public class DifficultyMathTests
    {
        [Fact]
        public void ParseClamp_ParsesValidValue() =>
            Assert.Equal(9.3, DifficultyMath.ParseClamp("9.3", 0, 11));

        [Fact]
        public void ParseClamp_ClampsAboveMax() =>
            Assert.Equal(11, DifficultyMath.ParseClamp("99", 0, 11));

        [Fact]
        public void ParseClamp_ClampsBelowMin() =>
            Assert.Equal(0, DifficultyMath.ParseClamp("-4", 0, 11));

        [Fact]
        public void ParseClamp_ReturnsNullOnGarbage() =>
            Assert.Null(DifficultyMath.ParseClamp("x", 0, 11));

        [Fact]
        public void ParseClamp_ReturnsNullOnEmpty() =>
            Assert.Null(DifficultyMath.ParseClamp("   ", 0, 11));

        [Fact]
        public void FormatBpm_RendersArrow() =>
            Assert.Equal("180 → 243", DifficultyMath.FormatBpm(180m, 243m));

        [Fact]
        public void FormatBpmRange_HidesRangeForSingleBpm() =>
            Assert.Equal(string.Empty, DifficultyMath.FormatBpmRange(180m, 180m));

        [Fact]
        public void FormatBpmRange_ShowsRangeForVariableBpm() =>
            Assert.Equal("(120 – 240)", DifficultyMath.FormatBpmRange(120m, 240m));

        [Fact]
        public void FormatBpmRange_HidesRangeForNoData() =>
            Assert.Equal(string.Empty, DifficultyMath.FormatBpmRange(0m, 0m));
    }
}
