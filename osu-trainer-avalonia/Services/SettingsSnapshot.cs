using OsuTrainerCore;

namespace osu_trainer_avalonia.Services
{
    public static class SettingsSnapshot
    {
        public static AppSettings Capture(BeatmapEditor editor, bool updatesCheckEnabled)
        {
            return new AppSettings
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
                UpdatesCheckEnabled = updatesCheckEnabled
            };
        }

        /// <summary>Precondition: <paramref name="editor"/> is freshly constructed, at its
        /// defaults — this sets values, it does not synchronise against an existing state.</summary>
        public static void ApplyImmediate(BeatmapEditor editor, AppSettings s)
        {
            editor.BpmRate = s.BpmRate;
            editor.SetScaleAR(s.ScaleAR);
            editor.SetScaleOD(s.ScaleOD);
            if (s.ChangePitch) editor.ToggleChangePitchSetting();
            if (s.NoSpinners) editor.ToggleNoSpinners();
            if (s.HighQualityMp3s) editor.ToggleHighQualityMp3s();
        }

        /// <summary>Precondition: <paramref name="editor"/>.State == READY.</summary>
        public static void ApplyOnReady(BeatmapEditor editor, AppSettings s)
        {
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
    }
}
