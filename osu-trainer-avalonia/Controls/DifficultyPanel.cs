using System;
using Avalonia.Controls;
using OsuTrainerCore;

namespace osu_trainer_avalonia.Controls
{
    /// <summary>
    /// Everything MainWindow and QuickSettingsWindow show for the four difficulty rows and
    /// the rate slider, decided once. <see cref="DifficultyPanel.Project"/> reads the model
    /// and answers every question — enabled or not, which values, how the BPM line reads —
    /// so neither window has to, and the two can no longer drift apart.
    /// </summary>
    internal sealed record DifficultyPanelState(
        bool Enabled,
        double Hp, double Cs, double Ar, double Od,
        bool HpLocked, bool CsLocked, bool ArLocked, bool OdLocked,
        double Rate, string BpmLine);

    /// <summary>The controls a window hands the panel to drive. Built once, in its constructor.</summary>
    internal sealed record DifficultyPanelControls(
        DifficultyRow Hp, DifficultyRow Cs, DifficultyRow Ar, DifficultyRow Od,
        Slider Rate, TextBlock BpmLine);

    internal static class DifficultyPanel
    {
        /// <summary>
        /// The rows accept input only with a beatmap loaded and no export in flight —
        /// editing AR mid-export would mutate the model the batch is reading.
        /// </summary>
        internal static bool IsPanelEnabled(EditorState state, bool hasBeatmap) =>
            state == EditorState.READY && hasBeatmap;

        internal static DifficultyPanelState Project(BeatmapEditor editor)
        {
            // GetOriginalBpmData()/GetNewBpmData() read NewBeatmap, so the disabled path must
            // answer without them. Nothing stale leaks out either: values and locks read zero.
            // The trailing null test is redundant with IsPanelEnabled's hasBeatmap argument;
            // it is there so the compiler can narrow `beatmap` below.
            var beatmap = editor.NewBeatmap;
            if (!IsPanelEnabled(editor.State, beatmap != null) || beatmap == null)
                return new DifficultyPanelState(
                    Enabled: false,
                    Hp: 0, Cs: 0, Ar: 0, Od: 0,
                    HpLocked: false, CsLocked: false, ArLocked: false, OdLocked: false,
                    Rate: (double)editor.BpmRate,
                    BpmLine: "—");

            var (origBpm, _, _) = editor.GetOriginalBpmData();
            var (newBpm, _, _) = editor.GetNewBpmData();

            return new DifficultyPanelState(
                Enabled: true,
                Hp: (double)beatmap.HPDrainRate,
                Cs: (double)beatmap.CircleSize,
                Ar: (double)beatmap.ApproachRate,
                Od: (double)beatmap.OverallDifficulty,
                HpLocked: editor.HpIsLocked,
                CsLocked: editor.CsIsLocked,
                ArLocked: editor.ArIsLocked,
                OdLocked: editor.OdIsLocked,
                Rate: (double)editor.BpmRate,
                BpmLine: DifficultyMath.FormatRateBpmLine(editor.BpmRate, origBpm, newBpm));
        }

        /// <summary>
        /// Subscribes the user-edit direction, once. <paramref name="isRefreshing"/> is the
        /// caller's own model-refresh guard: a raw <see cref="Slider"/> raises ValueChanged on
        /// a programmatic write (unlike <see cref="DifficultyRow"/>), so without it every
        /// <see cref="Apply"/> would echo straight back into the model.
        /// </summary>
        internal static void Wire(DifficultyPanelControls c, BeatmapEditor editor, Func<bool> isRefreshing)
        {
            c.Hp.ValueCommitted += (_, v) => { if (!isRefreshing()) editor.SetHP((decimal)v); };
            c.Cs.ValueCommitted += (_, v) => { if (!isRefreshing()) editor.SetCS((decimal)v); };
            c.Ar.ValueCommitted += (_, v) => { if (!isRefreshing()) editor.SetAR((decimal)v); };
            c.Od.ValueCommitted += (_, v) => { if (!isRefreshing()) editor.SetOD((decimal)v); };

            // Unguarded: DifficultyRow raises LockToggled only on a real click.
            c.Hp.LockToggled += (_, _) => editor.ToggleHpLock();
            c.Cs.LockToggled += (_, _) => editor.ToggleCsLock();
            c.Ar.LockToggled += (_, _) => editor.ToggleArLock();
            c.Od.LockToggled += (_, _) => editor.ToggleOdLock();

            c.Rate.ValueChanged += (_, e) => { if (!isRefreshing()) editor.SetBpmMultiplier((decimal)e.NewValue); };
        }

        /// <summary>
        /// Pushes a projected state into the controls. Callers invoke this inside their own
        /// model-refresh guard. Disabled leaves the last values on screen rather than snapping
        /// them to zero — greyed-out stale numbers read better than a wall of zeros.
        /// </summary>
        internal static void Apply(DifficultyPanelControls c, DifficultyPanelState state)
        {
            c.BpmLine.Text = state.BpmLine;

            if (!state.Enabled)
            {
                c.Hp.IsEnabled = c.Cs.IsEnabled = c.Ar.IsEnabled = c.Od.IsEnabled = false;
                return;
            }

            c.Hp.Value = state.Hp;
            c.Cs.Value = state.Cs;
            c.Ar.Value = state.Ar;
            c.Od.Value = state.Od;

            c.Hp.IsLocked = state.HpLocked;
            c.Cs.IsLocked = state.CsLocked;
            c.Ar.IsLocked = state.ArLocked;
            c.Od.IsLocked = state.OdLocked;

            c.Rate.Value = state.Rate;

            c.Hp.IsEnabled = c.Cs.IsEnabled = c.Ar.IsEnabled = c.Od.IsEnabled = true;
        }
    }
}
