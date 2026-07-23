using System;
using System.IO;

namespace OsuTrainerCore
{
    /// <summary>
    /// Roots the bundled tool binaries and scratch temp files at fixed, launch-directory-
    /// independent locations. <see cref="DifficultyCalculator"/> and <see cref="SongSpeedChanger"/>
    /// previously resolved <c>binaries\*.exe</c> (and wrote GUID temp files) relative to
    /// <see cref="Environment.CurrentDirectory"/>, which only worked because the app was always
    /// launched from its own output directory — a shortcut with a different "Start in" breaks
    /// both. <see cref="AppContext.BaseDirectory"/> (not <c>Assembly.Location</c>, which is empty
    /// under single-file publish) always points at the exe's own directory.
    /// </summary>
    internal static class ToolPaths
    {
        public static string Oppai => Path.Combine(AppContext.BaseDirectory, "binaries", "oppai.exe");

        public static string Soundstretch => Path.Combine(AppContext.BaseDirectory, "binaries", "soundstretch.exe");

        public static string NewTempFile(string extension) =>
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + extension);
    }
}
