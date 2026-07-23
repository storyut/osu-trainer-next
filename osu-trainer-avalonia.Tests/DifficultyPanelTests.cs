using System;
using osu_trainer_avalonia.Controls;
using OsuTrainerCore;
using Xunit;

namespace osu_trainer_avalonia.Tests
{
    public class DifficultyPanelTests
    {
        private class StubCoreHost : ICoreHost
        {
            public void InvokeOnUiThread(Action action) => action();
            public void ShowError(string message) { }
        }

        private static BeatmapEditor NewEmptyEditor() => new BeatmapEditor(new StubCoreHost());

        [Theory]
        [InlineData(EditorState.NOT_READY, true, false)]
        [InlineData(EditorState.NOT_READY, false, false)]
        [InlineData(EditorState.READY, true, true)]
        [InlineData(EditorState.READY, false, false)]
        [InlineData(EditorState.GENERATING_BEATMAP, true, false)]
        [InlineData(EditorState.GENERATING_BEATMAP, false, false)]
        public void IsPanelEnabled_Table(EditorState state, bool hasBeatmap, bool expected) =>
            Assert.Equal(expected, DifficultyPanel.IsPanelEnabled(state, hasBeatmap));

        [Fact]
        public void Project_NoBeatmap_IsDisabledWithPlaceholderLine()
        {
            var editor = NewEmptyEditor();

            var state = DifficultyPanel.Project(editor);

            Assert.False(state.Enabled);
            Assert.Equal("—", state.BpmLine);
            Assert.Equal(0, state.Hp);
            Assert.Equal(0, state.Cs);
            Assert.Equal(0, state.Ar);
            Assert.Equal(0, state.Od);
            Assert.False(state.HpLocked);
            Assert.False(state.CsLocked);
            Assert.False(state.ArLocked);
            Assert.False(state.OdLocked);
            Assert.Equal((double)editor.BpmRate, state.Rate);
        }

        [Fact]
        public void Project_ReadyBeatmap_MirrorsDifficultyValues()
        {
            var editor = EditorFixture.LoadReadyEditor();

            var state = DifficultyPanel.Project(editor);

            Assert.True(state.Enabled);
            Assert.Equal((double)editor.NewBeatmap.HPDrainRate, state.Hp);
            Assert.Equal((double)editor.NewBeatmap.CircleSize, state.Cs);
            Assert.Equal((double)editor.NewBeatmap.ApproachRate, state.Ar);
            Assert.Equal((double)editor.NewBeatmap.OverallDifficulty, state.Od);
        }

        [Fact]
        public void Project_ReadyBeatmap_MirrorsEachLockIndependently()
        {
            var editor = EditorFixture.LoadReadyEditor();

            var initial = DifficultyPanel.Project(editor);
            Assert.False(initial.HpLocked);
            Assert.False(initial.CsLocked);
            Assert.False(initial.ArLocked);
            Assert.False(initial.OdLocked);

            editor.ToggleHpLock();
            var hp = DifficultyPanel.Project(editor);
            Assert.True(hp.HpLocked);
            Assert.False(hp.CsLocked);
            Assert.False(hp.ArLocked);
            Assert.False(hp.OdLocked);
            editor.ToggleHpLock();

            editor.ToggleCsLock();
            var cs = DifficultyPanel.Project(editor);
            Assert.False(cs.HpLocked);
            Assert.True(cs.CsLocked);
            Assert.False(cs.ArLocked);
            Assert.False(cs.OdLocked);
            editor.ToggleCsLock();

            editor.ToggleArLock();
            var ar = DifficultyPanel.Project(editor);
            Assert.False(ar.HpLocked);
            Assert.False(ar.CsLocked);
            Assert.True(ar.ArLocked);
            Assert.False(ar.OdLocked);
            editor.ToggleArLock();

            editor.ToggleOdLock();
            var od = DifficultyPanel.Project(editor);
            Assert.False(od.HpLocked);
            Assert.False(od.CsLocked);
            Assert.False(od.ArLocked);
            Assert.True(od.OdLocked);
        }

        [Fact]
        public void Project_ReadyBeatmap_MirrorsRateAndBpmLine()
        {
            var editor = EditorFixture.LoadReadyEditor();
            editor.SetBpmMultiplier(1.3M);

            var state = DifficultyPanel.Project(editor);

            Assert.Equal(1.3, state.Rate);
            Assert.Equal(
                DifficultyMath.FormatRateBpmLine(
                    editor.BpmRate,
                    editor.GetOriginalBpmData().Item1,
                    editor.GetNewBpmData().Item1),
                state.BpmLine);
        }
    }
}
