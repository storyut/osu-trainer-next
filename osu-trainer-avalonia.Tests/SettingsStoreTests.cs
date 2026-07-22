using System;
using System.IO;
using osu_trainer_avalonia.Services;
using Xunit;

namespace osu_trainer_avalonia.Tests
{
    public class SettingsStoreTests : IDisposable
    {
        private readonly string tempPath = Path.Combine(Path.GetTempPath(), $"osu-trainer-next-tests-{Guid.NewGuid():N}.json");

        public void Dispose()
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }

        [Fact]
        public void Load_MissingFile_ReturnsDefaultsWithoutError()
        {
            bool errored = false;
            var store = new SettingsStore(tempPath, _ => errored = true);

            var settings = store.Load();

            Assert.False(errored);
            Assert.Equal(1.0M, settings.BpmRate);
            Assert.True(settings.ScaleAR);
            Assert.True(settings.UpdatesCheckEnabled);
        }

        [Fact]
        public void Load_CorruptFile_ReturnsDefaultsAndReportsError()
        {
            File.WriteAllText(tempPath, "{ not valid json");
            string? reported = null;
            var store = new SettingsStore(tempPath, msg => reported = msg);

            var settings = store.Load();

            Assert.NotNull(reported);
            Assert.Equal(1.0M, settings.BpmRate);
        }

        [Fact]
        public void SaveThenLoad_RoundTripsValues()
        {
            var store = new SettingsStore(tempPath, _ => Assert.Fail("unexpected error"));
            var original = new AppSettings
            {
                BpmRate = 1.5M,
                CsIsLocked = true,
                LockedCs = 3.9M,
                ForceHardrockCirclesize = false,
                UpdatesCheckEnabled = false
            };

            store.Save(original);
            var loaded = store.Load();

            Assert.Equal(original.BpmRate, loaded.BpmRate);
            Assert.Equal(original.CsIsLocked, loaded.CsIsLocked);
            Assert.Equal(original.LockedCs, loaded.LockedCs);
            Assert.Equal(original.UpdatesCheckEnabled, loaded.UpdatesCheckEnabled);
        }
    }
}
