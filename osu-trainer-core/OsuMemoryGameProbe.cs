using OsuMemoryDataProvider;
using System.Diagnostics;
using System.IO;

namespace OsuTrainerCore
{
    // The only file permitted to name StructuredOsuMemoryReader — everything else
    // talks to IOsuGameProbe instead.
    internal sealed class OsuMemoryGameProbe : IOsuGameProbe
    {
        private StructuredOsuMemoryReader reader;

        public bool IsGameRunning()
        {
            return Process.GetProcessesByName("osu!").Length > 0;
        }

        public string TryGetOsuDirectory()
        {
            var processes = Process.GetProcessesByName("osu!");
            if (processes.Length == 0)
                return null;

            try
            {
                return Path.GetDirectoryName(processes[0].MainModule.FileName);
            }
            catch
            {
                // MainModule access can throw (Win32Exception/InvalidOperationException) if the
                // process exits mid-read or access is denied — treat as "not available this tick".
                return null;
            }
        }

        public bool TryGetStatus(out OsuMemoryStatus status)
        {
            reader ??= new StructuredOsuMemoryReader();
            try
            {
                reader.TryRead(reader.OsuMemoryAddresses.GeneralData);
                status = (OsuMemoryStatus)(reader.OsuMemoryAddresses.GeneralData.OsuStatus);
                return true;
            }
            catch
            {
                // osu!'s memory layout is unreadable this tick (wrong offsets for this build,
                // process exiting, game not far enough into its own startup) — a normal polled
                // failure, not an error to escalate on its own.
                status = default;
                return false;
            }
        }

        public bool TryGetBeatmap(out string folder, out string file)
        {
            reader ??= new StructuredOsuMemoryReader();
            try
            {
                reader.TryRead(reader.OsuMemoryAddresses.Beatmap);
                file = reader.OsuMemoryAddresses.Beatmap.OsuFileName;
                folder = reader.OsuMemoryAddresses.Beatmap.FolderName;
                return true;
            }
            catch
            {
                // Same class of failure as TryGetStatus's catch — memory unreadable this tick.
                folder = null;
                file = null;
                return false;
            }
        }
    }
}
