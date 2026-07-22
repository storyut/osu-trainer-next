using System;
using System.IO;

namespace OsuTrainerCore
{
    // A hit-object line is comma-separated with the start time in field index 2:
    // x,y,time,type,hitSound,...
    internal static class PracticeCut
    {
        internal static (string[] Lines, int SurvivingObjects) TrimHitObjectLines(string[] lines, int startMs, int endMs)
        {
            var result = new string[lines.Length];
            int survivingObjects = 0;
            bool inHitObjects = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (!inHitObjects)
                {
                    result[i] = line;
                    if (line.Trim() == "[HitObjects]")
                        inHitObjects = true;
                    continue;
                }

                if (!IsHitObjectLine(line, out int time))
                {
                    // comment, blank line, or unparseable hit-object line: preserved, not counted
                    result[i] = line;
                    continue;
                }

                if (time >= startMs && time <= endMs)
                {
                    result[i] = line;
                    survivingObjects++;
                }
                else
                {
                    result[i] = null;
                }
            }

            // compact out the removed (null) lines, preserving order
            int keptCount = 0;
            for (int i = 0; i < result.Length; i++)
                if (result[i] != null)
                    keptCount++;

            var compacted = new string[keptCount];
            int j = 0;
            for (int i = 0; i < result.Length; i++)
                if (result[i] != null)
                    compacted[j++] = result[i];

            return (compacted, survivingObjects);
        }

        private static bool IsHitObjectLine(string line, out int time)
        {
            time = 0;

            if (string.IsNullOrWhiteSpace(line))
                return false;
            if (line.TrimStart().StartsWith("//"))
                return false;

            string[] fields = line.Split(',');
            if (fields.Length < 3)
                return false;

            return int.TryParse(fields[2], out time);
        }

        internal static int TrimOsuFile(string osuPath, int startMs, int endMs)
        {
            string[] lines = File.ReadAllLines(osuPath);
            var (trimmed, survivingObjects) = TrimHitObjectLines(lines, startMs, endMs);
            File.WriteAllLines(osuPath, trimmed);
            return survivingObjects;
        }
    }
}
