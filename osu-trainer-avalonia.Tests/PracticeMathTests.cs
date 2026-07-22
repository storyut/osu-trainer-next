using System.Collections.Generic;
using osu_trainer_avalonia.Controls;
using Xunit;

namespace osu_trainer_avalonia.Tests
{
    public class PracticeMathTests
    {
        [Theory]
        [InlineData("1:30", 90000)]
        [InlineData("01:30.250", 90250)]
        [InlineData("0:05", 5000)]
        public void TryParseTimestamp_ParsesValidFormats(string text, int expectedMs)
        {
            Assert.True(PracticeMath.TryParseTimestamp(text, out int ms));
            Assert.Equal(expectedMs, ms);
        }

        [Theory]
        [InlineData("1:60")]
        [InlineData("90")]
        [InlineData("-1:30")]
        [InlineData("")]
        [InlineData("abc")]
        public void TryParseTimestamp_RejectsInvalidFormats(string text)
        {
            Assert.False(PracticeMath.TryParseTimestamp(text, out _));
        }

        [Fact]
        public void TryParseTimestamp_RejectsNull()
        {
            Assert.False(PracticeMath.TryParseTimestamp(null, out _));
        }

        [Fact]
        public void BuildLadder_ExpandsInclusiveSteps()
        {
            var ladder = PracticeMath.BuildLadder(0.90m, 1.10m, 0.05m);
            Assert.Equal(new List<decimal> { 0.90m, 0.95m, 1.00m, 1.05m, 1.10m }, ladder);
        }

        [Fact]
        public void BuildLadder_FromEqualsTo_YieldsOneStep()
        {
            var ladder = PracticeMath.BuildLadder(1.0m, 1.0m, 0.05m);
            Assert.Equal(new List<decimal> { 1.00m }, ladder);
        }

        [Fact]
        public void BuildLadder_ZeroStep_YieldsEmpty()
        {
            Assert.Empty(PracticeMath.BuildLadder(0.90m, 1.10m, 0.0m));
        }

        [Fact]
        public void BuildLadder_NegativeStep_YieldsEmpty()
        {
            Assert.Empty(PracticeMath.BuildLadder(0.90m, 1.10m, -0.05m));
        }

        [Fact]
        public void BuildLadder_FromGreaterThanTo_YieldsEmpty()
        {
            Assert.Empty(PracticeMath.BuildLadder(1.10m, 0.90m, 0.05m));
        }

        [Fact]
        public void BuildLadder_ExcludesToWhenNotLandingOnStep()
        {
            var ladder = PracticeMath.BuildLadder(0.90m, 1.00m, 0.03m);
            Assert.Equal(new List<decimal> { 0.90m, 0.93m, 0.96m, 0.99m }, ladder);
        }

        [Theory]
        [InlineData(90000, 120000, "1m30-2m00")]
        [InlineData(5000, 65000, "0m05-1m05")]
        [InlineData(600000, 660000, "10m00-11m00")]
        public void FormatRangeSuffix_FormatsAsExpected(int startMs, int endMs, string expected)
        {
            Assert.Equal(expected, PracticeMath.FormatRangeSuffix(startMs, endMs));
        }

        // ---- Rule-12 refusal table: rate ladder rows ----

        [Fact]
        public void TryValidateLadder_StepZeroOrLess_Refuses()
        {
            Assert.False(PracticeMath.TryValidateLadder("0.90", "1.10", "0", out _, out string error));
            Assert.Equal("Rate ladder: step must be greater than 0.", error);
        }

        [Fact]
        public void TryValidateLadder_FromGreaterThanTo_Refuses()
        {
            Assert.False(PracticeMath.TryValidateLadder("1.10", "0.90", "0.05", out _, out string error));
            Assert.Equal("Rate ladder: 'from' must not exceed 'to'.", error);
        }

        [Fact]
        public void TryValidateLadder_AnyStepOutsideBounds_Refuses()
        {
            Assert.False(PracticeMath.TryValidateLadder("0.10", "1.10", "0.05", out _, out string error));
            Assert.Equal("Rate ladder: rates must stay between 0.5x and 2.0x.", error);
        }

        [Fact]
        public void TryValidateLadder_MoreThanTenSteps_Refuses()
        {
            Assert.False(PracticeMath.TryValidateLadder("0.50", "2.00", "0.01", out _, out string error));
            Assert.Equal("Rate ladder: at most 10 diffs at a time.", error);
        }

        [Fact]
        public void TryValidateLadder_ValidInput_Succeeds()
        {
            Assert.True(PracticeMath.TryValidateLadder("0.90", "1.10", "0.05", out var ladder, out string error));
            Assert.Equal(string.Empty, error);
            Assert.Equal(new List<decimal> { 0.90m, 0.95m, 1.00m, 1.05m, 1.10m }, ladder);
        }

        // ---- Rule-12 refusal table: practice cut rows ----

        [Fact]
        public void TryValidatePracticeRange_BadTimestamp_Refuses()
        {
            Assert.False(PracticeMath.TryValidatePracticeRange("abc", "1:00", out _, out _, out string error));
            Assert.Equal("Practice cut: bad timestamp.", error);
        }

        [Fact]
        public void TryValidatePracticeRange_StartNotBeforeEnd_Refuses()
        {
            Assert.False(PracticeMath.TryValidatePracticeRange("1:00", "0:40", out _, out _, out string error));
            Assert.Equal("Practice cut: start must be before end.", error);
        }

        [Fact]
        public void TryValidatePracticeRange_OnlyStartFilled_Refuses()
        {
            Assert.False(PracticeMath.TryValidatePracticeRange("0:40", "", out _, out _, out string error));
            Assert.Equal("Practice cut: fill in both start and end.", error);
        }

        [Fact]
        public void TryValidatePracticeRange_OnlyEndFilled_Refuses()
        {
            Assert.False(PracticeMath.TryValidatePracticeRange("", "1:00", out _, out _, out string error));
            Assert.Equal("Practice cut: fill in both start and end.", error);
        }

        [Fact]
        public void TryValidatePracticeRange_BothEmpty_SucceedsWithNoCut()
        {
            Assert.True(PracticeMath.TryValidatePracticeRange("", "", out var range, out _, out string error));
            Assert.Null(range);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void TryValidatePracticeRange_ValidInput_Succeeds()
        {
            Assert.True(PracticeMath.TryValidatePracticeRange("0:40", "1:00", out var range, out string suffix, out string error));
            Assert.Equal(string.Empty, error);
            Assert.Equal((40000, 60000), range);
            Assert.Equal("0m40-1m00", suffix);
        }
    }
}
