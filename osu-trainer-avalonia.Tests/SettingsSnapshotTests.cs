using OsuTrainerCore;
using osu_trainer_avalonia.Services;
using Xunit;

namespace osu_trainer_avalonia.Tests
{
    public class SettingsSnapshotTests
    {
        private class StubCoreHost : ICoreHost
        {
            public void InvokeOnUiThread(System.Action action) => action();
            public void ShowError(string message) { }
        }

        private static BeatmapEditor NewEditor() => new BeatmapEditor(new StubCoreHost());

        [Fact]
        public void Capture_NoBeatmap_ReturnsDefaults()
        {
            var editor = NewEditor();

            var s = SettingsSnapshot.Capture(editor, updatesCheckEnabled: false);

            Assert.False(s.BpmIsLocked);
            Assert.False(s.HpIsLocked);
            Assert.False(s.CsIsLocked);
            Assert.False(s.ArIsLocked);
            Assert.False(s.OdIsLocked);
            Assert.Equal(200, s.LockedBpm);
            Assert.Equal(0M, s.LockedHp);
            Assert.Equal(0M, s.LockedCs);
            Assert.Equal(0M, s.LockedAr);
            Assert.Equal(0M, s.LockedOd);
            Assert.Equal(editor.BpmRate, s.BpmRate);
            Assert.Equal(editor.ScaleAR, s.ScaleAR);
            Assert.Equal(editor.ScaleOD, s.ScaleOD);
            Assert.Equal(editor.ForceHardrockCirclesize, s.ForceHardrockCirclesize);
            Assert.Equal(editor.ChangePitch, s.ChangePitch);
            Assert.Equal(editor.NoSpinners, s.NoSpinners);
            Assert.Equal(editor.HighQualityMp3s, s.HighQualityMp3s);
            Assert.False(s.UpdatesCheckEnabled);
        }

        [Fact]
        public void Capture_HpLocked_ReadsBeatmapValue()
        {
            var editor = EditorFixture.LoadReadyEditor();
            editor.ToggleHpLock();
            editor.SetHP(4.5M);

            var s = SettingsSnapshot.Capture(editor, updatesCheckEnabled: true);

            Assert.True(s.HpIsLocked);
            Assert.Equal(4.5M, s.LockedHp);
        }

        [Fact]
        public void Capture_BpmLocked_ReadsBpmData()
        {
            var editor = EditorFixture.LoadReadyEditor();
            editor.ToggleBpmLock();

            var s = SettingsSnapshot.Capture(editor, updatesCheckEnabled: true);

            Assert.True(s.BpmIsLocked);
            Assert.Equal((int)editor.GetNewBpmData().Item1, s.LockedBpm);
        }

        [Fact]
        public void Capture_CsLocked_ReadsBeatmapValue()
        {
            var editor = EditorFixture.LoadReadyEditor();
            editor.ToggleCsLock();
            editor.SetCS(3.5M);

            var s = SettingsSnapshot.Capture(editor, updatesCheckEnabled: true);

            Assert.True(s.CsIsLocked);
            Assert.Equal(3.5M, s.LockedCs);
        }

        [Fact]
        public void Capture_ArLocked_ReadsBeatmapValue()
        {
            var editor = EditorFixture.LoadReadyEditor();
            editor.ToggleArLock();
            editor.SetAR(8.5M);

            var s = SettingsSnapshot.Capture(editor, updatesCheckEnabled: true);

            Assert.True(s.ArIsLocked);
            Assert.Equal(8.5M, s.LockedAr);
        }

        [Fact]
        public void Capture_OdLocked_ReadsBeatmapValue()
        {
            var editor = EditorFixture.LoadReadyEditor();
            editor.ToggleOdLock();
            editor.SetOD(2.5M);

            var s = SettingsSnapshot.Capture(editor, updatesCheckEnabled: true);

            Assert.True(s.OdIsLocked);
            Assert.Equal(2.5M, s.LockedOd);
        }

        [Fact]
        public void ApplyImmediate_SetsRateScaleAndToggles()
        {
            var editor = NewEditor();
            var s = new AppSettings { BpmRate = 1.3M, ScaleAR = false, ScaleOD = true, NoSpinners = true };

            SettingsSnapshot.ApplyImmediate(editor, s);

            Assert.Equal(1.3M, editor.BpmRate);
            Assert.False(editor.ScaleAR);
            Assert.True(editor.ScaleOD);
            Assert.True(editor.NoSpinners);
            Assert.False(editor.ChangePitch);
            Assert.False(editor.HighQualityMp3s);
        }

