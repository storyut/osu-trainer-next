using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using osu_trainer_avalonia.Controls;
using osu_trainer_avalonia.Interop;
using osu_trainer_avalonia.Services;
using OsuTrainerCore;

namespace osu_trainer_avalonia
{
    public partial class MainWindow : Window
    {
        private readonly BeatmapEditor editor;
        private readonly LiveMapWatcher liveMapWatcher;
        private readonly SettingsStore settingsStore;
        private readonly GlobalHotKey globalHotKey;
        private readonly Button[] profileButtons = new Button[4];
        private readonly Button[] profileMenuButtons = new Button[4];
        private readonly DifficultyPanelControls difficultyControls;
        private bool updatingFromModel;
        private AppSettings? pendingPersistedSettings;
        private string? pendingUpdateUrl;
        private (decimal Below, decimal Above) ladderPreset = (0.10m, 0.10m);
        private decimal ladderStep = 0.05m;

        /// <summary>Shared with <see cref="QuickSettingsWindow"/> so both windows drive the same model.</summary>
        public BeatmapEditor Editor => editor;

        public MainWindow()
        {
            InitializeComponent();

            var host = new AvaloniaCoreHost(msg => StatusText.Text = msg);
            editor = new BeatmapEditor(host);
            editor.BeatmapSwitched += (_, _) => { UpdateBeatmapCardArt(); RefreshControlsFromModel(); };
            editor.BeatmapModified += (_, _) => RefreshControlsFromModel();
            editor.ControlsModified += (_, _) => RefreshControlsFromModel();
            editor.StateChanged += (_, _) => RefreshState();
            editor.BeatmapSwitched += OnFirstBeatmapSwitchedApplyPersistedSettings;

            difficultyControls = new DifficultyPanelControls(HpRow, CsRow, ArRow, OdRow, RateSlider, RateBpmText);
            DifficultyPanel.Wire(difficultyControls, editor, () => updatingFromModel);

            BuildProfileSlots();

            RefreshControlsFromModel();

            settingsStore = new SettingsStore(msg => StatusText.Text = msg);
            var loadedSettings = settingsStore.Load();
            ApplyPersistedSettingsPreLoad(loadedSettings);
            pendingPersistedSettings = loadedSettings;

            // Deferred to Opened: the check's first HTTPS request drags the whole HTTP + TLS +
            // JSON stack through the JIT (measured 170-420ms of threadpool CPU), which on this
            // CPU-bound startup path competes with the UI thread and delays first paint. It is
            // fire-and-forget either way, so nothing depends on it having started by then.
            if (loadedSettings.UpdatesCheckEnabled)
                Opened += StartUpdateCheckOnce;

            globalHotKey = new GlobalHotKey();
            globalHotKey.RateNudgeRequested += OnRateNudgeRequested;
            globalHotKey.Start();

            liveMapWatcher = new LiveMapWatcher(host);
            liveMapWatcher.SongsFolderDetected += (_, folder) => OsuTrainerCore.JunUtils.SongsFolder = folder;
            liveMapWatcher.BeatmapDetected += (_, path) => editor.RequestBeatmapLoad(path);
            liveMapWatcher.Start();

            Closed += (_, _) => liveMapWatcher.Stop();
            Closing += (_, e) =>
            {
                SaveCurrentSettings();
                e.Cancel = true;
                Hide();
            };
        }

        /// <summary>Saves settings and releases the global hotkey. Called once, from the tray "Quit" path, before the process actually exits.</summary>
        public void PrepareForShutdown()
        {
            SaveCurrentSettings();
            globalHotKey.Dispose();
        }

        public void RestoreFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        // ---- window chrome ----------------------------------------------------

        private void OnTitleBarPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        }

        private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

        // ---- loading ----------------------------------------------------------

        private CancellationTokenSource? batchCts;

