using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace DelimPlot.App.Controls;

public sealed class FixedSlider : Control
{
    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<FixedSlider, double>(nameof(Minimum), 0);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<FixedSlider, double>(nameof(Maximum), 1);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<FixedSlider, double>(nameof(Step), 0);

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<FixedSlider, double>(
            nameof(Value),
            0,
            defaultBindingMode: BindingMode.TwoWay);

    private const double TrackHeight = 6;
    private const double ThumbRadius = 9;
    private const double HorizontalPadding = 11;
    private const double DesiredWidth = 180;
    private const double DesiredHeight = 30;
    private bool _isDragging;

    public FixedSlider()
    {
        MinWidth = DesiredWidth;
        MinHeight = DesiredHeight;
        Height = DesiredHeight;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var width = Math.Max(Bounds.Width, MinWidth);
        var height = Math.Max(Bounds.Height, MinHeight);
        var y = height / 2;
        var left = HorizontalPadding;
        var right = width - HorizontalPadding;
        var trackWidth = Math.Max(1, right - left);
        var ratio = GetRatio();
        var thumbX = left + trackWidth * ratio;

        var baseTrack = new Rect(left, y - TrackHeight / 2, trackWidth, TrackHeight);
        var activeTrack = new Rect(left, y - TrackHeight / 2, Math.Max(0, thumbX - left), TrackHeight);

        context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#D1D5DB")), null, baseTrack, TrackHeight / 2);
        context.DrawRectangle(new SolidColorBrush(Color.Parse("#2563EB")), null, activeTrack, TrackHeight / 2);

        var thumbCenter = new Point(thumbX, y);
        context.DrawEllipse(
            Brushes.White,
            new Pen(new SolidColorBrush(Color.Parse("#2563EB")), 2),
            thumbCenter,
            ThumbRadius,
            ThumbRadius);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width)
            ? DesiredWidth
            : Math.Max(DesiredWidth, availableSize.Width);

        return new Size(width, DesiredHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        return new Size(Math.Max(finalSize.Width, DesiredWidth), DesiredHeight);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MinimumProperty ||
            change.Property == MaximumProperty ||
            change.Property == StepProperty ||
            change.Property == ValueProperty)
        {
            if (change.Property != ValueProperty)
                Value = ClampAndSnap(Value);

            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isDragging = true;
        e.Pointer.Capture(this);
        SetValueFromPointer(e);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isDragging)
            return;

        SetValueFromPointer(e);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_isDragging)
            return;

        _isDragging = false;
        e.Pointer.Capture(null);
        SetValueFromPointer(e);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isDragging = false;
    }

    private void SetValueFromPointer(PointerEventArgs e)
    {
        var width = Math.Max(Bounds.Width, MinWidth);
        var trackWidth = Math.Max(1, width - HorizontalPadding * 2);
        var x = Math.Clamp(e.GetPosition(this).X - HorizontalPadding, 0, trackWidth);
        var ratio = x / trackWidth;
        Value = ClampAndSnap(Minimum + ratio * (Maximum - Minimum));
    }

    private double GetRatio()
    {
        if (Maximum <= Minimum)
            return 0;

        return Math.Clamp((Value - Minimum) / (Maximum - Minimum), 0, 1);
    }

    private double ClampAndSnap(double value)
    {
        if (Maximum <= Minimum)
            return Minimum;

        var clamped = Math.Clamp(value, Minimum, Maximum);
        if (Step <= 0)
            return clamped;

        var steps = Math.Round((clamped - Minimum) / Step);
        return Math.Clamp(Minimum + steps * Step, Minimum, Maximum);
    }
}
