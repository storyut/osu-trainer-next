using System;
using OsuTrainerCore;
using Xunit;

namespace osu_trainer_avalonia.Tests
{
    public class BeatmapEditorLockTests
    {
        private class StubCoreHost : ICoreHost
        {
            public void InvokeOnUiThread(Action action) => action();
            public void ShowError(string message) { }
        }

        private static BeatmapEditor NewEditor() => new BeatmapEditor(new StubCoreHost());

        [Fact]
        public void ToggleHpLock_NoBeatmapLoaded_DoesNotThrow()
        {
            var editor = NewEditor();
            Assert.Null(Record.Exception(() => editor.ToggleHpLock()));
            Assert.Null(Record.Exception(() => editor.ToggleHpLock()));
        }

        [Fact]
        public void ToggleCsLock_NoBeatmapLoaded_DoesNotThrow()
        {
            var editor = NewEditor();
            Assert.Null(Record.Exception(() => editor.ToggleCsLock()));
            Assert.Null(Record.Exception(() => editor.ToggleCsLock()));
        }

        [Fact]
        public void ToggleArLock_NoBeatmapLoaded_DoesNotThrow()
        {
            var editor = NewEditor();
            Assert.Null(Record.Exception(() => editor.ToggleArLock()));
            Assert.Null(Record.Exception(() => editor.ToggleArLock()));
        }

        [Fact]
        public void ToggleOdLock_NoBeatmapLoaded_DoesNotThrow()
        {
            var editor = NewEditor();
            Assert.Null(Record.Exception(() => editor.ToggleOdLock()));
            Assert.Null(Record.Exception(() => editor.ToggleOdLock()));
        }

        [Fact]
        public void ToggleHrEmulation_NoBeatmapLoaded_DoesNotThrow()
        {
            var editor = NewEditor();
            Assert.Null(Record.Exception(() => editor.ToggleHrEmulation()));
            Assert.Null(Record.Exception(() => editor.ToggleHrEmulation()));
        }
    }
}
