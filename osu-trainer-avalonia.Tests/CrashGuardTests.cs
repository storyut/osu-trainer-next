using System;
using System.IO;
using osu_trainer_avalonia.Services;
using Xunit;

namespace osu_trainer_avalonia.Tests
{
    public class CrashGuardTests
    {
        [Fact]
        public void FormatEntry_ContainsTypeMessageStackAndTimestamp()
        {
            Exception ex;
            try { throw new InvalidOperationException("boom"); }
            catch (Exception caught) { ex = caught; }

            var timestamp = new DateTime(2026, 7, 24, 10, 30, 0);
            var entry = CrashGuard.FormatEntry(ex, timestamp);

            Assert.Contains(nameof(InvalidOperationException), entry);
            Assert.Contains("boom", entry);
            Assert.Contains(nameof(CrashGuardTests) + "." + nameof(FormatEntry_ContainsTypeMessageStackAndTimestamp), entry);
            Assert.Contains("2026-07-24 10:30:00", entry);
        }

        [Fact]
        public void AppendLog_SecondCall_AppendsRatherThanTruncates()
        {
            var path = Path.Combine(Path.GetTempPath(), $"crashguard-test-{Guid.NewGuid()}.log");
            try
            {
                CrashGuard.AppendLog(path, "first" + Environment.NewLine);
                CrashGuard.AppendLog(path, "second" + Environment.NewLine);

                var contents = File.ReadAllText(path);
                Assert.Contains("first", contents);
                Assert.Contains("second", contents);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void ReentryGate_SecondEnterWithoutExit_Fails()
        {
            var gate = new CrashGuard.ReentryGate();

            Assert.True(gate.TryEnter());
            Assert.False(gate.TryEnter());

            gate.Exit();

            Assert.True(gate.TryEnter());
        }
    }
}
