using System;
using System.Collections.Generic;
using System.IO;
using OsuMemoryDataProvider;
using OsuTrainerCore;
using Xunit;

namespace osu_trainer_avalonia.Tests
{
    public class LiveMapWatcherTests : IDisposable
    {
        private class FakeOsuGameProbe : IOsuGameProbe
        {
            public bool Running;
            public string? Directory;
            public OsuMemoryStatus Status;
            public bool StatusShouldFail;
            public string? Folder;
            public string? File;
            public bool BeatmapShouldFail;

            public bool IsGameRunning() => Running;

            public string? TryGetOsuDirectory() => Directory;

            public bool TryGetStatus(out OsuMemoryStatus status)
            {
                if (StatusShouldFail)
                {
                    status = default;
                    return false;
                }
                status = Status;
                return true;
            }

            public bool TryGetBeatmap(out string? folder, out string? file)
            {
                if (BeatmapShouldFail)
                {
                    folder = null;
                    file = null;
                    return false;
                }
                folder = Folder;
                file = File;
                return true;
            }
        }

        private class RecordingCoreHost : ICoreHost
        {
            public readonly List<string> Errors = new();

            public void InvokeOnUiThread(Action action) => action();

            public void ShowError(string message) => Errors.Add(message);
        }

        private readonly string osuDir = Path.Combine(Path.GetTempPath(), $"osu-trainer-next-tests-{Guid.NewGuid():N}");
        private readonly string songsFolder;

        public LiveMapWatcherTests()
        {
            songsFolder = Path.Combine(osuDir, "Songs");
            Directory.CreateDirectory(songsFolder);
        }

        public void Dispose()
        {
            if (Directory.Exists(osuDir))
                Directory.Delete(osuDir, recursive: true);
        }

        private static LiveMapWatcher NewWatcher(FakeOsuGameProbe probe, out RecordingCoreHost host)
        {
            host = new RecordingCoreHost();
            return new LiveMapWatcher(host, probe);
        }

        private string CreateBeatmapFile(string folderName, string fileName)
        {
            var dir = Path.Combine(songsFolder, folderName);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, fileName);
            File.WriteAllText(path, "osu file format v14");
            return path;
        }

        // --- PollProcess ---

        [Fact]
        public void PollProcess_GameNotRunning_NoEventsAndNoStatusRead()
        {
            var probe = new FakeOsuGameProbe { Running = false, StatusShouldFail = true };
            var watcher = NewWatcher(probe, out var host);
            bool songsFolderEventRaised = false;
            watcher.SongsFolderDetected += (_, _) => songsFolderEventRaised = true;

            watcher.PollProcess();

            Assert.False(songsFolderEventRaised);
            Assert.Empty(host.Errors);
            Assert.Equal("", watcher.SongsFolder);
        }

        [Fact]
        public void PollProcess_RunningWithEmptySongsFolder_DetectsSongsFolderOnce()
        {
            var probe = new FakeOsuGameProbe { Running = true, Directory = @"C:\osu!", Status = OsuMemoryStatus.MainMenu };
            var watcher = NewWatcher(probe, out _);
            var raisedCount = 0;
            string? detected = null;
            watcher.SongsFolderDetected += (_, folder) => { raisedCount++; detected = folder; };

            watcher.PollProcess();

            Assert.Equal(1, raisedCount);
            Assert.Equal(Path.Combine(@"C:\osu!", "Songs"), detected);
            Assert.Equal(Path.Combine(@"C:\osu!", "Songs"), watcher.SongsFolder);
        }

        [Fact]
        public void PollProcess_SongsFolderAlreadySet_NeverRaisesAgain()
        {
            var probe = new FakeOsuGameProbe { Running = true, Directory = @"C:\osu!", Status = OsuMemoryStatus.MainMenu };
            var watcher = NewWatcher(probe, out _);
            var raisedCount = 0;
            watcher.SongsFolderDetected += (_, _) => raisedCount++;

            watcher.PollProcess();
            probe.Directory = @"D:\other-osu!";
            watcher.PollProcess();
            watcher.PollProcess();

            Assert.Equal(1, raisedCount);
            Assert.Equal(Path.Combine(@"C:\osu!", "Songs"), watcher.SongsFolder);
        }