        private async void OnGenerateClick(object? sender, RoutedEventArgs e)
        {
            if (batchCts != null)
            {
                batchCts.Cancel();
                return;
            }

            IReadOnlyList<decimal> ladder = LadderEnabledCheck.IsChecked == true
                ? PracticeMath.BuildAnchoredLadder(editor.BpmRate, ladderPreset.Below, ladderPreset.Above, ladderStep)
                : new[] { editor.BpmRate };
            if (!TryParsePracticeRange(out var range, out string rangeSuffix, out string rangeError))
            {
                StatusText.Text = rangeError;
                return;
            }

            batchCts = new CancellationTokenSource();
            GenerateButton.Content = "Cancel";
            RefreshState();

            var progress = new Progress<BeatmapEditor.BatchProgress>(p =>
                StatusText.Text = $"Generating {p.Completed}/{p.Total} — {p.Rate:0.00}x");

            try
            {
                var result = await editor.GenerateBatchAsync(ladder, range, rangeSuffix, progress, batchCts.Token);
                StatusText.Text = result switch
                {
                    BeatmapEditor.BatchResult.Cancelled => "Generation cancelled.",
                    BeatmapEditor.BatchResult.NoObjectsInRange => "Practice cut: no hit objects in that range.",
                    BeatmapEditor.BatchResult.Published => "Generated.",
                    _ => StatusText.Text, // Failed: message already set via ICoreHost.ShowError
                };
            }
            finally
            {
                batchCts.Dispose();
                batchCts = null;
                GenerateButton.Content = "Generate";
                RefreshState();
            }
        }

        private bool TryParsePracticeRange(out BeatmapEditor.PracticeRange? range, out string rangeSuffix, out string error)
        {
            bool ok = PracticeMath.TryValidatePracticeRange(PracticeStartBox.Text, PracticeEndBox.Text, out var tupleRange, out rangeSuffix, out error);
            range = tupleRange.HasValue ? new BeatmapEditor.PracticeRange(tupleRange.Value.StartMs, tupleRange.Value.EndMs) : null;
            return ok;
        }

        private void OnLadderEnabledClick(object? sender, RoutedEventArgs e)
        {
            bool on = LadderEnabledCheck.IsChecked == true;
            LadderPresetDown10.IsEnabled = LadderPresetUp10.IsEnabled = LadderPresetAround10.IsEnabled = LadderPresetUp20.IsEnabled = on;
            LadderStep005.IsEnabled = LadderStep010.IsEnabled = on;
            UpdateLadderPreview();
        }

