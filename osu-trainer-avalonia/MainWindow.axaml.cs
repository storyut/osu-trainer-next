using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using OsuTrainerCore;

namespace osu_trainer_avalonia
{
    public partial class MainWindow : Window
    {
        private readonly BeatmapEditor editor;
        private readonly LiveMapWatcher liveMapWatcher;
        private bool updatingFromModel;

        public MainWindow()
        {
            InitializeComponent();

            var host = new AvaloniaCoreHost(msg => StatusText.Text = msg);
            editor = new BeatmapEditor(host);
            editor.BeatmapSwitched += (_, _) => { UpdateHeroBackground(); editor_Updated(); };
            editor.BeatmapModified += (_, _) => editor_Updated();
            editor.StateChanged += (_, _) => editor_StateUpdated();

            UpdateRateBubble(RateSlider.Value);

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

        private void editor_Updated() => RefreshFromModel();
        private void editor_StateUpdated() => RefreshState();

        private void OnLoadClick(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Loading...";
            editor.RequestBeatmapLoad(PathTextBox.Text);
        }

        private void OnGenerateClick(object sender, RoutedEventArgs e)
        {
            editor.GenerateBeatmap();
        }

        private void OnRateChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            UpdateRateBubble(e.NewValue);
            if (updatingFromModel) return;
            editor.SetBpmMultiplier((decimal)e.NewValue);
        }

        private void UpdateRateBubble(double value)
        {
            const double min = 0.5, max = 2.0, trackWidth = 380, bubbleWidth = 48;
            double fraction = (value - min) / (max - min);
            double left = fraction * (trackWidth - bubbleWidth);
            RateBubble.Margin = new Thickness(left, -28, 0, 0);
            RateBubbleText.Text = $"{value:0.00}x";
        }

        private void OnArChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (updatingFromModel) return;
            editor.SetAR((decimal)e.NewValue);
        }

        private void OnCsChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (updatingFromModel) return;
            editor.SetCS((decimal)e.NewValue);
        }

        private void OnOdChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (updatingFromModel) return;
            editor.SetOD((decimal)e.NewValue);
        }

        private void OnHpChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (updatingFromModel) return;
            editor.SetHP((decimal)e.NewValue);
        }

        private void RefreshState()
        {
            GenerateButton.IsEnabled = editor.State == EditorState.READY;
        }

        private void RefreshFromModel()
        {
            if (editor.State != EditorState.READY || editor.NewBeatmap == null)
            {
                StatusText.Text = editor.NotReadyReason switch
                {
                    BadBeatmapReason.ERROR_LOADING_BEATMAP => "Could not load that .osu file.",
                    BadBeatmapReason.EMPTY_MAP => "That beatmap has no hit objects.",
                    _ => "No beatmap loaded."
                };
                return;
            }

            updatingFromModel = true;
            RateSlider.Value = (double)editor.BpmRate;
            ArSlider.Value = (double)editor.NewBeatmap.ApproachRate;
            CsSlider.Value = (double)editor.NewBeatmap.CircleSize;
            OdSlider.Value = (double)editor.NewBeatmap.OverallDifficulty;
            HpSlider.Value = (double)editor.NewBeatmap.HPDrainRate;
            updatingFromModel = false;

            StarRatingDiamond.Stars = (double)editor.StarRating;
            StatusText.Text = "Loaded.";
        }

        private void UpdateHeroBackground()
        {
            try
            {
                if (editor.OriginalBeatmap == null || string.IsNullOrEmpty(editor.OriginalBeatmap.Background))
                {
                    HeroBackgroundImage.IsVisible = false;
                    return;
                }

                string bgPath = Path.Combine(OsuTrainerCore.JunUtils.GetBeatmapDirectoryName(editor.OriginalBeatmap), editor.OriginalBeatmap.Background);
                if (!File.Exists(bgPath))
                {
                    HeroBackgroundImage.IsVisible = false;
                    return;
                }

                using var stream = File.OpenRead(bgPath);
                HeroBackgroundImage.Source = new Avalonia.Media.Imaging.Bitmap(stream);
                HeroBackgroundImage.IsVisible = true;

                // IsVisible flips false->true don't get an automatic re-layout on this
                // Avalonia version, so the Image is otherwise left arranged at its stale
                // (0-size) bounds and never paints.
                HeroBackgroundImage.InvalidateMeasure();
                HeroBackgroundImage.InvalidateArrange();
            }
            catch
            {
                HeroBackgroundImage.IsVisible = false;
            }
        }
    }
}
