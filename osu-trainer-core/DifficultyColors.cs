namespace OsuTrainerCore
{
    public static class DifficultyColors
    {
        public static readonly (byte R, byte G, byte B) Easy       = (136, 179, 0);
        public static readonly (byte R, byte G, byte B) Normal     = (102, 204, 255);
        public static readonly (byte R, byte G, byte B) Hard       = (255, 204, 34);
        public static readonly (byte R, byte G, byte B) Insane     = (255, 102, 170);
        public static readonly (byte R, byte G, byte B) Expert     = (170, 136, 255);
        public static readonly (byte R, byte G, byte B) ExpertPlus = (23, 22, 28);

        public static (byte R, byte G, byte B) GetDifficultyColor(decimal stars)
        {
            if (stars < 2M) return Easy;
            if (stars < 2.7M) return Normal;
            if (stars < 4M) return Hard;
            if (stars < 5.3M) return Insane;
            if (stars < 6.5M) return Expert;
            return ExpertPlus;
        }
    }
}