        private void OnLadderPresetClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton clicked) return;

            var siblings = new[] { LadderPresetDown10, LadderPresetUp10, LadderPresetAround10, LadderPresetUp20 };
            foreach (var button in siblings)
                button.IsChecked = ReferenceEquals(button, clicked);

            var parts = ((string)clicked.Tag!).Split(',');
            ladderPreset = (decimal.Parse(parts[0], CultureInfo.InvariantCulture), decimal.Parse(parts[1], CultureInfo.InvariantCulture));
            UpdateLadderPreview();
        }

        private void OnLadderStepClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton clicked) return;

            var siblings = new[] { LadderStep005, LadderStep010 };
            foreach (var button in siblings)
                button.IsChecked = ReferenceEquals(button, clicked);

            ladderStep = decimal.Parse((string)clicked.Tag!, CultureInfo.InvariantCulture);
            UpdateLadderPreview();
        }

        private void UpdateLadderPreview()
        {
            bool on = LadderEnabledCheck.IsChecked == true;
            if (!on || editor.State != EditorState.READY)
            {
                LadderPreviewText.Text = string.Empty;
                return;
            }

            LadderPreviewText.Text = PracticeMath.FormatLadderPreview(editor.BpmRate, ladderPreset.Below, ladderPreset.Above, ladderStep);
        }

        private void OnResetClick(object? sender, RoutedEventArgs e)
        {
            editor.ResetBeatmap();
            RefreshControlsFromModel();
        }

        private async void OnSongsFolderClick(object? sender, RoutedEventArgs e)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose the osu! Songs folder",
                AllowMultiple = false
            });
            var path = folders.FirstOrDefault()?.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                OsuTrainerCore.JunUtils.SongsFolder = path;
                StatusText.Text = $"Songs folder: {path}";
            }
        }

        // ---- rate + BPM -------------------------------------------------------

        private void OnBpmLockClick(object? sender, RoutedEventArgs e)
        {
            if (updatingFromModel) return;
            editor.ToggleBpmLock();
        }

        /// <summary>Ctrl+Alt+Up/Down, delivered from <see cref="GlobalHotKey"/> on the UI thread.
        /// Drives the slider so it goes through the exact same path a mouse drag does.</summary>
        private void OnRateNudgeRequested(bool increase)
        {
            decimal delta = increase ? 0.05M : -0.05M;
            decimal newRate = Math.Clamp(editor.BpmRate + delta, 0.5M, 2.0M);
            RateSlider.Value = (double)newRate;
        }

        // ---- difficulty rows --------------------------------------------------

        private void OnScaleArClick(object? sender, RoutedEventArgs e)
        {
            if (updatingFromModel) return;
            editor.SetScaleAR(ScaleArCheck.IsChecked == true);
        }

        private void OnScaleOdClick(object? sender, RoutedEventArgs e)
        {
            if (updatingFromModel) return;
            editor.SetScaleOD(ScaleOdCheck.IsChecked == true);
        }

        private void OnHrClick(object? sender, RoutedEventArgs e)
        {
            if (updatingFromModel) return;
            editor.ToggleHrEmulation();
        }

        // ---- extras -----------------------------------------------------------

        private void OnToggleExtras(object? sender, RoutedEventArgs e)
        {
            bool show = !ExtrasPanel.IsVisible;
            ExtrasPanel.IsVisible = show;
            // The extras expand sideways: the left column keeps its designed 376px width and
            // the window grows to make room, rather than the stack growing taller.
            Width = show ? 720 : 400;
            MoreButtonIcon.Kind = show ? AppIconKind.ChevronDown : AppIconKind.ChevronRight;
            MoreButtonText.Text = show ? "Less" : "More";
        }

        private void OnNoSpinnersClick(object? sender, RoutedEventArgs e)
        {
            if (updatingFromModel) return;
            editor.ToggleNoSpinners();
        }

        private void OnChangePitchClick(object? sender, RoutedEventArgs e)
        {
            if (updatingFromModel) return;
            editor.ToggleChangePitchSetting();
        }

        private void OnHqMp3Click(object? sender, RoutedEventArgs e)
        {
            if (updatingFromModel) return;
            editor.ToggleHighQualityMp3s();
        }

        // ---- settings persistence -----------------------------------------------
        // BeatmapEditor.SetHP/SetCS/SetAR/SetOD only act while State == READY, and the
        // Toggle*Lock methods dereference NewBeatmap unconditionally — both assume a
        // beatmap is already loaded, same as the existing LoadProfile(int) does. So the
        // fields that only matter once a beatmap exists (the four difficulty locks, BPM
        // lock, HR emulation) are applied once, on the first BeatmapSwitched after
        // construction; everything else (rate, Scale AR/OD, pitch/spinners/mp3 toggles) is
        // safe to apply immediately since those setters tolerate the NOT_READY state. The
        // field mapping and this split live in Services.SettingsSnapshot.

        private void ApplyPersistedSettingsPreLoad(AppSettings s)
        {
            SettingsSnapshot.ApplyImmediate(editor, s);
            UpdatesCheck.IsChecked = s.UpdatesCheckEnabled;

            // No beatmap is loaded yet, so RefreshControlsFromModel() (which reads
            // editor.NewBeatmap) can't run — but the rate slider isn't tied to a beatmap and
            // should reflect the restored rate immediately, not just once one loads.
            updatingFromModel = true;
            RateSlider.Value = (double)s.BpmRate;
            updatingFromModel = false;
        }

        private void OnFirstBeatmapSwitchedApplyPersistedSettings(object? sender, EventArgs e)
        {
            if (pendingPersistedSettings == null || editor.State != EditorState.READY)
                return;

            var s = pendingPersistedSettings;
            pendingPersistedSettings = null;
            editor.BeatmapSwitched -= OnFirstBeatmapSwitchedApplyPersistedSettings;

            SettingsSnapshot.ApplyOnReady(editor, s);
        }

        private void SaveCurrentSettings()
        {
            var s = SettingsSnapshot.Capture(editor, UpdatesCheck.IsChecked == true);
            settingsStore.Save(s);
        }

        // ---- update checker -------------------------------------------------------

        /// <summary>One-shot: <see cref="Window.Opened"/> fires again on every Show() after the
        /// ✕ button hides this window to the tray, and the update check should run once per
        /// process, not once per restore.</summary>
        private void StartUpdateCheckOnce(object? sender, EventArgs e)
        {
            Opened -= StartUpdateCheckOnce;
            _ = CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            var info = await UpdateChecker.CheckForUpdateAsync();
            if (info == null)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                pendingUpdateUrl = info.HtmlUrl;
                UpdateRowText.Text = $"Update available: {info.Version}";
                UpdateRow.IsVisible = true;
            });
        }

        private void OnUpdatesCheckClick(object? sender, RoutedEventArgs e) => SaveCurrentSettings();

        private void OnUpdateRowClick(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(pendingUpdateUrl)) return;
            Process.Start(new ProcessStartInfo(pendingUpdateUrl) { UseShellExecute = true });
        }

        private void OnDismissUpdateRowClick(object? sender, RoutedEventArgs e) => UpdateRow.IsVisible = false;

        // ---- profiles ---------------------------------------------------------

        private void BuildProfileSlots()
        {
            editor.LoadProfilesFromDisk();
            for (int i = 0; i < profileButtons.Length; i++)
            {
                int idx = i;

                var loadButton = new Button
                {
                    Content = new TextBlock
                    {
                        Text = editor.UserProfiles[idx].Name,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        TextAlignment = Avalonia.Media.TextAlignment.Center
                    },
                    Width = 48,
                    FontSize = 10,
                    Padding = new Thickness(2, 3),
                    MinHeight = 0,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Background = Brushes.Transparent,
                    [!TemplatedControl.ForegroundProperty] = this[!ForegroundProperty]
                };
                loadButton.SetValue(AutomationProperties.AutomationIdProperty, $"ProfileButton{idx}");
                loadButton.Click += (_, _) => { editor.LoadProfile(idx); RefreshControlsFromModel(); };
                profileButtons[idx] = loadButton;

                var saveButton = new Button { Content = "Save current" };
                saveButton.SetValue(AutomationProperties.AutomationIdProperty, $"ProfileSave{idx}");
                var renameBox = new TextBox { Text = editor.UserProfiles[idx].Name, Width = 150, Watermark = "Rename" };
                renameBox.SetValue(AutomationProperties.AutomationIdProperty, $"ProfileRename{idx}");
                var flyout = new Flyout
                {
                    Content = new StackPanel
                    {
                        Spacing = 6,
                        Children = { saveButton, renameBox }
                    }
                };

                saveButton.Click += (_, _) =>
                {
                    editor.SaveProfile(idx);
                    editor.SaveProfilesToDisk();
                    flyout.Hide();
                };
                renameBox.KeyDown += (_, ke) =>
                {
                    if (ke.Key != Key.Enter) return;
                    editor.RenameProfile(idx, string.IsNullOrWhiteSpace(renameBox.Text) ? editor.UserProfiles[idx].Name : renameBox.Text.Trim());
                    editor.SaveProfilesToDisk();
                    ((TextBlock)profileButtons[idx].Content!).Text = editor.UserProfiles[idx].Name;
                    flyout.Hide();
                };

                var menuButton = new Button
                {
                    Content = new AppIcon
                    {
                        Kind = AppIconKind.ChevronDown,
                        Size = 12,
                        Stroke = Brushes.White
                    },
                    Padding = new Thickness(2, 3),
                    MinHeight = 0,
                    Background = Brushes.Transparent,
                    Flyout = flyout
                };
                menuButton.SetValue(AutomationProperties.AutomationIdProperty, $"ProfileMenu{idx}");
                profileMenuButtons[idx] = menuButton;

                ProfilesPanel.Children.Add(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children = { loadButton, menuButton }
                });
            }
        }

        // ---- state / refresh --------------------------------------------------

        private bool CanClickGenerate => editor.State == EditorState.READY || batchCts != null;

        /// <summary>
        /// The StateChanged path. Deliberately *not* RefreshControlsFromModel(): that rewrites
        /// StatusText to "Loaded." and would clobber the live "Generating n/n" progress line.
        /// It still re-applies the panel, so rows greyed when an export starts come back when
        /// it finishes.
        /// </summary>
        private void RefreshState()
        {
            GenerateButton.IsEnabled = CanClickGenerate;

            updatingFromModel = true;
            DifficultyPanel.Apply(difficultyControls, DifficultyPanel.Project(editor));
            updatingFromModel = false;
        }

        private void RefreshControlsFromModel()
        {
            var panelState = DifficultyPanel.Project(editor);

            if (editor.State == EditorState.NOT_READY || editor.NewBeatmap == null)
            {
                string reason = editor.NotReadyReason switch
                {
                    BadBeatmapReason.ERROR_LOADING_BEATMAP => "Could not load that .osu file.",
                    BadBeatmapReason.EMPTY_MAP => "That beatmap has no hit objects.",
                    _ => "No beatmap loaded."
                };
                SongTitle.Text = reason;
                SongArtist.Text = string.Empty;
                SongDifficulty.Text = string.Empty;
                // Apply() resets the readout to "—"; without it the previous map's BPM line stays.
                DifficultyPanel.Apply(difficultyControls, panelState);
                BpmRangeText.Text = string.Empty;
                StatusText.Text = reason;
                GenerateButton.IsEnabled = false;
                HrCsCheck.IsEnabled = false;
                foreach (var b in profileButtons) b.IsEnabled = false;
                foreach (var b in profileMenuButtons) b.IsEnabled = false;
                UpdateLadderPreview();
                return;
            }

            updatingFromModel = true;

            DifficultyPanel.Apply(difficultyControls, panelState);

            ScaleArCheck.IsChecked = editor.ScaleAR;
            ScaleOdCheck.IsChecked = editor.ScaleOD;
            HrCsCheck.IsChecked = editor.ForceHardrockCirclesize;

            var (_, newMin, newMax) = editor.GetNewBpmData();
            BpmRangeText.Text = DifficultyMath.FormatBpmRange(newMin, newMax);
            BpmLockButton.IsChecked = editor.BpmIsLocked;

            NoSpinnersCheck.IsChecked = editor.NoSpinners;
            ChangePitchCheck.IsChecked = editor.ChangePitch;
            HqMp3Check.IsChecked = editor.HighQualityMp3s;

            StarRatingDiamond.Stars = (double)editor.StarRating;

            var map = editor.OriginalBeatmap;
            SongTitle.Text = string.IsNullOrWhiteSpace(map.Title) ? "(untitled)" : map.Title;
            SongArtist.Text = map.Artist;
            SongDifficulty.Text = map.Version;
            StatusText.Text = "Loaded.";

            GenerateButton.IsEnabled = CanClickGenerate;
            HrCsCheck.IsEnabled = true;
            foreach (var b in profileButtons) b.IsEnabled = true;
            foreach (var b in profileMenuButtons) b.IsEnabled = true;

            updatingFromModel = false;

            UpdateLadderPreview();
        }

        /// <summary>
        /// Paints the beatmap card's artwork. Every failure path hides the art rectangle,
        /// which reveals the card's flat CardBackground — that empty card *is* the intended
        /// fallback, so the catch below is a deliberate outcome, not a swallowed error.
        /// </summary>
        private void UpdateBeatmapCardArt()
        {
            try
            {
                if (editor.OriginalBeatmap == null || string.IsNullOrEmpty(editor.OriginalBeatmap.Background))
                {
                    BeatmapCardArt.IsVisible = false;
                    return;
                }

                string bgPath = Path.Combine(OsuTrainerCore.JunUtils.GetBeatmapDirectoryName(editor.OriginalBeatmap), editor.OriginalBeatmap.Background);
                if (!File.Exists(bgPath))
                {
                    BeatmapCardArt.IsVisible = false;
                    return;
                }

                using var stream = File.OpenRead(bgPath);
                var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
                BeatmapCardArt.Fill = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                BeatmapCardArt.IsVisible = true;

                // IsVisible flips false->true don't get an automatic re-layout on this
                // Avalonia version, so the shape is otherwise left arranged at its stale
                // (0-size) bounds and never paints.
                BeatmapCardArt.InvalidateMeasure();
                BeatmapCardArt.InvalidateArrange();
            }
            catch
            {
                // Unreadable/corrupt image: fall back to the flat card.
                BeatmapCardArt.IsVisible = false;
            }
        }
    }
}
