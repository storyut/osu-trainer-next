using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using osu_trainer_avalonia.Controls;
using OsuTrainerCore;

namespace osu_trainer_avalonia
{
    public partial class MainWindow : Window
    {
        private readonly BeatmapEditor editor;
        private readonly LiveMapWatcher liveMapWatcher;
        private readonly Button[] profileButtons = new Button[4];
        private bool updatingFromModel;

        public MainWindow()
        {
            InitializeComponent();

            var host = new AvaloniaCoreHost(msg => StatusText.Text = msg);
            editor = new BeatmapEditor(host);
            editor.BeatmapSwitched += (_, _) => { UpdateHeroBackground(); RefreshControlsFromModel(); };
            editor.BeatmapModified += (_, _) => RefreshControlsFromModel();
            editor.ControlsModified += (_, _) => RefreshControlsFromModel();
            editor.StateChanged += (_, _) => RefreshState();

            WireDifficultyRows();
            BuildProfileSlots();

            UpdateRateBubble(RateSlider.Value);
            RefreshControlsFromModel();

            liveMapWatcher = new LiveMapWatcher(host);
            liveMapWatcher.SongsFolderDetected += (_, folder) => OsuTrainerCore.JunUtils.SongsFolder = folder;
            liveMapWatcher.BeatmapDetected += (_, path) =>
            {
                PathTextBox.Text = path;
                editor.RequestBeatmapLoad(path);
            };
            liveMapWatcher.Start();

            Closed += (_, _) => liveMapWatcher.Stop();
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

        private void OnGenerateClick(object? sender, RoutedEventArgs e) => editor.GenerateBeatmap();

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
            const double min = 0.5, max = 2.0, trackWidth = 400, bubbleWidth = 48;
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
            MoreButton.Content = show ? "▼ Less" : "▶ More";
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

        // ---- profiles ---------------------------------------------------------

        private void BuildProfileSlots()
        {
            editor.LoadProfilesFromDisk();
            for (int i = 0; i < profileButtons.Length; i++)
            {
                int idx = i;

                var loadButton = new Button
                {
                    Content = editor.UserProfiles[idx].Name,
                    Width = 64,
                    FontSize = 11,
                    Padding = new Thickness(4, 3),
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
                    profileButtons[idx].Content = editor.UserProfiles[idx].Name;
                    flyout.Hide();
                };

                var menuButton = new Button
                {
                    Content = "▾",
                    FontSize = 11,
                    Padding = new Thickness(4, 3),
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

        private void RefreshState() => GenerateButton.IsEnabled = editor.State == EditorState.READY;

        private void RefreshControlsFromModel()
        {
            if (editor.State != EditorState.READY || editor.NewBeatmap == null)
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

            GenerateButton.IsEnabled = editor.State == EditorState.READY;

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
