using Avalonia;
using System;

namespace osu_trainer_avalonia;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    // No .WithInterFont(): both windows set Quicksand explicitly, so Inter was never drawn —
    // it only cost a 2.1MB embedded font to register at startup. Anything Quicksand lacks
    // (CJK song titles) falls back to the platform font manager either way; Inter has no CJK.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
