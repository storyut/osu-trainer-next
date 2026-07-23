using System.IO;
using FsBeatmapProcessor;
using OsuTrainerCore;
using Xunit;

namespace osu_trainer_avalonia.Tests
{
    // Unit 015. These 13 cases pin the generated diff/audio/file names branch by branch.
    // They were first written against the PRE-refactor BeatmapEditor.ModifyBeatmapMetadata and
    // passed there; repointing them at ExportNamer.Describe changed only the Describe() helper
    // below, not one expected string. That is what makes the extraction a checkable pure move.
    public class ExportNamerTests
    {
        // Carried over from the editor-driven version of this suite, where the defaults below
        // were the fixture map's own values: 180bpm, HP5 CS4 OD6 AR7, "Fixture Artist - Fixture
        // Title (fablely) [Normal]", audio.mp3.
        private const decimal FixtureBpm = 180M;

        private sealed class NameCase
        {
            public string Artist = "Fixture Artist";
            public string Title = "Fixture Title";
            public string Creator = "fablely";
            public string Version = "Normal";
            public string AudioFilename = "audio.mp3";

            public decimal Multiplier = 1.0M;
            public decimal Bpm = FixtureBpm;
            public GameMode Mode = GameMode.osu;

            public decimal OriginalHP = 5M, NewHP = 5M;
            public decimal OriginalCS = 4M, NewCS = 4M;
            public decimal ScaledAR = 7M, NewAR = 7M;
            public decimal ScaledOD = 6M, NewOD = 6M;

            public bool ChangePitch;
            public bool PreDT;
            public bool ForceRateSuffix;
            public string? RangeSuffix;
        }

        private static ExportNaming Describe(NameCase c) =>
            ExportNamer.Describe(new ExportNamingInputs
            {
                Artist          = c.Artist,
                Title           = c.Title,
                Creator         = c.Creator,
                Version         = c.Version,
                AudioFilename   = c.AudioFilename,
                Bpm             = c.Bpm,
                Mode            = c.Mode,
                OriginalHP      = c.OriginalHP,
                NewHP           = c.NewHP,
                OriginalCS      = c.OriginalCS,
                NewCS           = c.NewCS,
                ScaledAR        = c.ScaledAR,
                NewAR           = c.NewAR,
                ScaledOD        = c.ScaledOD,
                NewOD           = c.NewOD,
                Multiplier      = c.Multiplier,
                ChangePitch     = c.ChangePitch,
                PreDT           = c.PreDT,
                RangeSuffix     = c.RangeSuffix,
                ForceRateSuffix = c.ForceRateSuffix,
            });

        // 1 — unmodified: no rate suffix, no re-appended .mp3, no difficulty suffix.
        [Fact]
        public void UnmodifiedMapKeepsItsNames()
        {
            var (version, audio, _) = Describe(new NameCase());

            Assert.Equal("Normal", version);
            Assert.Equal("audio.mp3", audio);
        }

        // 2 — rate only.
        [Fact]
        public void RateAppendsMultiplierAndBpmToVersionAndAudio()
        {
            var (version, audio, _) = Describe(new NameCase { Multiplier = 1.5M, Bpm = 270M });

            Assert.Equal("Normal 1.5x (270bpm)", version);
            Assert.Equal("audio 1.500x.mp3", audio);
        }

        // 3 — a batch ladder at exactly 1.00x still gets the suffix, or its .osu would collide
        // with the unmodified original inside the .osz.
        [Fact]
        public void ForceRateSuffixAppliesAtExactlyOneTimes()
        {
            var (version, audio, _) = Describe(new NameCase { Multiplier = 1.0M, ForceRateSuffix = true });

            Assert.Equal("Normal 1x (180bpm)", version);
            Assert.Equal("audio 1.000x.mp3", audio);
        }

        // 4 — boundary partner of case 3.
        [Fact]
        public void NoRateSuffixAtOneTimesWithoutForce()
        {
            var (version, audio, _) = Describe(new NameCase { Multiplier = 1.0M, ForceRateSuffix = false });

            Assert.Equal("Normal", version);
            Assert.Equal("audio.mp3", audio);
        }

        // 5 — pitch suffix, both directions.
        [Fact]
        public void PitchRaisedAndLoweredAppearBeforeTheExtension()
        {
            var (_, raised, _) = Describe(new NameCase { Multiplier = 1.25M, Bpm = 225M, ChangePitch = true });
            var (_, lowered, _) = Describe(new NameCase { Multiplier = 0.75M, Bpm = 135M, ChangePitch = true });

            Assert.Equal("audio 1.250x (pitch raised).mp3", raised);
            Assert.Equal("audio 0.750x (pitch lowered).mp3", lowered);
        }

