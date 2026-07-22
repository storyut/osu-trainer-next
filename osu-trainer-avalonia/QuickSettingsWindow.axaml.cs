using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using osu_trainer_avalonia.Controls;
using OsuTrainerCore;

namespace osu_trainer_avalonia
{
    /// <summary>
    /// The tray icon's quick-settings flyout: rate + HP/CS/AR/OD, reusing
    /// <see cref="DifficultyRow"/> and bound to the same <see cref="BeatmapEditor"/>
    /// instance MainWindow uses, so an edit in either window shows up in the other.
    /// Hidden (not closed) on dismiss so it can be reopened without re-measuring/re-binding.
    /// </summary>
    public partial class QuickSettingsWindow : Window
    {
        private readonly BeatmapEditor editor;
        private bool updatingFromModel;

        /// <summary>Design-time/previewer only — the app always uses the (BeatmapEditor) constructor.</summary>
        public QuickSettingsWindow() : this(new BeatmapEditor(new AvaloniaCoreHost(_ => { })))
        {
        }

        public QuickSettingsWindow(BeatmapEditor editor)
        {
            InitializeComponent();
            this.editor = editor;

            RateSlider.ValueChanged += OnRateChanged;
            HpRow.ValueCommitted += (_, v) => { if (!updatingFromModel) editor.SetHP((decimal)v); };
            CsRow.ValueCommitted += (_, v) => { if (!updatingFromModel) editor.SetCS((decimal)v); };
            ArRow.ValueCommitted += (_, v) => { if (!updatingFromModel) editor.SetAR((decimal)v); };
            OdRow.ValueCommitted += (_, v) => { if (!updatingFromModel) editor.SetOD((decimal)v); };
            HpRow.LockToggled += (_, _) => editor.ToggleHpLock();
            CsRow.LockToggled += (_, _) => editor.ToggleCsLock();
            ArRow.LockToggled += (_, _) => editor.ToggleArLock();
            OdRow.LockToggled += (_, _) => editor.ToggleOdLock();

            editor.BeatmapSwitched += (_, _) => RefreshFromModel();
            editor.BeatmapModified += (_, _) => RefreshFromModel();
            editor.ControlsModified += (_, _) => RefreshFromModel();

            Deactivated += (_, _) => Hide();
            KeyDown += (_, e) => { if (e.Key == Key.Escape) Hide(); };
        }

        public void ShowAtBottomRight()
        {
            RefreshFromModel();
            Show();
            Activate();

            var area = Screens.Primary?.WorkingArea;
            if (area == null) return;

            double w = Bounds.Width > 0 ? Bounds.Width : 284;
            double h = Bounds.Height > 0 ? Bounds.Height : 340;
            Position = new PixelPoint(
                area.Value.Right - (int)w - 12,
                area.Value.Bottom - (int)h - 12);
        }

        private void OnRateChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            UpdateRateBubble(e.NewValue);
            if (updatingFromModel) return;
            editor.SetBpmMultiplier((decimal)e.NewValue);
        }

        private void UpdateRateBubble(double value)
        {
            const double min = 0.5, max = 2.0, trackWidth = 236, bubbleWidth = 48;
            double fraction = (value - min) / (max - min);
            double left = fraction * (trackWidth - bubbleWidth);
            RateBubble.Margin = new Thickness(left, -28, 0, 0);
            RateBubbleText.Text = $"{value:0.00}x";
        }

        private void RefreshFromModel()
        {
            if (editor.State != EditorState.READY || editor.NewBeatmap == null)
                return;

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

            var (origBpm, _, _) = editor.GetOriginalBpmData();
            var (newBpm, _, _) = editor.GetNewBpmData();
            BpmText.Text = DifficultyMath.FormatBpm(origBpm, newBpm);

            updatingFromModel = false;
        }
    }
}
