using OsuMemoryDataProvider;
using OsuMemoryDataProvider.OsuMemoryModels.Direct;
using ProcessMemoryDataFinder.API;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Timers;

namespace OsuTrainerCore
{
    public class LiveMapWatcher
    {
        private readonly ICoreHost host;
        private readonly StructuredOsuMemoryReader osuReader = new StructuredOsuMemoryReader();
        private readonly Timer processCheckTimer;
        private readonly Timer beatmapCheckTimer;
        private bool gameRunning;
        private bool mapSelectScreen;
        private string previousBeatmapRead;

        public string SongsFolder { get; private set; } = "";

        public event EventHandler<string> SongsFolderDetected;
        public event EventHandler<string> BeatmapDetected;

        public LiveMapWatcher(ICoreHost host)
        {
            this.host = host;
            processCheckTimer = new Timer(1000) { AutoReset = true };
            processCheckTimer.Elapsed += (s, e) => CheckOsuProcess();
            beatmapCheckTimer = new Timer(500) { AutoReset = true };
            beatmapCheckTimer.Elapsed += (s, e) => CheckBeatmap();
        }

        public void Start()
        {
            processCheckTimer.Start();
            beatmapCheckTimer.Start();
        }

        public void Stop()
        {
            processCheckTimer.Stop();
            beatmapCheckTimer.Stop();
        }

        private void CheckOsuProcess()
        {
            var processes = Process.GetProcessesByName("osu!");
            if (processes.Length == 0)
            {
                gameRunning = false;
                return;
            }
            gameRunning = true;

            if (string.IsNullOrEmpty(SongsFolder))
            {
                try
                {
                    string osuExePath = processes[0].MainModule.FileName;
                    SongsFolder = Path.Combine(Path.GetDirectoryName(osuExePath), "Songs");
                    host.InvokeOnUiThread(() => SongsFolderDetected?.Invoke(this, SongsFolder));
                }
                catch { }
            }

            try
            {
                osuReader.TryRead(osuReader.OsuMemoryAddresses.GeneralData);
                var status = (OsuMemoryStatus)(osuReader.OsuMemoryAddresses.GeneralData.OsuStatus);
                mapSelectScreen = status == OsuMemoryStatus.SongSelect
                    || status == OsuMemoryStatus.MultiplayerRoom
                    || status == OsuMemoryStatus.MultiplayerSongSelect;
            }
            catch
            {
                mapSelectScreen = false;
            }
        }

        private void CheckBeatmap()
        {
            if (!gameRunning || !mapSelectScreen || string.IsNullOrEmpty(SongsFolder))
                return;

            string beatmapFilename, beatmapFolder;
            try
            {
                osuReader.TryRead(osuReader.OsuMemoryAddresses.Beatmap);
                beatmapFilename = osuReader.OsuMemoryAddresses.Beatmap.OsuFileName;
                beatmapFolder = osuReader.OsuMemoryAddresses.Beatmap.FolderName;
            }
            catch
            {
                return;
            }

            var invalidChars = Path.GetInvalidPathChars();
            if (string.IsNullOrWhiteSpace(beatmapFilename) || beatmapFilename.Any(c => invalidChars.Contains(c)))
                return;
            if (string.IsNullOrWhiteSpace(beatmapFolder) || beatmapFolder.Any(c => invalidChars.Contains(c)))
                return;

            if (previousBeatmapRead == beatmapFilename)
                return;
            previousBeatmapRead = beatmapFilename;

            string absoluteFilename = Path.Combine(SongsFolder, beatmapFolder.TrimEnd(), beatmapFilename);
            if (!File.Exists(absoluteFilename))
                return;

            host.InvokeOnUiThread(() => BeatmapDetected?.Invoke(this, absoluteFilename));
        }
    }
}
