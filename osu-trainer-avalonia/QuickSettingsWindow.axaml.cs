using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using osu_trainer_avalonia.Controls;
using osu_trainer_avalonia.Interop;
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
        private readonly DifficultyPanelControls difficultyControls;
        private bool updatingFromModel;

        /// <summary>Design-time/previewer only — the app always uses the (BeatmapEditor) constructor.</summary>
        public QuickSettingsWindow() : this(new BeatmapEditor(new AvaloniaCoreHost(_ => { })))
        {
        }

        public QuickSettingsWindow(BeatmapEditor editor)
        {
            InitializeComponent();
            this.editor = editor;

            difficultyControls = new DifficultyPanelControls(HpRow, CsRow, ArRow, OdRow, RateSlider, BpmText);
            DifficultyPanel.Wire(difficultyControls, editor, () => updatingFromModel);

            GenerateButton.Click += (_, _) => editor.GenerateBeatmap();

            editor.BeatmapSwitched += (_, _) => RefreshFromModel();
            editor.BeatmapModified += (_, _) => RefreshFromModel();
            editor.ControlsModified += (_, _) => RefreshFromModel();
            // Re-enables the rows once an export finishes; nothing else raises an event then.
            editor.StateChanged += (_, _) => RefreshFromModel();

            Deactivated += (_, _) => Hide();
            KeyDown += (_, e) => { if (e.Key == Key.Escape) Hide(); };
        }

        public void ShowAtBottomRight()
        {
            RefreshFromModel();
            Show();
            Activate();

            var cursor = CursorInterop.GetCursorScreenPosition();
            var screen = Screens.ScreenFromPoint(cursor) ?? Screens.Primary;
            var area = screen?.WorkingArea;
            if (area == null) return;

            double w = Bounds.Width > 0 ? Bounds.Width : 284;
            double h = Bounds.Height > 0 ? Bounds.Height : 340;

            int x = cursor.X - (int)w - 12;
            int y = cursor.Y - (int)h - 12;
            x = Math.Clamp(x, area.Value.X, area.Value.X + area.Value.Width - (int)w);
            y = Math.Clamp(y, area.Value.Y, area.Value.Y + area.Value.Height - (int)h);

            Position = new PixelPoint(x, y);
        }

        private void RefreshFromModel()
        {
            var state = DifficultyPanel.Project(editor);

            updatingFromModel = true;
            DifficultyPanel.Apply(difficultyControls, state);
            GenerateButton.IsEnabled = state.Enabled;
            updatingFromModel = false;
        }
    }
}
