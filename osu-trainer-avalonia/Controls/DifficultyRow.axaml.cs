using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace osu_trainer_avalonia.Controls
{
    /// <summary>
    /// A single "difficulty" row: label | editable numeric box | slider | lock toggle |
    /// optional trailing content (e.g. a Scale/HR checkbox). The slider and numeric box stay
    /// in sync; user edits raise <see cref="ValueCommitted"/> and lock clicks raise
    /// <see cref="LockToggled"/>. Programmatic updates via the <see cref="Value"/>/<see cref="IsLocked"/>
    /// properties (used by MainWindow's model-refresh) do NOT raise those events.
    /// </summary>
    public partial class DifficultyRow : UserControl
    {
        public static readonly StyledProperty<string> LabelProperty =
            AvaloniaProperty.Register<DifficultyRow, string>(nameof(Label), "AR");

        public static readonly StyledProperty<double> ValueProperty =
            AvaloniaProperty.Register<DifficultyRow, double>(nameof(Value));

        public static readonly StyledProperty<double> MinimumProperty =
            AvaloniaProperty.Register<DifficultyRow, double>(nameof(Minimum), 0);

        public static readonly StyledProperty<double> MaximumProperty =
            AvaloniaProperty.Register<DifficultyRow, double>(nameof(Maximum), 11);

        public static readonly StyledProperty<bool> IsLockedProperty =
            AvaloniaProperty.Register<DifficultyRow, bool>(nameof(IsLocked));

        public static readonly StyledProperty<object?> TrailingContentProperty =
            AvaloniaProperty.Register<DifficultyRow, object?>(nameof(TrailingContent));

        public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
        public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        public bool IsLocked { get => GetValue(IsLockedProperty); set => SetValue(IsLockedProperty, value); }
        public object? TrailingContent { get => GetValue(TrailingContentProperty); set => SetValue(TrailingContentProperty, value); }

        /// <summary>Raised when the user commits a new value (slider drag or box entry). Not raised on model refresh.</summary>
        public event EventHandler<double>? ValueCommitted;

        /// <summary>Raised when the user clicks the lock toggle. Not raised on model refresh.</summary>
        public event EventHandler? LockToggled;

        // Guards re-entrancy so pushing a value into the controls doesn't loop back out as a commit.
        private bool suppress;

        static DifficultyRow()
        {
            LabelProperty.Changed.AddClassHandler<DifficultyRow>((x, e) =>
            {
                if (x.LabelText != null) x.LabelText.Text = (string)e.NewValue!;
                x.ApplyAutomationIds();
            });
            ValueProperty.Changed.AddClassHandler<DifficultyRow>((x, e) => x.OnValueChanged((double)e.NewValue!));
            MinimumProperty.Changed.AddClassHandler<DifficultyRow>((x, e) =>
            {
                if (x.ValueSlider != null) x.ValueSlider.Minimum = (double)e.NewValue!;
            });
            MaximumProperty.Changed.AddClassHandler<DifficultyRow>((x, e) =>
            {
                if (x.ValueSlider != null) x.ValueSlider.Maximum = (double)e.NewValue!;
            });
            IsLockedProperty.Changed.AddClassHandler<DifficultyRow>((x, e) =>
            {
                if (x.LockButton != null) x.LockButton.IsChecked = (bool)e.NewValue!;
            });
        }

        public DifficultyRow()
        {
            InitializeComponent();

            LabelText.Text = Label;
            ValueSlider.Minimum = Minimum;
            ValueSlider.Maximum = Maximum;
            // Difficulty values move in 0.1 steps — snap the thumb so a drag can never land on
            // a 0.01 fraction (which would export to osu! as "AR ~8" instead of AR 8).
            ValueSlider.TickFrequency = 0.1;
            ValueSlider.IsSnapToTickEnabled = true;
            ValueSlider.Value = Value;
            ValueBox.Text = DifficultyMath.FormatDifficulty(Value);
            LockButton.IsChecked = IsLocked;

            ApplyAutomationIds();

            ValueSlider.ValueChanged += OnSliderChanged;
            ValueBox.KeyDown += OnBoxKeyDown;
            ValueBox.LostFocus += (_, _) => CommitBox();
            LockButton.Click += OnLockClick;
        }

        /// <summary>
        /// Names the inner parts after the row's label ("ARBox", "ARSlider", "ARLock").
        /// A UserControl wrapper is not surfaced in the Windows automation tree, so an
        /// AutomationId on the row itself is unreachable — the parts need their own.
        /// </summary>
        private void ApplyAutomationIds()
        {
            if (ValueBox == null) return;
            ValueBox.SetValue(AutomationProperties.AutomationIdProperty, $"{Label}Box");
            ValueSlider.SetValue(AutomationProperties.AutomationIdProperty, $"{Label}Slider");
            LockButton.SetValue(AutomationProperties.AutomationIdProperty, $"{Label}Lock");
        }

        private void OnValueChanged(double v)
        {
            if (suppress) return;
            suppress = true;
            if (ValueSlider != null) ValueSlider.Value = v;
            if (ValueBox != null) ValueBox.Text = DifficultyMath.FormatDifficulty(v);
            suppress = false;
        }

        private void OnSliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            if (suppress) return;
            // Snapping keeps the thumb on 0.1 ticks, but round anyway so the committed value is
            // a clean single-decimal number rather than 8.000000000000002 from tick math.
            double v = Math.Round(e.NewValue, 1);
            suppress = true;
            Value = v;
            ValueSlider.Value = v;
            ValueBox.Text = DifficultyMath.FormatDifficulty(v);
            suppress = false;
            ValueCommitted?.Invoke(this, v);
        }

        private void OnBoxKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitBox();
                e.Handled = true;
            }
        }

        private void CommitBox()
        {
            // Merely tabbing through the box must not commit. The box is formatted to one
            // decimal, so committing an untouched 7.1 over a model value of 7.14 would
            // silently round it. If the text still matches what the model rendered, do nothing.
            if (ValueBox.Text == DifficultyMath.FormatDifficulty(Value))
                return;

            var parsed = DifficultyMath.ParseClamp(ValueBox.Text, Minimum, Maximum);
            suppress = true;
            if (parsed is double d)
            {
                d = Math.Round(d, 1);
                Value = d;
                ValueSlider.Value = d;
                ValueBox.Text = DifficultyMath.FormatDifficulty(d);
                suppress = false;
                ValueCommitted?.Invoke(this, d);
            }
            else
            {
                // Unparseable entry: silently revert to the current model value.
                ValueBox.Text = DifficultyMath.FormatDifficulty(Value);
                suppress = false;
            }
        }

        private void OnLockClick(object? sender, RoutedEventArgs e)
        {
            // Report the user's intent; MainWindow drives the core lock and pushes the
            // authoritative IsLocked back. Revert the optimistic check to avoid drift.
            if (LockButton != null) LockButton.IsChecked = IsLocked;
            LockToggled?.Invoke(this, EventArgs.Empty);
        }
    }
}