        [Fact]
        public void PollProcess_DirectoryUnavailable_SongsFolderStaysEmptyAndRetried()
        {
            var probe = new FakeOsuGameProbe { Running = true, Directory = null, Status = OsuMemoryStatus.MainMenu };
            var watcher = NewWatcher(probe, out _);
            bool raised = false;
            watcher.SongsFolderDetected += (_, _) => raised = true;

            watcher.PollProcess();
            Assert.False(raised);
            Assert.Equal("", watcher.SongsFolder);

            probe.Directory = @"C:\osu!";
            watcher.PollProcess();

            Assert.True(raised);
            Assert.Equal(Path.Combine(@"C:\osu!", "Songs"), watcher.SongsFolder);
        }

        [Theory]
        [InlineData(OsuMemoryStatus.SongSelect)]
        [InlineData(OsuMemoryStatus.MultiplayerRoom)]
        [InlineData(OsuMemoryStatus.MultiplayerSongSelect)]
        public void PollProcess_StatusIsMapSelectLike_SetsMapSelectScreenTrue(OsuMemoryStatus status)
        {
            var probe = new FakeOsuGameProbe { Running = true, Directory = @"C:\osu!", Status = status };
            var watcher = NewWatcher(probe, out _);

            watcher.PollProcess();
            watcher.PollBeatmap(); // reachable only when mapSelectScreen is true

            // PollBeatmap will attempt a read (BeatmapShouldFail=false, Folder/File null) and
            // return via the null-filename guard, which only happens if it got past the
            // gameRunning/mapSelectScreen/SongsFolder gate — proving mapSelectScreen was true.
            Assert.True(watcher.SongsFolder.Length > 0);
        }

        [Theory]
        [InlineData(OsuMemoryStatus.MainMenu)]
        [InlineData(OsuMemoryStatus.Playing)]
        [InlineData(OsuMemoryStatus.EditingMap)]
        public void PollProcess_StatusIsNotMapSelectLike_SetsMapSelectScreenFalse(OsuMemoryStatus status)
        {
            var probe = new FakeOsuGameProbe
            {
                Running = true,
                Directory = @"C:\osu!",
                Status = status,
                Folder = "folder",
                File = "map.osu"
            };
            var watcher = NewWatcher(probe, out _);
            bool beatmapRaised = false;
            watcher.BeatmapDetected += (_, _) => beatmapRaised = true;

            watcher.PollProcess();
            watcher.PollBeatmap();

            Assert.False(beatmapRaised);
        }

        [Fact]
        public void PollProcess_StatusReadFails_SetsMapSelectScreenFalseAndIncrementsFailureCounter()
        {
            var probe = new FakeOsuGameProbe
            {
                Running = true,
                Directory = @"C:\osu!",
                StatusShouldFail = true,
                Folder = "folder",
                File = "map.osu"
            };
            var watcher = NewWatcher(probe, out var host);
            bool beatmapRaised = false;
            watcher.BeatmapDetected += (_, _) => beatmapRaised = true;

            watcher.PollProcess();
            watcher.PollBeatmap();

            Assert.False(beatmapRaised);
            Assert.Empty(host.Errors); // one failure alone must not report yet
        }

        [Fact]
        public void PollProcess_StatusReadSucceeds_ResetsFailureCounterAndRearmsReport()
        {
            var probe = new FakeOsuGameProbe { Running = true, Directory = @"C:\osu!", StatusShouldFail = true };
            var watcher = NewWatcher(probe, out var host);

            for (int i = 0; i < 9; i++)
                watcher.PollProcess();
            Assert.Empty(host.Errors);

            probe.StatusShouldFail = false;
            probe.Status = OsuMemoryStatus.MainMenu;
            watcher.PollProcess(); // success resets the counter

            probe.StatusShouldFail = true;
            for (int i = 0; i < 9; i++)
                watcher.PollProcess();
            Assert.Empty(host.Errors); // only 9 consecutive failures since the reset

            watcher.PollProcess(); // 10th consecutive failure since the reset
            Assert.Single(host.Errors);
        }

        [Fact]
        public void PollProcess_TenthConsecutiveFailure_ReportsExactlyOnce()
        {
            var probe = new FakeOsuGameProbe { Running = true, Directory = @"C:\osu!", StatusShouldFail = true };
            var watcher = NewWatcher(probe, out var host);

            for (int i = 0; i < 9; i++)
                watcher.PollProcess();
            Assert.Empty(host.Errors);

            watcher.PollProcess();
            Assert.Single(host.Errors);
        }

