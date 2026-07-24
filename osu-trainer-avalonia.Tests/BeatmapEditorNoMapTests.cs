using OsuTrainerCore;
using Xunit;

namespace osu_trainer_avalonia.Tests
{
    public class BeatmapEditorNoMapTests
    {
        [Fact]
        public void EmptyEditor_IsGenuinelyInTheCrashState()
        {
            var editor = EditorFixture.NewEmptyEditor();
            Assert.Equal(EditorState.NOT_READY, editor.State);
            Assert.Null(editor.NewBeatmap);
            Assert.Null(editor.OriginalBeatmap);
        }

        [Fact]
        public void LoadProfile_NoMap_DoesNotThrow()
        {
            var editor = EditorFixture.NewEmptyEditor();
            Assert.Null(Record.Exception(() => editor.LoadProfile(0)));
        }

        [Fact]
        public void SaveProfile_NoMap_DoesNotThrow()
        {
            var editor = EditorFixture.NewEmptyEditor();
            Assert.Null(Record.Exception(() => editor.SaveProfile(0)));
        }

        [Fact]
        public void SetBpmMultiplier_NoMap_BpmNotLocked_DoesNotThrow()
        {
            var editor = EditorFixture.NewEmptyEditor();
            Assert.Null(Record.Exception(() => editor.SetBpmMultiplier(1.3M)));
        }

        [Fact]
        public void SetBpmMultiplier_NoMap_BpmLocked_DoesNotThrow()
        {
            // C3's real repro: BPM lock can be toggled with no map, then the rate
            // slider drives SetBpmMultiplier while BpmIsLocked is true.
            var editor = EditorFixture.NewEmptyEditor();
            editor.ToggleBpmLock();
            Assert.Null(Record.Exception(() => editor.SetBpmMultiplier(1.3M)));
        }

        [Fact]
        public void NewMapIsDifferent_NoMap_DoesNotThrow()
        {
            var editor = EditorFixture.NewEmptyEditor();
            Assert.Null(Record.Exception(() => editor.NewMapIsDifferent()));
        }

        [Fact]
        public void GetScaledAR_NoMap_DoesNotThrow()
        {
            var editor = EditorFixture.NewEmptyEditor();
            Assert.Null(Record.Exception(() => editor.GetScaledAR()));
        }

        [Fact]
        public void GetScaledOD_NoMap_DoesNotThrow()
        {
            var editor = EditorFixture.NewEmptyEditor();
            Assert.Null(Record.Exception(() => editor.GetScaledOD()));
        }

        [Fact]
        public void NewMapIsDifferent_NoMap_ReturnsFalse()
        {
            var editor = EditorFixture.NewEmptyEditor();
            Assert.False(editor.NewMapIsDifferent());
        }

        [Fact]
        public void GetScaledAR_NoMap_ReturnsZero()
        {
            var editor = EditorFixture.NewEmptyEditor();
            Assert.Equal(0M, editor.GetScaledAR());
        }

        [Fact]
        public void GetScaledOD_NoMap_ReturnsZero()
        {
            var editor = EditorFixture.NewEmptyEditor();
            Assert.Equal(0M, editor.GetScaledOD());
        }

        [Fact]
        public void LoadProfile_NoMap_DoesNotMutateState()
        {
            var editor = EditorFixture.NewEmptyEditor();
            var before = Snapshot(editor);

            editor.LoadProfile(0);

            AssertUnchanged(before, editor);
        }

        [Fact]
        public void SaveProfile_NoMap_DoesNotMutateState()
        {
            var editor = EditorFixture.NewEmptyEditor();
            var before = Snapshot(editor);

            editor.SaveProfile(0);

            AssertUnchanged(before, editor);
        }

        private record EditorSnapshot(
            bool HpIsLocked, bool CsIsLocked, bool ArIsLocked, bool OdIsLocked, bool BpmIsLocked,
            bool ScaleAR, bool ScaleOD, decimal BpmRate, ProfileSnapshot Profile0);

        private record ProfileSnapshot(
            string Name, bool HpIsLocked, bool CsIsLocked, bool ArIsLocked, bool OdIsLocked,
            decimal lockedHP, decimal lockedCS, decimal lockedAR, decimal lockedOD,
            bool ScaleAR, bool ScaleOD, bool ForceHardrockCirclesize, bool ChangePitch,
            bool NoSpinners, bool BpmIsLocked, int lockedBpm, decimal BpmMultiplier);

        private static EditorSnapshot Snapshot(BeatmapEditor editor)
        {
            var p = editor.UserProfiles[0];
            return new EditorSnapshot(
                editor.HpIsLocked, editor.CsIsLocked, editor.ArIsLocked, editor.OdIsLocked,
                editor.BpmIsLocked, editor.ScaleAR, editor.ScaleOD, editor.BpmRate,
                new ProfileSnapshot(
                    p.Name, p.HpIsLocked, p.CsIsLocked, p.ArIsLocked, p.OdIsLocked,
                    p.lockedHP, p.lockedCS, p.lockedAR, p.lockedOD,
                    p.ScaleAR, p.ScaleOD, p.ForceHardrockCirclesize, p.ChangePitch,
                    p.NoSpinners, p.BpmIsLocked, p.lockedBpm, p.BpmMultiplier));
        }

        private static void AssertUnchanged(EditorSnapshot before, BeatmapEditor editor)
        {
            Assert.Equal(before, Snapshot(editor));
        }
    }
}
