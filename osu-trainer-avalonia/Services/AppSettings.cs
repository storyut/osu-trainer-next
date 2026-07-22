namespace osu_trainer_avalonia.Services
{
    /// <summary>
    /// Everything <c>BeatmapEditor.SaveSettings()</c> currently leaves as an in-memory-only
    /// stub. Locked HP/CS/AR/OD/BPM values only matter when their paired *IsLocked flag is
    /// set — an unlocked value is beatmap-derived and never persisted.
    /// </summary>
    public sealed class AppSettings
    {
        public decimal BpmRate { get; set; } = 1.0M;
        public bool BpmIsLocked { get; set; }
        public int LockedBpm { get; set; } = 200;

        public bool HpIsLocked { get; set; }
        public decimal LockedHp { get; set; }
        public bool CsIsLocked { get; set; }
        public decimal LockedCs { get; set; }
        public bool ArIsLocked { get; set; }
        public decimal LockedAr { get; set; }
        public bool OdIsLocked { get; set; }
        public decimal LockedOd { get; set; }

        public bool ScaleAR { get; set; } = true;
        public bool ScaleOD { get; set; } = true;
        public bool ForceHardrockCirclesize { get; set; }
        public bool ChangePitch { get; set; }
        public bool NoSpinners { get; set; }
        public bool HighQualityMp3s { get; set; }

        public bool UpdatesCheckEnabled { get; set; } = true;
    }
}
