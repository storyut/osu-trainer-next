using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace osu_trainer_avalonia.Services
{
    /// <summary>
    /// Last-resort net: converts an unhandled exception into a recovered, logged error
    /// instead of a silent process death. Not a substitute for guarding the actual bug —
    /// it exists to catch whatever an audit missed.
    /// </summary>
    internal static class CrashGuard
    {
        internal sealed class ReentryGate
        {
            private int entered;
            public bool TryEnter() => Interlocked.Exchange(ref entered, 1) == 0;
            public void Exit() => Interlocked.Exchange(ref entered, 0);
        }

        private static readonly ReentryGate gate = new();

        public static string LogPath => Path.Combine(Path.GetTempPath(), "osu-trainer-next-crash.log");

        public static string FormatEntry(Exception ex, DateTime timestamp) =>
            $"[{timestamp:yyyy-MM-dd HH:mm:ss}] {ex.GetType().FullName}: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}";

        public static void AppendLog(string path, string entry) => File.AppendAllText(path, entry);

        public static void Install(Action<string> report)
        {
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                Handle(e.Exception, report);
                e.Handled = true;
            };

            // Not recoverable: the CLR tears down after this handler returns. Logged so the
            // fault leaves evidence, but there is no "e.Handled" to set here.
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    Handle(ex, report);
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Handle(e.Exception, report);
                e.SetObserved();
            };
        }

        private static void Handle(Exception ex, Action<string> report)
        {
            if (!gate.TryEnter())
                return; // a fault inside this handler must not recurse

            try
            {
                var entry = FormatEntry(ex, DateTime.Now);
                try
                {
                    AppendLog(LogPath, entry);
                }
                catch
                {
                    // The log write itself failing has no further recovery path to report to —
                    // this is the last-resort handler; there is nothing underneath it.
                }

                Dispatcher.UIThread.Post(() => report?.Invoke($"{ex.GetType().Name}: {ex.Message}"));
            }
            finally
            {
                gate.Exit();
            }
        }
    }
}
