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

        [Fact]
        public void SaveThenLoad_AllEighteenProperties_AreFieldIdentical()
        {
            var store = new SettingsStore(tempPath, _ => Assert.Fail("unexpected error"));
            var original = new AppSettings
            {
                BpmRate = 1.35M,
                BpmIsLocked = true,
                LockedBpm = 174,
                HpIsLocked = true,
                LockedHp = 4.5M,
                CsIsLocked = true,
                LockedCs = 3.9M,
                ArIsLocked = true,
                LockedAr = 9.1M,
                OdIsLocked = true,
                LockedOd = 7.2M,
                ScaleAR = false,
                ScaleOD = false,
                ForceHardrockCirclesize = true,
                ChangePitch = true,
                NoSpinners = true,
                HighQualityMp3s = true,
                UpdatesCheckEnabled = false,
            };

            store.Save(original);
            var loaded = store.Load();

            Assert.Equal(original.BpmRate, loaded.BpmRate);
            Assert.Equal(original.BpmIsLocked, loaded.BpmIsLocked);
            Assert.Equal(original.LockedBpm, loaded.LockedBpm);
            Assert.Equal(original.HpIsLocked, loaded.HpIsLocked);
            Assert.Equal(original.LockedHp, loaded.LockedHp);
            Assert.Equal(original.CsIsLocked, loaded.CsIsLocked);
            Assert.Equal(original.LockedCs, loaded.LockedCs);
            Assert.Equal(original.ArIsLocked, loaded.ArIsLocked);
            Assert.Equal(original.LockedAr, loaded.LockedAr);
            Assert.Equal(original.OdIsLocked, loaded.OdIsLocked);
            Assert.Equal(original.LockedOd, loaded.LockedOd);
            Assert.Equal(original.ScaleAR, loaded.ScaleAR);
            Assert.Equal(original.ScaleOD, loaded.ScaleOD);
            Assert.Equal(original.ForceHardrockCirclesize, loaded.ForceHardrockCirclesize);
            Assert.Equal(original.ChangePitch, loaded.ChangePitch);
            Assert.Equal(original.NoSpinners, loaded.NoSpinners);
            Assert.Equal(original.HighQualityMp3s, loaded.HighQualityMp3s);
            Assert.Equal(original.UpdatesCheckEnabled, loaded.UpdatesCheckEnabled);
        }

        [Fact]
        public void Load_PreExistingReflectionEraJson_StillDeserializes()
        {
            // Hand-written, matching the shape JsonSerializer.Serialize(AppSettings) produced
            // before the source-gen switch — the JSON shape is unchanged, only the serializer
            // metadata source changed, so an on-disk file from an older build must still load.
            const string reflectionEraJson = """
                {
                  "BpmRate": 1.2,
                  "BpmIsLocked": true,
                  "LockedBpm": 180,
                  "HpIsLocked": false,
                  "LockedHp": 0,
                  "CsIsLocked": false,
                  "LockedCs": 0,
                  "ArIsLocked": false,
                  "LockedAr": 0,
                  "OdIsLocked": false,
                  "LockedOd": 0,
                  "ScaleAR": true,
                  "ScaleOD": true,
                  "ForceHardrockCirclesize": false,
                  "ChangePitch": false,
                  "NoSpinners": false,
                  "HighQualityMp3s": false,
                  "UpdatesCheckEnabled": true
                }
                """;
            File.WriteAllText(tempPath, reflectionEraJson);
            var store = new SettingsStore(tempPath, _ => Assert.Fail("unexpected error"));

            var loaded = store.Load();

            Assert.Equal(1.2M, loaded.BpmRate);
            Assert.True(loaded.BpmIsLocked);
            Assert.Equal(180, loaded.LockedBpm);
        }
    }
}
