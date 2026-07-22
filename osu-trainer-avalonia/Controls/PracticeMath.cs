using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace osu_trainer_avalonia.Controls
{
    /// <summary>
    /// Pure, UI-free helpers for the practice-cut timestamp fields and rate-ladder
    /// expansion. Counterpart to <see cref="DifficultyMath"/> — no UI types, no I/O.
    /// </summary>
    public static class PracticeMath
    {
        private static readonly Regex TimestampPattern =
            new Regex(@"^(\d{1,2}):([0-5]\d)(\.\d{1,3})?$", RegexOptions.Compiled);

        /// <summary>
        /// Parses "m:ss", "mm:ss", "m:ss.mmm" or "mm:ss.mmm" into milliseconds. Rejects a
        /// leading sign, a missing colon, empty/whitespace text, and anything else.
        /// </summary>
        public static bool TryParseTimestamp(string? text, out int ms)
        {
            ms = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var match = TimestampPattern.Match(text.Trim());
            if (!match.Success)
                return false;

            int minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            int seconds = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            int millis = 0;
            if (match.Groups[3].Success)
            {
                string fraction = match.Groups[3].Value.Substring(1).PadRight(3, '0');
                millis = int.Parse(fraction, CultureInfo.InvariantCulture);
            }

            ms = ((minutes * 60) + seconds) * 1000 + millis;
            return true;
        }

        /// <summary>
        /// Expands the inclusive ladder from..to by step. Returns an empty list when
        /// step is not positive or from exceeds to. "to" is included when it lands
        /// within 1e-9 of a step.
        /// </summary>
        public static IReadOnlyList<decimal> BuildLadder(decimal from, decimal to, decimal step)
        {
            var ladder = new List<decimal>();
            if (step <= 0 || from > to)
                return ladder;

            const decimal tolerance = 0.000000001m;
            decimal value = from;
            while (value <= to + tolerance)
            {
                ladder.Add(value);
                value += step;
            }

            return ladder;
        }

        /// <summary>Formats a range as "1m30-2m00" — minutes unpadded, seconds zero-padded.</summary>
        public static string FormatRangeSuffix(int startMs, int endMs) =>
            $"{FormatMinSec(startMs)}-{FormatMinSec(endMs)}";

        /// <summary>
        /// Validates the three rate-ladder text fields against the Spec's rule-12 refusal
        /// table and, on success, expands them into the ladder. Every failure sets the
        /// exact refusal message the row calls for.
        /// </summary>
        public static bool TryValidateLadder(string? fromText, string? toText, string? stepText, out IReadOnlyList<decimal> ladder, out string error)
        {
            ladder = Array.Empty<decimal>();
            error = string.Empty;

            decimal.TryParse(fromText, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal from);
            decimal.TryParse(toText, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal to);
            decimal.TryParse(stepText, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal step);

            if (step <= 0)
            {
                error = "Rate ladder: step must be greater than 0.";
                return false;
            }
            if (from > to)
            {
                error = "Rate ladder: 'from' must not exceed 'to'.";
                return false;
            }

            var built = BuildLadder(from, to, step);
            bool anyOutOfBounds = false;
            foreach (var rate in built)
            {
                if (rate < 0.5m || rate > 2.0m)
                {
                    anyOutOfBounds = true;
                    break;
                }
            }
            if (built.Count == 0 || anyOutOfBounds)
            {
                error = "Rate ladder: rates must stay between 0.5x and 2.0x.";
                return false;
            }
            if (built.Count > 10)
            {
                error = "Rate ladder: at most 10 diffs at a time.";
                return false;
            }

            ladder = built;
            return true;
        }

        /// <summary>
        /// Validates the practice-cut start/end text fields against the Spec's rule-12
        /// refusal table. Both fields empty is not a refusal — it means "no cut" (Spec
        /// rule 1), so this returns true with a null range.
        /// </summary>
        public static bool TryValidatePracticeRange(string? startText, string? endText, out (int StartMs, int EndMs)? range, out string rangeSuffix, out string error)
        {
            range = null;
            rangeSuffix = string.Empty;
            error = string.Empty;

            string start = startText?.Trim() ?? string.Empty;
            string end = endText?.Trim() ?? string.Empty;
            bool hasStart = start.Length > 0;
            bool hasEnd = end.Length > 0;

            if (!hasStart && !hasEnd)
                return true;

            if (hasStart != hasEnd)
            {
                error = "Practice cut: fill in both start and end.";
                return false;
            }

            if (!TryParseTimestamp(start, out int startMs) || !TryParseTimestamp(end, out int endMs))
            {
                error = "Practice cut: bad timestamp.";
                return false;
            }
            if (startMs >= endMs)
            {
                error = "Practice cut: start must be before end.";
                return false;
            }

            range = (startMs, endMs);
            rangeSuffix = FormatRangeSuffix(startMs, endMs);
            return true;
        }

        private static string FormatMinSec(int ms)
        {
            int totalSeconds = ms / 1000;
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            return $"{minutes}m{seconds:00}";
        }
    }
}
