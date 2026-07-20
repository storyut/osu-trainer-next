using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OsuTrainerCore;

namespace osu_trainer_avalonia.Controls
{
    public partial class StarRatingDiamond : UserControl
    {
        public static readonly StyledProperty<double> StarsProperty =
            AvaloniaProperty.Register<StarRatingDiamond, double>(nameof(Stars));

        public double Stars
        {
            get => GetValue(StarsProperty);
            set => SetValue(StarsProperty, value);
        }

        static StarRatingDiamond()
        {
            StarsProperty.Changed.AddClassHandler<StarRatingDiamond>((x, e) => x.UpdateVisual());
        }

        public StarRatingDiamond()
        {
            InitializeComponent();
        }

        private void UpdateVisual()
        {
            var (r, g, b) = DifficultyColors.GetDifficultyColor((decimal)Stars);
            Diamond.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
            StarsText.Text = $"{Stars:0.00}";
        }
    }
}
