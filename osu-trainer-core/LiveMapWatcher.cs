using OsuMemoryDataProvider;
using System;
using System.IO;
using System.Linq;
using System.Timers;

namespace OsuTrainerCore
{
    public class LiveMapWatcher
    {
        private const int StatusFailureReportThreshold = 10;

        private readonly ICoreHost host;
        private readonly IOsuGameProbe probe;
        private readonly Timer processCheckTimer;
        private readonly Timer beatmapCheckTimer;
        private bool gameRunning;
        private bool mapSelectScreen;
        private string previousBeatmapRead;
        private int consecutiveStatusFailures;
        private bool statusFailureReported;

        public string SongsFolder { get; private set; } = "";

        public event EventHandler<string> SongsFolderDetected;
        public event EventHandler<string> BeatmapDetected;

        public LiveMapWatcher(ICoreHost host) : this(host, new OsuMemoryGameProbe())
        {
        }

        internal LiveMapWatcher(ICoreHost host, IOsuGameProbe probe)
        {
            this.host = host;
            this.probe = probe;
            processCheckTimer = new Timer(1000) { AutoReset = true };
            processCheckTimer.Elapsed += (s, e) => PollProcess();
            beatmapCheckTimer = new Timer(500) { AutoReset = true };
            beatmapCheckTimer.Elapsed += (s, e) => PollBeatmap();
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

        internal void PollProcess()
        {
            if (!probe.IsGameRunning())
            {
                gameRunning = false;
                return;
            }
            gameRunning = true;

            if (string.IsNullOrEmpty(SongsFolder))
            {
                string osuDirectory = probe.TryGetOsuDirectory();
                if (osuDirectory != null)
                {
                    SongsFolder = Path.Combine(osuDirectory, "Songs");
                    host.InvokeOnUiThread(() => SongsFolderDetected?.Invoke(this, SongsFolder));
                }
            }

            if (probe.TryGetStatus(out OsuMemoryStatus status))
            {
                mapSelectScreen = status == OsuMemoryStatus.SongSelect
                    || status == OsuMemoryStatus.MultiplayerRoom
                    || status == OsuMemoryStatus.MultiplayerSongSelect;
                consecutiveStatusFailures = 0;
                statusFailureReported = false;
            }
            else
            {
                mapSelectScreen = false;
                consecutiveStatusFailures++;
                if (consecutiveStatusFailures == StatusFailureReportThreshold && !statusFailureReported)
                {
                    statusFailureReported = true;
                    host.ShowError("osu! is running, but its current state can't be read right now.");
                }
            }
        }

        internal void PollBeatmap()
        {
            if (!gameRunning || !mapSelectScreen || string.IsNullOrEmpty(SongsFolder))
                return;

            if (!probe.TryGetBeatmap(out string beatmapFolder, out string beatmapFilename))
                return;

            var invalidChars = Path.GetInvalidPathChars();
            if (string.IsNullOrWhiteSpace(beatmapFilename) || beatmapFilename.Any(c => invalidChars.Contains(c)))
                return;
            if (string.IsNullOrWhiteSpace(beatmapFolder) || beatmapFolder.Any(c => invalidChars.Contains(c)))
                return;

            if (previousBeatmapRead == beatmapFilename)
                return;

            string absoluteFilename = Path.Combine(SongsFolder, beatmapFolder.TrimEnd(), beatmapFilename);
            if (!File.Exists(absoluteFilename))
                return;

            previousBeatmapRead = beatmapFilename;
            host.InvokeOnUiThread(() => BeatmapDetected?.Invoke(this, absoluteFilename));
        }
    }
}
