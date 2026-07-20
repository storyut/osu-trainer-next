using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using OsuTrainerCore;

namespace osu_trainer_avalonia
{
    public partial class MainWindow : Window
    {
        private readonly BeatmapEditor editor;
        private bool updatingFromModel;

        public MainWindow()
        {
            InitializeComponent();

            var host = new AvaloniaCoreHost(msg => StatusText.Text = msg);
            editor = new BeatmapEditor(host);
            editor.BeatmapSwitched += (_, _) => editor_Updated();
            editor.BeatmapModified += (_, _) => editor_Updated();
            editor.StateChanged += (_, _) => editor_StateUpdated();
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
            if (updatingFromModel) return;
            editor.SetBpmMultiplier((decimal)e.NewValue);
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

            StarRatingText.Text = $"Star rating: {editor.StarRating:0.00}";
            StatusText.Text = "Loaded.";
        }
    }
}