        [Fact]
        public void ApplyOnReady_Hr_TogglesHrNotCs()
        {
            var editor = EditorFixture.LoadReadyEditor();
            var s = new AppSettings { ForceHardrockCirclesize = true, CsIsLocked = true, LockedCs = 3M };

            SettingsSnapshot.ApplyOnReady(editor, s);

            Assert.True(editor.ForceHardrockCirclesize);
            Assert.False(editor.CsIsLocked);
        }

        [Fact]
        public void ApplyOnReady_HpLock_SetsBeatmapHp()
        {
            var editor = EditorFixture.LoadReadyEditor();
            var s = new AppSettings { HpIsLocked = true, LockedHp = 3.0M };

            SettingsSnapshot.ApplyOnReady(editor, s);

            Assert.True(editor.HpIsLocked);
            Assert.Equal(3.0M, editor.NewBeatmap.HPDrainRate);
        }

        [Fact]
        public void ApplyOnReady_CsLock_SetsBeatmapCs()
        {
            var editor = EditorFixture.LoadReadyEditor();
            var s = new AppSettings { CsIsLocked = true, LockedCs = 3.0M };

            SettingsSnapshot.ApplyOnReady(editor, s);

            Assert.True(editor.CsIsLocked);
            Assert.Equal(3.0M, editor.NewBeatmap.CircleSize);
        }

        [Fact]
        public void ApplyOnReady_ArLock_SetsBeatmapAr()
        {
            var editor = EditorFixture.LoadReadyEditor();
            var s = new AppSettings { ArIsLocked = true, LockedAr = 9.0M };

            SettingsSnapshot.ApplyOnReady(editor, s);

            Assert.True(editor.ArIsLocked);
            Assert.Equal(9.0M, editor.NewBeatmap.ApproachRate);
        }

        [Fact]
        public void ApplyOnReady_OdLock_SetsBeatmapOd()
        {
            var editor = EditorFixture.LoadReadyEditor();
            var s = new AppSettings { OdIsLocked = true, LockedOd = 2.0M };

            SettingsSnapshot.ApplyOnReady(editor, s);

            Assert.True(editor.OdIsLocked);
            Assert.Equal(2.0M, editor.NewBeatmap.OverallDifficulty);
        }

        [Fact]
        public void ApplyOnReady_BpmLock_SetsBeatmapBpm()
        {
            var editor = EditorFixture.LoadReadyEditor();
            var s = new AppSettings { BpmIsLocked = true, LockedBpm = 150 };

            SettingsSnapshot.ApplyOnReady(editor, s);

            Assert.True(editor.BpmIsLocked);
            Assert.Equal(150, (int)editor.GetNewBpmData().Item1);
        }

        [Fact]
        public void RoundTrip_CaptureApplyCapture_Equal()
        {
            var source = EditorFixture.LoadReadyEditor();
            source.ToggleHpLock();
            source.SetHP(3.5M);
            source.SetScaleAR(false);
            source.ToggleArLock();
            source.SetAR(9.0M);
            source.ToggleNoSpinners();
            var captured = SettingsSnapshot.Capture(source, updatesCheckEnabled: true);

            var target = EditorFixture.LoadReadyEditor();
            SettingsSnapshot.ApplyImmediate(target, captured);
            SettingsSnapshot.ApplyOnReady(target, captured);
            var recaptured = SettingsSnapshot.Capture(target, updatesCheckEnabled: true);

            Assert.Equal(captured.BpmRate, recaptured.BpmRate);
            Assert.Equal(captured.BpmIsLocked, recaptured.BpmIsLocked);
            Assert.Equal(captured.LockedBpm, recaptured.LockedBpm);
            Assert.Equal(captured.HpIsLocked, recaptured.HpIsLocked);
            Assert.Equal(captured.LockedHp, recaptured.LockedHp);
            Assert.Equal(captured.CsIsLocked, recaptured.CsIsLocked);
            Assert.Equal(captured.LockedCs, recaptured.LockedCs);
            Assert.Equal(captured.ArIsLocked, recaptured.ArIsLocked);
            Assert.Equal(captured.LockedAr, recaptured.LockedAr);
            Assert.Equal(captured.OdIsLocked, recaptured.OdIsLocked);
            Assert.Equal(captured.LockedOd, recaptured.LockedOd);
            Assert.Equal(captured.ForceHardrockCirclesize, recaptured.ForceHardrockCirclesize);
            Assert.Equal(captured.NoSpinners, recaptured.NoSpinners);
        }
    }
}
