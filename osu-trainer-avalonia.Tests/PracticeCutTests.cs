using OsuTrainerCore;
using Xunit;

namespace osu_trainer_avalonia.Tests
{
    public class PracticeCutTests
    {
        private static readonly string[] BaseLines =
        {
            "osu file format v14",
            "",
            "[General]",
            "AudioFilename: audio.mp3",
            "",
            "[TimingPoints]",
            "0,333.333333333333,4,2,1,60,1,0",
            "",
            "[HitObjects]",
        };

        private static string[] WithHitObjects(params string[] hitObjectLines)
        {
            var lines = new string[BaseLines.Length + hitObjectLines.Length];
            BaseLines.CopyTo(lines, 0);
            hitObjectLines.CopyTo(lines, BaseLines.Length);
            return lines;
        }

        [Fact]
        public void KeepsObjectsInsideRange()
        {
            var lines = WithHitObjects(
                "100,100,1000,1,0,0:0:0:0:",
                "100,100,2000,1,0,0:0:0:0:",
                "100,100,3000,1,0,0:0:0:0:");

            var (result, count) = PracticeCut.TrimHitObjectLines(lines, 1500, 2500);

            Assert.Equal(1, count);
            Assert.Contains("100,100,2000,1,0,0:0:0:0:", result);
            Assert.DoesNotContain("100,100,1000,1,0,0:0:0:0:", result);
            Assert.DoesNotContain("100,100,3000,1,0,0:0:0:0:", result);
        }

        [Fact]
        public void IsInclusiveAtBothEnds()
        {
            var lines = WithHitObjects(
                "100,100,1500,1,0,0:0:0:0:",
                "100,100,2500,1,0,0:0:0:0:");

            var (result, count) = PracticeCut.TrimHitObjectLines(lines, 1500, 2500);

            Assert.Equal(2, count);
            Assert.Contains("100,100,1500,1,0,0:0:0:0:", result);
            Assert.Contains("100,100,2500,1,0,0:0:0:0:", result);
        }

        [Fact]
        public void KeepsObjectStartingInsideEndingOutside()
        {
            // slider: x,y,time,type,hitSound,curveData,slides,length,...
            var sliderLine = "100,100,2400,2,0,B|200:200,1,300";
            var lines = WithHitObjects(sliderLine);

            var (result, count) = PracticeCut.TrimHitObjectLines(lines, 1500, 2500);

            Assert.Equal(1, count);
            Assert.Contains(sliderLine, result);
        }

        [Fact]
        public void LeavesOtherSectionsByteIdentical()
        {
            var lines = WithHitObjects("100,100,2000,1,0,0:0:0:0:");

            var (result, _) = PracticeCut.TrimHitObjectLines(lines, 1500, 2500);

            for (int i = 0; i < BaseLines.Length; i++)
                Assert.Equal(BaseLines[i], result[i]);
        }

        [Fact]
        public void PreservesCommentsAndBlankLines()
        {
            var lines = WithHitObjects(
                "//comment",
                "",
                "100,100,1000,1,0,0:0:0:0:");

            var (result, count) = PracticeCut.TrimHitObjectLines(lines, 1500, 2500);

            Assert.Equal(0, count);
            Assert.Contains("//comment", result);
            Assert.Contains("", result);
        }

        [Fact]
        public void PreservesUnparseableHitObjectLine()
        {
            var badLine = "100,100,abc,1,0,0:0:0:0:";
            var lines = WithHitObjects(badLine);

            var (result, count) = PracticeCut.TrimHitObjectLines(lines, 1500, 2500);

            Assert.Equal(0, count);
            Assert.Contains(badLine, result);
        }

        [Fact]
        public void ReportsZeroWhenRangeIsEmpty()
        {
            var lines = WithHitObjects(
                "100,100,1000,1,0,0:0:0:0:",
                "100,100,3000,1,0,0:0:0:0:");

            var (result, count) = PracticeCut.TrimHitObjectLines(lines, 1500, 2500);

            Assert.Equal(0, count);
            Assert.Equal(BaseLines.Length, result.Length);
            Assert.DoesNotContain("100,100,1000,1,0,0:0:0:0:", result);
            Assert.DoesNotContain("100,100,3000,1,0,0:0:0:0:", result);
        }
    }
}
