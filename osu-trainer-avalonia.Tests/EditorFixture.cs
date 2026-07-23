using System;
using System.IO;
using System.Threading;
using OsuTrainerCore;

namespace osu_trainer_avalonia.Tests
{
    internal static class EditorFixture
    {
        private class StubCoreHost : ICoreHost
        {
            public void InvokeOnUiThread(Action action) => action();
            public void ShowError(string message) { }
        }

        public static string FixturePath =>
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "minimal.osu");

        public static BeatmapEditor LoadReadyEditor()
        {
            var editor = new BeatmapEditor(new StubCoreHost());
            using var ready = new ManualResetEventSlim(false);
            EventHandler onSwitched = (_, _) => { if (editor.State == EditorState.READY) ready.Set(); };
            editor.BeatmapSwitched += onSwitched;
            editor.RequestBeatmapLoad(FixturePath);
            if (!ready.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("fixture beatmap never reached READY");
            editor.BeatmapSwitched -= onSwitched;
            return editor;
        }
    }
}