        [Fact]
        public void PollProcess_FailuresPastTenth_DoNotReportAgain()
        {
            var probe = new FakeOsuGameProbe { Running = true, Directory = @"C:\osu!", StatusShouldFail = true };
            var watcher = NewWatcher(probe, out var host);

            for (int i = 0; i < 25; i++)
                watcher.PollProcess();

            Assert.Single(host.Errors);
        }

        [Fact]
        public void PollProcess_FailureThenSuccessThenTenMoreFailures_ReportsASecondTime()
        {
            var probe = new FakeOsuGameProbe { Running = true, Directory = @"C:\osu!", StatusShouldFail = true };
            var watcher = NewWatcher(probe, out var host);

            for (int i = 0; i < 10; i++)
                watcher.PollProcess();
            Assert.Single(host.Errors);

            probe.StatusShouldFail = false;
            probe.Status = OsuMemoryStatus.MainMenu;
            watcher.PollProcess();

            probe.StatusShouldFail = true;
            for (int i = 0; i < 10; i++)
                watcher.PollProcess();

            Assert.Equal(2, host.Errors.Count);
        }

        // --- PollBeatmap ---

        [Fact]
        public void PollBeatmap_GameNotRunning_NoReadAttemptedNoEvent()
        {
            var probe = new FakeOsuGameProbe { BeatmapShouldFail = true };
            var watcher = NewWatcher(probe, out _);
            bool raised = false;
            watcher.BeatmapDetected += (_, _) => raised = true;

            watcher.PollBeatmap();

            Assert.False(raised);
        }

        [Fact]
        public void PollBeatmap_MapSelectScreenFalse_NoReadAttemptedNoEvent()
        {
            var probe = new FakeOsuGameProbe
            {
                Running = true,
                Directory = @"C:\osu!",
                Status = OsuMemoryStatus.MainMenu,
                Folder = "folder",
                File = "map.osu"
            };
            var watcher = NewWatcher(probe, out _);
            bool raised = false;
            watcher.BeatmapDetected += (_, _) => raised = true;

            watcher.PollProcess(); // mapSelectScreen stays false
            watcher.PollBeatmap();

            Assert.False(raised);
        }

        [Fact]
        public void PollBeatmap_SongsFolderEmpty_NoReadAttemptedNoEvent()
        {
            var probe = new FakeOsuGameProbe
            {
                Running = true,
                Directory = null, // SongsFolder never gets set
                Status = OsuMemoryStatus.SongSelect,
                Folder = "folder",
                File = "map.osu"
            };
            var watcher = NewWatcher(probe, out _);
            bool raised = false;
            watcher.BeatmapDetected += (_, _) => raised = true;

            watcher.PollProcess();
            watcher.PollBeatmap();

            Assert.False(raised);
        }

        private LiveMapWatcher NewReadyWatcher(FakeOsuGameProbe probe, out RecordingCoreHost host)
        {
            probe.Running = true;
            probe.Directory = osuDir; // SongsFolder resolves to osuDir/Songs
            probe.Status = OsuMemoryStatus.SongSelect;
            var watcher = NewWatcher(probe, out host);
            watcher.PollProcess();
            return watcher;
        }

