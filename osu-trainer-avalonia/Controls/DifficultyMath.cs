using System;
using System.Globalization;

namespace osu_trainer_avalonia.Controls
{
    /// <summary>
    /// Pure, UI-free helpers for numeric parsing/formatting used by the difficulty rows
    /// and the BPM readout. Kept separate from any Avalonia control so it is trivially
    /// unit-testable without spinning up the Avalonia runtime.
    /// </summary>
    public static class DifficultyMath
    {
        /// <summary>
        /// Parses a user-typed value and clamps it into [min, max]. Returns null when the
        /// text cannot be parsed as a number, so callers can revert to the model value.
        /// </summary>
        public static double? ParseClamp(string? text, double min, double max)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return null;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>Formats a difficulty value (AR/CS/OD/HP) with up to one decimal place.</summary>
        public static string FormatDifficulty(double value) =>
            value.ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>Formats the main BPM readout line: "180 → 243" (values rounded to int).</summary>
        public static string FormatBpm(decimal originalBpm, decimal newBpm) =>
            $"{Round(originalBpm)} → {Round(newBpm)}";

        /// <summary>
        /// Formats a "(min – max)" range for a variable-BPM map, or an empty string when
        /// the map has a single BPM (min == max) or no data (max == 0).
        /// </summary>
        public static string FormatBpmRange(decimal min, decimal max)
        {
            if (max == 0 || Round(min) == Round(max))
                return string.Empty;
            return $"({Round(min)} – {Round(max)})";
        }

        private static int Round(decimal value) =>
            (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
