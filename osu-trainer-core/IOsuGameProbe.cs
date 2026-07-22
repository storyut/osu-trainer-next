using OsuMemoryDataProvider;

namespace OsuTrainerCore
{
    // A false/null return means "osu! is not readable right now" — a normal
    // polled state for the caller to retry next tick, not an error to escalate.
    internal interface IOsuGameProbe
    {
        bool IsGameRunning();
        string TryGetOsuDirectory();
        bool TryGetStatus(out OsuMemoryStatus status);
        bool TryGetBeatmap(out string folder, out string file);
    }
}
