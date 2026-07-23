using System;
using System.IO;
using OsuTrainerCore;
using Xunit;

namespace osu_trainer_avalonia.Tests
{
    public class ToolPathsTests : IDisposable
    {
        private readonly string originalCwd = Environment.CurrentDirectory;

        public void Dispose() => Environment.CurrentDirectory = originalCwd;

        [Fact]
        public void Oppai_IsRootedAtBaseDirectory_EvenAfterCwdChanges()
        {
            Environment.CurrentDirectory = Path.GetTempPath();

            Assert.Equal(
                Path.Combine(AppContext.BaseDirectory, "binaries", "oppai.exe"),
                ToolPaths.Oppai);
        }

        [Fact]
        public void Soundstretch_IsRootedAtBaseDirectory_EvenAfterCwdChanges()
        {
            Environment.CurrentDirectory = Path.GetTempPath();

            Assert.Equal(
                Path.Combine(AppContext.BaseDirectory, "binaries", "soundstretch.exe"),
                ToolPaths.Soundstretch);
        }

        [Fact]
        public void NewTempFile_IsUnderSystemTempPath_WithGivenExtension()
        {
            string path = ToolPaths.NewTempFile(".wav");

            Assert.StartsWith(Path.GetTempPath(), path);
            Assert.EndsWith(".wav", path);
        }

        [Fact]
        public void NewTempFile_TwoCalls_NeverCollide()
        {
            string a = ToolPaths.NewTempFile(".wav");
            string b = ToolPaths.NewTempFile(".wav");

            Assert.NotEqual(a, b);
        }
    }
}
