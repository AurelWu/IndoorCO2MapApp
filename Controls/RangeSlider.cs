using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;

namespace IndoorCO2MapAppV2.Controls;

public partial class RangeSlider : ContentView
{
    // Bindable properties
    public static readonly BindableProperty MinimumProperty =
        BindableProperty.Create(nameof(Minimum), typeof(int), typeof(RangeSlider), 0);

    public static readonly BindableProperty MaximumProperty =
        BindableProperty.Create(nameof(Maximum), typeof(int), typeof(RangeSlider), 100);

    public static readonly BindableProperty LowerValueProperty =
        BindableProperty.Create(nameof(LowerValue), typeof(int), typeof(RangeSlider), 0, BindingMode.TwoWay);

    public static readonly BindableProperty UpperValueProperty =
        BindableProperty.Create(nameof(UpperValue), typeof(int), typeof(RangeSlider), 100, BindingMode.TwoWay);

    public static readonly BindableProperty ThumbSizeProperty =
        BindableProperty.Create(nameof(ThumbSize), typeof(double), typeof(RangeSlider), 24.0, propertyChanged: OnThumbSizeChanged);

    public static readonly BindableProperty TrackHeightProperty =
        BindableProperty.Create(nameof(TrackHeight), typeof(double), typeof(RangeSlider), 4.0, propertyChanged: OnTrackHeightChanged);

    // Properties
    public int Minimum { get => (int)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public int Maximum { get => (int)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public int LowerValue { get => (int)GetValue(LowerValueProperty); set => SetValue(LowerValueProperty, value); }
    public int UpperValue { get => (int)GetValue(UpperValueProperty); set => SetValue(UpperValueProperty, value); }
    public double ThumbSize { get => (double)GetValue(ThumbSizeProperty); set => SetValue(ThumbSizeProperty, value); }
    public double TrackHeight { get => (double)GetValue(TrackHeightProperty); set => SetValue(TrackHeightProperty, value); }

    // Visual Elements
    private readonly BoxView _track = new() { Color = Colors.Gray, HeightRequest = 4, CornerRadius = 2 };
    private readonly BoxView _highlight = new() { Color = Colors.DodgerBlue, HeightRequest = 4, CornerRadius = 2 };

    private readonly Border _lowerThumb = new()
    {
        Background = Colors.White,
        Stroke = Colors.Black,
        StrokeThickness = 1
    };

    private readonly Border _upperThumb = new()
    {
        Background = Colors.White,
        Stroke = Colors.Black,
        StrokeThickness = 1
    };

    private readonly AbsoluteLayout _layout = [];

    public RangeSlider()
    {
        // Set default thumb size
        UpdateThumbSize();
        UpdateTrackHeight();

        // Add elements
        _layout.Children.Add(_track);
        _layout.Children.Add(_highlight);
        _layout.Children.Add(_lowerThumb);
        _layout.Children.Add(_upperThumb);
        Content = _layout;

        // Gesture recognizers
        var lowerPan = new PanGestureRecognizer();
        lowerPan.PanUpdated += OnLowerPan;
        _lowerThumb.GestureRecognizers.Add(lowerPan);

        var upperPan = new PanGestureRecognizer();
        upperPan.PanUpdated += OnUpperPan;
        _upperThumb.GestureRecognizers.Add(upperPan);

        Loaded += (_, __) => UpdateLayoutPositions();
        SizeChanged += (_, __) => UpdateLayoutPositions();
    }

    // -----------------------------
    // Thumb and Track updates
    // -----------------------------
    private static void OnThumbSizeChanged(BindableObject bindable, object oldVal, object newVal)
    {
        if (bindable is RangeSlider slider) slider.UpdateThumbSize();
    }

    private static void OnTrackHeightChanged(BindableObject bindable, object oldVal, object newVal)
    {
        if (bindable is RangeSlider slider) slider.UpdateTrackHeight();
    }

    private void UpdateThumbSize()
    {
        _lowerThumb.WidthRequest = ThumbSize;
        _lowerThumb.HeightRequest = ThumbSize;
        _lowerThumb.StrokeShape = new RoundRectangle { CornerRadius = ThumbSize / 2 };

        _upperThumb.WidthRequest = ThumbSize;
        _upperThumb.HeightRequest = ThumbSize;
        _upperThumb.StrokeShape = new RoundRectangle { CornerRadius = ThumbSize / 2 };
        UpdateLayoutPositions();
    }

    private void UpdateTrackHeight()
    {
        _track.HeightRequest = TrackHeight;
        _highlight.HeightRequest = TrackHeight;
        UpdateLayoutPositions();
    }

    // -----------------------------
    // Pan logic
    // -----------------------------
    private double _startLowerX;
    private double _startUpperX;

    private void OnLowerPan(object? sender, PanUpdatedEventArgs e)
    {
        if (_layout.Width <= 0) return;

        if (e.StatusType == GestureStatus.Started) _startLowerX = _lowerThumb.TranslationX;

        if (e.StatusType == GestureStatus.Running)
        {
            double trackWidth = _layout.Width - ThumbSize;
            double newX = Math.Clamp(_startLowerX + e.TotalX, 0, _upperThumb.TranslationX);
            LowerValue = PositionToValue(newX, trackWidth);
            UpdateLayoutPositions();
        }
    }

    private void OnUpperPan(object? sender, PanUpdatedEventArgs e)
    {
        if (_layout.Width <= 0) return;

        if (e.StatusType == GestureStatus.Started) _startUpperX = _upperThumb.TranslationX;

        if (e.StatusType == GestureStatus.Running)
        {
            double trackWidth = _layout.Width - ThumbSize;
            double newX = Math.Clamp(_startUpperX + e.TotalX, _lowerThumb.TranslationX, trackWidth);
            UpperValue = PositionToValue(newX, trackWidth);
            UpdateLayoutPositions();
        }
    }

    // -----------------------------
    // Layout
    // -----------------------------
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        UpdateLayoutPositions();
    }

    private void UpdateLayoutPositions()
    {
        if (_layout.Width <= 0) return;

        double trackWidth = _layout.Width - ThumbSize;

        double lw = ValueToPosition(LowerValue, trackWidth);
        double uw = ValueToPosition(UpperValue, trackWidth);

        double centerY = Height / 2 - ThumbSize / 2;

        // Track and highlight
        AbsoluteLayout.SetLayoutBounds(_track, new Rect(0, Height / 2 - TrackHeight / 2, _layout.Width, TrackHeight));
        AbsoluteLayout.SetLayoutBounds(_highlight, new Rect(lw + ThumbSize / 2, Height / 2 - TrackHeight / 2, uw - lw, TrackHeight));

        // Thumbs
        _lowerThumb.TranslationX = lw;
        _lowerThumb.TranslationY = centerY;

        _upperThumb.TranslationX = uw;
        _upperThumb.TranslationY = centerY;
    }

    // -----------------------------
    // Helpers
    // -----------------------------
    private double ValueToPosition(int value, double trackWidth)
    {
        double range = Maximum - Minimum;
        return (value - Minimum) / range * trackWidth;
    }

    private int PositionToValue(double pos, double trackWidth)
    {
        if (trackWidth <= 0) return Minimum;
        double ratio = Math.Clamp(pos / trackWidth, 0, 1);
        return Minimum + (int)Math.Round(ratio * (Maximum - Minimum));
    }
}