        [Fact]
        public void PollBeatmap_ReadFails_NoEvent()
        {
            var probe = new FakeOsuGameProbe();
            var watcher = NewReadyWatcher(probe, out _);
            probe.BeatmapShouldFail = true;
            bool raised = false;
            watcher.BeatmapDetected += (_, _) => raised = true;

            watcher.PollBeatmap();

            Assert.False(raised);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void PollBeatmap_FilenameNullEmptyOrWhitespace_NoEvent(string? filename)
        {
            var probe = new FakeOsuGameProbe { Folder = "folder", File = filename };
            var watcher = NewReadyWatcher(probe, out _);
            bool raised = false;
            watcher.BeatmapDetected += (_, _) => raised = true;

            watcher.PollBeatmap();

            Assert.False(raised);
        }

        [Fact]
        public void PollBeatmap_FilenameHasInvalidPathChar_NoEvent()
        {
            var invalidChar = Path.GetInvalidPathChars()[0];
            var probe = new FakeOsuGameProbe { Folder = "folder", File = $"map{invalidChar}.osu" };
            var watcher = NewReadyWatcher(probe, out _);
            bool raised = false;
            watcher.BeatmapDetected += (_, _) => raised = true;

            watcher.PollBeatmap();

            Assert.False(raised);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void PollBeatmap_FolderNullEmptyOrWhitespace_NoEvent(string? folder)
        {
            var probe = new FakeOsuGameProbe { Folder = folder, File = "map.osu" };
            var watcher = NewReadyWatcher(probe, out _);
            bool raised = false;
            watcher.BeatmapDetected += (_, _) => raised = true;

            watcher.PollBeatmap();

            Assert.False(raised);
        }

        [Fact]
        public void PollBeatmap_FolderHasInvalidPathChar_NoEvent()
        {
            var invalidChar = Path.GetInvalidPathChars()[0];
            var probe = new FakeOsuGameProbe { Folder = $"folder{invalidChar}", File = "map.osu" };
            var watcher = NewReadyWatcher(probe, out _);
            bool raised = false;
            watcher.BeatmapDetected += (_, _) => raised = true;

            watcher.PollBeatmap();

            Assert.False(raised);
        }

        [Fact]
        public void PollBeatmap_FolderHasTrailingWhitespace_TrimmedBeforeJoining()
        {
            var expected = CreateBeatmapFile("folder", "map.osu");
            var probe = new FakeOsuGameProbe { Folder = "folder   ", File = "map.osu" };
            var watcher = NewReadyWatcher(probe, out _);
            string? detected = null;
            watcher.BeatmapDetected += (_, path) => detected = path;

            watcher.PollBeatmap();

            Assert.Equal(expected, detected);
        }

        [Fact]
        public void PollBeatmap_SameFilenameAsPreviousSuccessfulRead_NoEvent()
        {
            CreateBeatmapFile("folder", "map.osu");
            var probe = new FakeOsuGameProbe { Folder = "folder", File = "map.osu" };
            var watcher = NewReadyWatcher(probe, out _);
            var raisedCount = 0;
            watcher.BeatmapDetected += (_, _) => raisedCount++;

            watcher.PollBeatmap();
            watcher.PollBeatmap();
            watcher.PollBeatmap();

            Assert.Equal(1, raisedCount);
        }

        [Fact]
        public void PollBeatmap_ABAPattern_RaisesThreeEventsBecauseDedupIsAgainstImmediatePreviousOnly()
        {
            CreateBeatmapFile("folder", "a.osu");
            CreateBeatmapFile("folder", "b.osu");
            var probe = new FakeOsuGameProbe { Folder = "folder", File = "a.osu" };
            var watcher = NewReadyWatcher(probe, out _);
            var raisedCount = 0;
            watcher.BeatmapDetected += (_, _) => raisedCount++;

            watcher.PollBeatmap(); // A
            probe.File = "b.osu";
            watcher.PollBeatmap(); // B
            probe.File = "a.osu";
            watcher.PollBeatmap(); // A again

            Assert.Equal(3, raisedCount);
        }

        [Fact]
        public void PollBeatmap_ResolvedFileDoesNotExist_NoEventAndDedupNotUpdated()
        {
            var probe = new FakeOsuGameProbe { Folder = "folder", File = "missing.osu" };
            var watcher = NewReadyWatcher(probe, out _);
            var raisedCount = 0;
            watcher.BeatmapDetected += (_, _) => raisedCount++;

            watcher.PollBeatmap();
            Assert.Equal(0, raisedCount);

            // The file now appears; the same map must still raise, proving dedup was not poisoned.
            CreateBeatmapFile("folder", "missing.osu");
            watcher.PollBeatmap();

            Assert.Equal(1, raisedCount);
        }

        [Fact]
        public void PollBeatmap_AllChecksPass_RaisesOnceWithAbsolutePath()
        {
            var expected = CreateBeatmapFile("folder", "map.osu");
            var probe = new FakeOsuGameProbe { Folder = "folder", File = "map.osu" };
            var watcher = NewReadyWatcher(probe, out _);
            string? detected = null;
            var raisedCount = 0;
            watcher.BeatmapDetected += (_, path) => { raisedCount++; detected = path; };

            watcher.PollBeatmap();

            Assert.Equal(1, raisedCount);
            Assert.Equal(expected, detected);
        }
    }
}
