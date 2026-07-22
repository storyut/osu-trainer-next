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

        // ---- BuildAnchoredLadder / FormatLadderPreview ----

        [Theory]
        [InlineData(1.30, 0.10, 0.10, 0.05, new double[] { 1.20, 1.25, 1.30, 1.35, 1.40 })]
        [InlineData(1.00, 0.10, 0.0, 0.05, new double[] { 0.90, 0.95, 1.00 })]
        [InlineData(1.00, 0.0, 0.10, 0.05, new double[] { 1.00, 1.05, 1.10 })]
        [InlineData(1.00, 0.0, 0.20, 0.05, new double[] { 1.00, 1.05, 1.10, 1.15, 1.20 })]
        [InlineData(1.00, 0.10, 0.10, 0.10, new double[] { 0.90, 1.00, 1.10 })]
        [InlineData(1.95, 0.10, 0.10, 0.05, new double[] { 1.85, 1.90, 1.95, 2.00 })]
        [InlineData(0.52, 0.10, 0.0, 0.05, new double[] { 0.50 })]
        [InlineData(2.00, 0.0, 0.20, 0.05, new double[] { 2.00 })]
        [InlineData(0.50, 0.10, 0.10, 0.05, new double[] { 0.50, 0.55, 0.60 })]
        public void BuildAnchoredLadder_MatchesExpected(double anchor, double below, double above, double step, double[] expected)
        {
            var ladder = PracticeMath.BuildAnchoredLadder((decimal)anchor, (decimal)below, (decimal)above, (decimal)step);
            var expectedDecimals = new List<decimal>();
            foreach (var value in expected)
                expectedDecimals.Add((decimal)value);
            Assert.Equal(expectedDecimals, ladder);
        }

        [Fact]
        public void BuildAnchoredLadder_AtLowerBound_NeverEmpty()
        {
            var ladder = PracticeMath.BuildAnchoredLadder(0.5m, 0.10m, 0.10m, 0.05m);
            Assert.NotEmpty(ladder);
            Assert.Equal(0.5m, ladder[0]);
        }

        [Fact]
        public void BuildAnchoredLadder_AtUpperBound_NeverEmpty()
        {
            var ladder = PracticeMath.BuildAnchoredLadder(2.0m, 0.10m, 0.10m, 0.05m);
            Assert.NotEmpty(ladder);
            Assert.Equal(2.0m, ladder[ladder.Count - 1]);
        }

        [Theory]
        [InlineData(1.30, 0.10, 0.10, 0.05, "5 diffs: 1.20 1.25 1.30 1.35 1.40")]
        [InlineData(1.00, 0.10, 0.0, 0.05, "3 diffs: 0.90 0.95 1.00")]
        public void FormatLadderPreview_NormalRendersDiffCountAndRates(double anchor, double below, double above, double step, string expected)
        {
            string preview = PracticeMath.FormatLadderPreview((decimal)anchor, (decimal)below, (decimal)above, (decimal)step);
            Assert.Equal(expected, preview);
        }

        [Fact]
        public void FormatLadderPreview_UpperClamp_AppendsClampNote()
        {
            string preview = PracticeMath.FormatLadderPreview(1.95m, 0.10m, 0.10m, 0.05m);
            Assert.Equal("4 diffs: 1.85 1.90 1.95 2.00 (clamped at 2.00x)", preview);
        }

        [Fact]
        public void FormatLadderPreview_LowerClamp_AppendsClampNoteAndSingularDiff()
        {
            string preview = PracticeMath.FormatLadderPreview(0.52m, 0.10m, 0.0m, 0.05m);
            Assert.Equal("1 diff: 0.50 (clamped at 0.50x)", preview);
        }

        public static IEnumerable<object[]> ShippedPresetsAndSteps()
        {
            (decimal Below, decimal Above)[] presets = new[]
            {
                (0.10m, 0m), (0m, 0.10m), (0.10m, 0.10m), (0m, 0.20m)
            };
            decimal[] steps = { 0.05m, 0.10m };
            foreach (var preset in presets)
                foreach (var step in steps)
                    for (decimal anchor = 0.5m; anchor <= 2.0m; anchor += 0.05m)
                        yield return new object[] { anchor, preset.Below, preset.Above, step };
        }

        [Theory]
        [MemberData(nameof(ShippedPresetsAndSteps))]
        public void BuildAnchoredLadder_ShippedPresets_NeverEmptyAndAtMostTen(decimal anchor, decimal below, decimal above, decimal step)
        {
            var ladder = PracticeMath.BuildAnchoredLadder(anchor, below, above, step);
            Assert.NotEmpty(ladder);
            Assert.True(ladder.Count <= 10);
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
