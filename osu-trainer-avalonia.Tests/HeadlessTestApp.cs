using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;

[assembly: AvaloniaTestApplication(typeof(osu_trainer_avalonia.Tests.HeadlessTestApp))]

namespace osu_trainer_avalonia.Tests
{
    /// <summary>Minimal headless Avalonia platform for tests that construct real controls
    /// (e.g. <see cref="AppIcon"/>'s <c>Geometry.Parse</c>, which needs a render interface
    /// even without a visible window).</summary>
    public class HeadlessTestApp
    {
        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<Application>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
