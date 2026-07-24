using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using osu_trainer_avalonia.Services;

namespace osu_trainer_avalonia;

public partial class App : Application
{
    private MainWindow? mainWindow;
    private QuickSettingsWindow? quickSettingsWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The ✕ button hides MainWindow rather than closing it (see MainWindow's
            // Closing handler) so the app can keep running as a tray icon; the default
            // OnLastWindowClose shutdown mode would otherwise exit the process the moment
            // that happens.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Installed before MainWindow exists so a fault in its constructor is caught too;
            // the report action reads the field lazily, so it tolerates mainWindow still being null.
            CrashGuard.Install(msg => mainWindow?.ReportCrash(msg));

            mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnTrayIconClicked(object? sender, System.EventArgs e)
    {
        if (mainWindow == null) return;

        quickSettingsWindow ??= new QuickSettingsWindow(mainWindow.Editor);
        if (quickSettingsWindow.IsVisible)
            quickSettingsWindow.Hide();
        else
            quickSettingsWindow.ShowAtBottomRight();
    }

    private void OnTrayShowClick(object? sender, System.EventArgs e) => mainWindow?.RestoreFromTray();

    private void OnTrayQuitClick(object? sender, System.EventArgs e)
    {
        mainWindow?.PrepareForShutdown();
        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }
}
