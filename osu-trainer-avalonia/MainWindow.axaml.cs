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
using LucideAvalonia;
using LucideAvalonia.Enum;
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
        private bool updatingFromModel;
        private AppSettings? pendingPersistedSettings;
        private string? pendingUpdateUrl;

        /// <summary>Shared with <see cref="QuickSettingsWindow"/> so both windows drive the same model.</summary>
        public BeatmapEditor Editor => editor;

        public MainWindow()
        {
            InitializeComponent();

            var host = new AvaloniaCoreHost(msg => StatusText.Text = msg);
            editor = new BeatmapEditor(host);
            editor.BeatmapSwitched += (_, _) => { UpdateHeroBackground(); RefreshControlsFromModel(); SeedPracticeLadderFields(); };
            editor.BeatmapModified += (_, _) => RefreshControlsFromModel();
            editor.ControlsModified += (_, _) => RefreshControlsFromModel();
            editor.StateChanged += (_, _) => RefreshState();
            editor.BeatmapSwitched += OnFirstBeatmapSwitchedApplyPersistedSettings;

            WireDifficultyRows();
            BuildProfileSlots();

            LadderFromBox.TextChanged += (_, _) => UpdateLadderCount();
            LadderToBox.TextChanged += (_, _) => UpdateLadderCount();
            LadderStepBox.TextChanged += (_, _) => UpdateLadderCount();

            UpdateRateBubble(RateSlider.Value);
            RefreshControlsFromModel();
            SeedPracticeLadderFields();

            settingsStore = new SettingsStore(msg => StatusText.Text = msg);
            var loadedSettings = settingsStore.Load();
            ApplyPersistedSettingsPreLoad(loadedSettings);
            pendingPersistedSettings = loadedSettings;

            if (loadedSettings.UpdatesCheckEnabled)
                _ = CheckForUpdatesAsync();

            globalHotKey = new GlobalHotKey();
            globalHotKey.RateNudgeRequested += OnRateNudgeRequested;
            globalHotKey.Start();

            liveMapWatcher = new LiveMapWatcher(host);
            liveMapWatcher.SongsFolderDetected += (_, folder) => OsuTrainerCore.JunUtils.SongsFolder = folder;
            liveMapWatcher.BeatmapDetected += (_, path) =>
            {
                PathTextBox.Text = path;
                editor.RequestBeatmapLoad(path);
            };
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

        private void OnLoadClick(object? sender, RoutedEventArgs e)
        {
            StatusText.Text = "Loading...";
            editor.RequestBeatmapLoad(PathTextBox.Text);
        }

        private CancellationTokenSource? batchCts;

        private async void OnGenerateClick(object? sender, RoutedEventArgs e)
        {
            if (batchCts != null)
            {
                batchCts.Cancel();
                return;
            }

            if (!TryParseLadder(out var ladder, out string ladderError))
            {
                StatusText.Text = ladderError;
                return;
            }
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

        private bool TryParseLadder(out IReadOnlyList<decimal> ladder, out string error) =>
            PracticeMath.TryValidateLadder(LadderFromBox.Text, LadderToBox.Text, LadderStepBox.Text, out ladder, out error);

        private bool TryParsePracticeRange(out BeatmapEditor.PracticeRange? range, out string rangeSuffix, out string error)
        {
            bool ok = PracticeMath.TryValidatePracticeRange(PracticeStartBox.Text, PracticeEndBox.Text, out var tupleRange, out rangeSuffix, out error);
            range = tupleRange.HasValue ? new BeatmapEditor.PracticeRange(tupleRange.Value.StartMs, tupleRange.Value.EndMs) : null;
            return ok;
        }

        private void SeedPracticeLadderFields()
        {
            string rateText = editor.BpmRate.ToString("0.00", CultureInfo.InvariantCulture);
            LadderFromBox.Text = rateText;
            LadderToBox.Text = rateText;
            LadderStepBox.Text = "0.05";
            UpdateLadderCount();
        }

        private void UpdateLadderCount()
        {
            bool okFrom = decimal.TryParse(LadderFromBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal from);
            bool okTo = decimal.TryParse(LadderToBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal to);
            bool okStep = decimal.TryParse(LadderStepBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal step);

            if (!okFrom || !okTo || !okStep)
            {
                LadderCountText.Text = "—";
                return;
            }

            var built = PracticeMath.BuildLadder(from, to, step);
            LadderCountText.Text = built.Count == 0 ? "—" : $"{built.Count} diffs";
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

        private void OnRateChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            UpdateRateBubble(e.NewValue);
            if (updatingFromModel) return;
            editor.SetBpmMultiplier((decimal)e.NewValue);
        }

        private void UpdateRateBubble(double value)
        {
            const double min = 0.5, max = 2.0, trackWidth = 352, bubbleWidth = 48;
            double fraction = (value - min) / (max - min);
            double left = fraction * (trackWidth - bubbleWidth);
            RateBubble.Margin = new Thickness(left, -28, 0, 0);
            RateBubbleText.Text = $"{value:0.00}x";
        }

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

        private void WireDifficultyRows()
        {
            HpRow.ValueCommitted += (_, v) => { if (!updatingFromModel) editor.SetHP((decimal)v); };
            CsRow.ValueCommitted += (_, v) => { if (!updatingFromModel) editor.SetCS((decimal)v); };
            ArRow.ValueCommitted += (_, v) => { if (!updatingFromModel) editor.SetAR((decimal)v); };
            OdRow.ValueCommitted += (_, v) => { if (!updatingFromModel) editor.SetOD((decimal)v); };

            HpRow.LockToggled += (_, _) => editor.ToggleHpLock();
            CsRow.LockToggled += (_, _) => editor.ToggleCsLock();
            ArRow.LockToggled += (_, _) => editor.ToggleArLock();
            OdRow.LockToggled += (_, _) => editor.ToggleOdLock();
        }

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
            MoreButtonIcon.Icon = show ? LucideIconNames.ChevronDown : LucideIconNames.ChevronRight;
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
        // safe to apply immediately since those setters tolerate the NOT_READY state.

        private void ApplyPersistedSettingsPreLoad(AppSettings s)
        {
            editor.BpmRate = s.BpmRate;
            editor.SetScaleAR(s.ScaleAR);
            editor.SetScaleOD(s.ScaleOD);
            if (s.ChangePitch) editor.ToggleChangePitchSetting();
            if (s.NoSpinners) editor.ToggleNoSpinners();
            if (s.HighQualityMp3s) editor.ToggleHighQualityMp3s();
            UpdatesCheck.IsChecked = s.UpdatesCheckEnabled;

            // No beatmap is loaded yet, so RefreshControlsFromModel() (which reads
            // editor.NewBeatmap) can't run — but the rate slider/bubble aren't tied to a
            // beatmap and should reflect the restored rate immediately, not just once one loads.
            updatingFromModel = true;
            RateSlider.Value = (double)s.BpmRate;
            UpdateRateBubble((double)s.BpmRate);
            updatingFromModel = false;
        }

        private void OnFirstBeatmapSwitchedApplyPersistedSettings(object? sender, EventArgs e)
        {
            if (pendingPersistedSettings == null || editor.State != EditorState.READY)
                return;

            var s = pendingPersistedSettings;
            pendingPersistedSettings = null;
            editor.BeatmapSwitched -= OnFirstBeatmapSwitchedApplyPersistedSettings;

            // HR emulation and a CS lock are mutually exclusive in BeatmapEditor already
            // (each Toggle clears the other), so only one branch here ever applies.
            if (s.ForceHardrockCirclesize)
            {
                editor.ToggleHrEmulation();
            }
            else if (s.CsIsLocked)
            {
                editor.ToggleCsLock();
                editor.SetCS(s.LockedCs);
            }

            if (s.HpIsLocked)
            {
                editor.ToggleHpLock();
                editor.SetHP(s.LockedHp);
            }

            if (s.ArIsLocked)
            {
                editor.ToggleArLock();
                editor.SetAR(s.LockedAr);
            }

            if (s.OdIsLocked)
            {
                editor.ToggleOdLock();
                editor.SetOD(s.LockedOd);
            }

            if (s.BpmIsLocked)
            {
                editor.ToggleBpmLock();
                editor.SetBpm(s.LockedBpm);
            }
        }

        private void SaveCurrentSettings()
        {
            var s = new AppSettings
            {
                BpmRate = editor.BpmRate,
                BpmIsLocked = editor.BpmIsLocked,
                LockedBpm = editor.BpmIsLocked ? (int)editor.GetNewBpmData().Item1 : 200,
                HpIsLocked = editor.HpIsLocked,
                LockedHp = editor.NewBeatmap?.HPDrainRate ?? 0M,
                CsIsLocked = editor.CsIsLocked,
                LockedCs = editor.NewBeatmap?.CircleSize ?? 0M,
                ArIsLocked = editor.ArIsLocked,
                LockedAr = editor.NewBeatmap?.ApproachRate ?? 0M,
                OdIsLocked = editor.OdIsLocked,
                LockedOd = editor.NewBeatmap?.OverallDifficulty ?? 0M,
                ScaleAR = editor.ScaleAR,
                ScaleOD = editor.ScaleOD,
                ForceHardrockCirclesize = editor.ForceHardrockCirclesize,
                ChangePitch = editor.ChangePitch,
                NoSpinners = editor.NoSpinners,
                HighQualityMp3s = editor.HighQualityMp3s,
                UpdatesCheckEnabled = UpdatesCheck.IsChecked == true
            };
            settingsStore.Save(s);
        }

        // ---- update checker -------------------------------------------------------

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
                    Content = new Lucide
                    {
                        Icon = LucideIconNames.ChevronDown,
                        Width = 12,
                        Height = 12,
                        StrokeBrush = Brushes.White
                    },
                    Padding = new Thickness(2, 3),
                    MinHeight = 0,
                    Background = Brushes.Transparent,
                    Flyout = flyout
                };
                menuButton.SetValue(AutomationProperties.AutomationIdProperty, $"ProfileMenu{idx}");

                ProfilesPanel.Children.Add(new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Children = { loadButton, menuButton }
                });
            }
        }

        // ---- state / refresh --------------------------------------------------

        private bool CanClickGenerate => editor.State == EditorState.READY || batchCts != null;

        private void RefreshState() => GenerateButton.IsEnabled = CanClickGenerate;

        private void RefreshControlsFromModel()
        {
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
                StatusText.Text = reason;
                GenerateButton.IsEnabled = false;
                HpRow.IsEnabled = CsRow.IsEnabled = ArRow.IsEnabled = OdRow.IsEnabled = HrCsCheck.IsEnabled = false;
                return;
            }

            updatingFromModel = true;

            RateSlider.Value = (double)editor.BpmRate;
            UpdateRateBubble((double)editor.BpmRate);

            HpRow.Value = (double)editor.NewBeatmap.HPDrainRate;
            CsRow.Value = (double)editor.NewBeatmap.CircleSize;
            ArRow.Value = (double)editor.NewBeatmap.ApproachRate;
            OdRow.Value = (double)editor.NewBeatmap.OverallDifficulty;

            HpRow.IsLocked = editor.HpIsLocked;
            CsRow.IsLocked = editor.CsIsLocked;
            ArRow.IsLocked = editor.ArIsLocked;
            OdRow.IsLocked = editor.OdIsLocked;

            ScaleArCheck.IsChecked = editor.ScaleAR;
            ScaleOdCheck.IsChecked = editor.ScaleOD;
            HrCsCheck.IsChecked = editor.ForceHardrockCirclesize;

            var (origBpm, _, _) = editor.GetOriginalBpmData();
            var (newBpm, newMin, newMax) = editor.GetNewBpmData();
            BpmText.Text = DifficultyMath.FormatBpm(origBpm, newBpm);
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
            HpRow.IsEnabled = CsRow.IsEnabled = ArRow.IsEnabled = OdRow.IsEnabled = HrCsCheck.IsEnabled = true;

            updatingFromModel = false;
        }

        private void UpdateHeroBackground()
        {
            try
            {
                if (editor.OriginalBeatmap == null || string.IsNullOrEmpty(editor.OriginalBeatmap.Background))
                {
                    HeroBackgroundRect.IsVisible = false;
                    return;
                }

                string bgPath = Path.Combine(OsuTrainerCore.JunUtils.GetBeatmapDirectoryName(editor.OriginalBeatmap), editor.OriginalBeatmap.Background);
                if (!File.Exists(bgPath))
                {
                    HeroBackgroundRect.IsVisible = false;
                    return;
                }

                using var stream = File.OpenRead(bgPath);
                var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
                HeroBackgroundRect.Fill = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                HeroBackgroundRect.IsVisible = true;

                // IsVisible flips false->true don't get an automatic re-layout on this
                // Avalonia version, so the shape is otherwise left arranged at its stale
                // (0-size) bounds and never paints.
                HeroBackgroundRect.InvalidateMeasure();
                HeroBackgroundRect.InvalidateArrange();
            }
            catch
            {
                HeroBackgroundRect.IsVisible = false;
            }
        }
    }
}