        // 6 — asymmetric guard: the preDT branch suppresses the pitch suffix at 1.00x, the
        // non-preDT branch does not. Only reachable at 1.00x via forceRateSuffix.
        [Fact]
        public void PreDtSuppressesPitchSuffixAtOneTimes()
        {
            var (_, preDt, _) = Describe(new NameCase { Multiplier = 1.0M, ChangePitch = true, PreDT = true });
            var (_, plain, _) = Describe(new NameCase { Multiplier = 1.0M, ChangePitch = true, ForceRateSuffix = true });

            Assert.Equal("audio 1.000x withDT.mp3", preDt);
            Assert.Equal("audio 1.000x (pitch raised).mp3", plain);
        }

        // 7 — preDT naming.
        [Fact]
        public void PreDtAudioCarriesWithDt()
        {
            var (version, audio, _) = Describe(new NameCase { Multiplier = 1.5M, Bpm = 270M, PreDT = true });

            Assert.Equal("Normal 1.5x (270bpm)", version);
            Assert.Equal("audio 1.500x withDT.mp3", audio);
        }

        // 8 — one difficulty setting at a time, then all four in their fixed order.
        [Fact]
        public void EachChangedDifficultySettingGetsItsOwnSuffix()
        {
            Assert.Equal("Normal HP4.5", Describe(new NameCase { NewHP = 4.5M }).Version);
            Assert.Equal("Normal CS3.9", Describe(new NameCase { NewCS = 3.9M }).Version);
            Assert.Equal("Normal AR9.2", Describe(new NameCase { NewAR = 9.2M }).Version);
            Assert.Equal("Normal OD8", Describe(new NameCase { NewOD = 8M }).Version);

            Assert.Equal(
                "Normal HP4.5 CS3.9 AR9.2 OD8",
                Describe(new NameCase { NewHP = 4.5M, NewCS = 3.9M, NewAR = 9.2M, NewOD = 8M }).Version);
        }

        // 9 — taiko and mania have no CS or AR, so those suffixes are suppressed there.
        [Theory]
        [InlineData(GameMode.Taiko)]
        [InlineData(GameMode.Mania)]
        public void TaikoAndManiaSuppressCsAndAr(GameMode mode)
        {
            var (version, _, _) = Describe(new NameCase
            {
                Mode = mode,
                NewHP = 4.5M,
                NewCS = 3.9M,
                NewAR = 9.2M,
                NewOD = 8M,
            });

            Assert.Equal("Normal HP4.5 OD8", version);
        }

        // 10 — above 10, AR and OD are shown even when they match the scaled reference, because
        // osu! silently clamps them and the name is the only record.
        [Fact]
        public void ArAndOdAboveTenAreShownEvenWhenUnchanged()
        {
            var (version, _, _) = Describe(new NameCase
            {
                ScaledAR = 10.5M,
                NewAR = 10.5M,
                ScaledOD = 10.5M,
                NewOD = 10.5M,
            });

            Assert.Equal("Normal AR10.5 OD10.5", version);
        }

        // 11 — practice-range suffix, appended only when non-empty.
        [Fact]
        public void RangeSuffixIsAppendedOnlyWhenNonEmpty()
        {
            Assert.Equal("Normal 1-2", Describe(new NameCase { RangeSuffix = "1-2" }).Version);
            Assert.Equal("Normal", Describe(new NameCase { RangeSuffix = "" }).Version);
            Assert.Equal("Normal", Describe(new NameCase { RangeSuffix = null }).Version);
        }

        // 12 — filename sanitising: every character Windows rejects in a path is stripped.
        [Fact]
        public void FileNameStripsCharactersWindowsRejects()
        {
            var (_, _, osuFileName) = Describe(new NameCase
            {
                Artist = "a\"r*t\\i/s?t",
                Title = "t<i>t|l:e",
                Creator = "cr:eator",
                Version = "N\"ormal",
            });

            Assert.Equal("artist - title (creator) [Normal].osu", osuFileName);
        }

        // 13 — the osutrainer tag is what makes generated maps findable in-game, and what
        // LoadBeatmap keys off to find the unmodified original. It is applied by the wrapper
        // that writes ExportNamer's result onto the map, so this case drives that wrapper.
        [Fact]
        public void OsutrainerTagIsAddedExactlyOnce()
        {
            var editor = EditorFixture.LoadReadyEditor();
            var map = new Beatmap(editor.NewBeatmap);
            var ctx = new ExportContext(editor.OriginalBeatmap, editor.NewBeatmap, editor.BpmRate, false, false, false, null!);

            BeatmapExporter.ApplyNaming(map, ctx, 1.0M);

            Assert.Single(map.Tags, tag => tag == "osutrainer");
        }
    }
}
