using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace osu_trainer_avalonia.Controls
{
    /// <summary>The 7 glyphs this app draws, replacing LucideAvalonia's 1500-icon dictionary.</summary>
    public enum AppIconKind
    {
        Minus,
        X,
        Lock,
        ChevronRight,
        ChevronDown,
        RotateCcw,
        Folder
    }

    /// <summary>
    /// Renders one glyph from a static, hand-verified geometry table (exact Lucide `d` path
    /// data, 24x24 stroke-2 grid) inside a Viewbox, so it scales to <see cref="Size"/> without a
    /// per-icon-set assembly. Stroke-based to match Lucide's rendering (Avalonia's PathIcon is
    /// fill-based and would misrender these).
    /// </summary>
    public partial class AppIcon : UserControl
    {
        public static readonly StyledProperty<AppIconKind> KindProperty =
            AvaloniaProperty.Register<AppIcon, AppIconKind>(nameof(Kind));

        public static readonly StyledProperty<double> SizeProperty =
            AvaloniaProperty.Register<AppIcon, double>(nameof(Size), 24);

        public static readonly StyledProperty<IBrush?> StrokeProperty =
            AvaloniaProperty.Register<AppIcon, IBrush?>(nameof(Stroke));

        public AppIconKind Kind
        {
            get => GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }

        public double Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public IBrush? Stroke
        {
            get => GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        // Exact Lucide `d` path data (24x24, stroke-width 2, round caps/joins), sourced verbatim
        // from lucide-icons/lucide's icons/*.svg. Multi-<path>/<rect> SVGs are folded into one
        // Avalonia path-mini-language string (multiple M subpaths; <rect> converted to explicit
        // M/L/A commands — there is no path-syntax "rect" shorthand).
        private static readonly IReadOnlyDictionary<AppIconKind, string> GeometryData =
            new Dictionary<AppIconKind, string>
            {
                [AppIconKind.Minus] = "M5,12 L19,12",
                [AppIconKind.X] = "M18,6 L6,18 M6,6 L18,18",
                [AppIconKind.Lock] =
                    "M5,11 L19,11 A2,2 0 0 1 21,13 L21,20 A2,2 0 0 1 19,22 L5,22 A2,2 0 0 1 3,20 " +
                    "L3,13 A2,2 0 0 1 5,11 Z M7,11 L7,7 A5,5 0 0 1 17,7 L17,11",
                [AppIconKind.ChevronRight] = "M9,18 L15,12 L9,6",
                [AppIconKind.ChevronDown] = "M6,9 L12,15 L18,9",
                [AppIconKind.RotateCcw] =
                    "M3,12 A9,9 0 1 0 12,3 A9.75,9.75 0 0 0 5.26,5.74 L3,8 M3,3 L3,8 L8,8",
                [AppIconKind.Folder] =
                    "M20,20 A2,2 0 0 0 22,18 L22,8 A2,2 0 0 0 20,6 L12.1,6 A2,2 0 0 1 10.41,5.1 " +
                    "L9.6,3.9 A2,2 0 0 0 7.93,3 L4,3 A2,2 0 0 0 2,5 L2,18 A2,2 0 0 0 4,20 Z",
            };

        static AppIcon()
        {
            KindProperty.Changed.AddClassHandler<AppIcon>((x, _) => x.UpdateGeometry());
            SizeProperty.Changed.AddClassHandler<AppIcon>((x, e) => x.Width = x.Height = (double)e.NewValue!);
            StrokeProperty.Changed.AddClassHandler<AppIcon>((x, _) => x.UpdateStroke());
        }

        public AppIcon()
        {
            InitializeComponent();
            Width = Height = Size;
            UpdateGeometry();
            UpdateStroke();
        }

        private void UpdateGeometry() => IconPath.Data = ResolveGeometry(Kind);

        /// <summary>Pure lookup, split out from <see cref="UpdateGeometry"/> so it is testable
        /// without constructing a <see cref="UserControl"/> (no Avalonia Application needed).</summary>
        internal static Geometry ResolveGeometry(AppIconKind kind)
        {
            if (!GeometryData.TryGetValue(kind, out var data))
                throw new ArgumentOutOfRangeException(nameof(kind), kind,
                    $"No geometry registered for {nameof(AppIconKind)}.{kind}.");

            return Geometry.Parse(data);
        }

        // A null Stroke (unset in XAML) must still draw a visible icon, not an invisible one.
        private void UpdateStroke() => IconPath.Stroke = Stroke ?? Brushes.White;
    }
}
