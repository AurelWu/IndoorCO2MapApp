using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Layouts;

namespace IndoorCO2MapAppV2.Controls;

public partial class RangeSlider : ContentView
{
    public event EventHandler? RangeChanged;

    // Bindable properties
    public static readonly BindableProperty MinimumProperty =
        BindableProperty.Create(nameof(Minimum), typeof(int), typeof(RangeSlider), 0, propertyChanged: OnMinimumChanged);
    public static readonly BindableProperty MaximumProperty =
        BindableProperty.Create(nameof(Maximum), typeof(int), typeof(RangeSlider), 100, propertyChanged: OnMaximumChanged);
    public static readonly BindableProperty LowerValueProperty =
        BindableProperty.Create(nameof(LowerValue), typeof(int), typeof(RangeSlider), 0, BindingMode.TwoWay, propertyChanged: OnLowerValueChanged);
    public static readonly BindableProperty UpperValueProperty =
        BindableProperty.Create(nameof(UpperValue), typeof(int), typeof(RangeSlider), 100, BindingMode.TwoWay, propertyChanged: OnUpperValueChanged);
    public static readonly BindableProperty ThumbSizeProperty =
        BindableProperty.Create(nameof(ThumbSize), typeof(double), typeof(RangeSlider), 24.0, propertyChanged: OnThumbSizeChanged);
    public static readonly BindableProperty TrackHeightProperty =
        BindableProperty.Create(nameof(TrackHeight), typeof(double), typeof(RangeSlider), 4.0, propertyChanged: OnTrackHeightChanged);
    public static readonly BindableProperty TrackColorProperty =
        BindableProperty.Create(nameof(TrackColor), typeof(Color), typeof(RangeSlider), Colors.Gray, propertyChanged: OnTrackColorChanged);
    public static readonly BindableProperty HighlightColorProperty =
        BindableProperty.Create(nameof(HighlightColor), typeof(Color), typeof(RangeSlider), Colors.DodgerBlue, propertyChanged: OnHighlightColorChanged);


    // Properties
    public int Minimum { get => (int)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public int Maximum { get => (int)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public int LowerValue { get => (int)GetValue(LowerValueProperty); set => SetValue(LowerValueProperty, value); }
    public int UpperValue { get => (int)GetValue(UpperValueProperty); set => SetValue(UpperValueProperty, value); }
    public double ThumbSize { get => (double)GetValue(ThumbSizeProperty); set => SetValue(ThumbSizeProperty, value); }
    public double TrackHeight { get => (double)GetValue(TrackHeightProperty); set => SetValue(TrackHeightProperty, value); }
    public Color TrackColor { get => (Color)GetValue(TrackColorProperty); set => SetValue(TrackColorProperty, value); }
    public Color HighlightColor { get => (Color)GetValue(HighlightColorProperty); set => SetValue(HighlightColorProperty, value); }

    public void RaiseRangeChanged()
        => RangeChanged?.Invoke(this, EventArgs.Empty);

    // Visual Elements
    private readonly BoxView _track = new() { HeightRequest = 4, CornerRadius = 2 };
    private readonly BoxView _highlight = new() { HeightRequest = 4, CornerRadius = 2 };
    private readonly Border _lowerThumb = new() { Background = Colors.White, Stroke = Colors.Black, StrokeThickness = 1 };
    private readonly Border _upperThumb = new() { Background = Colors.White, Stroke = Colors.Black, StrokeThickness = 1 };
    // Transparent hit-extension areas above each thumb (covers chart area above slider).
    // ContentView is used instead of BoxView: BoxView with null Color renders opaque on iOS.
    private readonly ContentView _lowerHitArea = new();
    private readonly ContentView _upperHitArea = new();
    private readonly AbsoluteLayout _layout = new();
    private bool _isInitialized;
    private bool _isDragging;

    public RangeSlider()
    {
        _layout.Clip = null; // Disable clipping to prevent flicker

        UpdateThumbSize();
        UpdateTrackHeight();
        UpdateTrackColors();

        AbsoluteLayout.SetLayoutFlags(_track, AbsoluteLayoutFlags.None);
        AbsoluteLayout.SetLayoutFlags(_highlight, AbsoluteLayoutFlags.None);
        AbsoluteLayout.SetLayoutFlags(_lowerThumb, AbsoluteLayoutFlags.None);
        AbsoluteLayout.SetLayoutFlags(_upperThumb, AbsoluteLayoutFlags.None);
        AbsoluteLayout.SetLayoutFlags(_lowerHitArea, AbsoluteLayoutFlags.None);
        AbsoluteLayout.SetLayoutFlags(_upperHitArea, AbsoluteLayoutFlags.None);

        _layout.Children.Add(_track);
        _layout.Children.Add(_highlight);
        _layout.Children.Add(_lowerThumb);
        _layout.Children.Add(_upperThumb);
        _layout.Children.Add(_lowerHitArea);
        _layout.Children.Add(_upperHitArea);

        Content = _layout;

        // AppThemeBinding resolves after the constructor on iOS — re-apply colors once loaded
        // and whenever the system theme changes so the slider isn't stuck black.
        Loaded += (s, e) =>
        {
            UpdateTrackColors();
            UpdateLayoutPositions();
        };
        if (Application.Current != null)
            Application.Current.RequestedThemeChanged += (s, e) => UpdateTrackColors();

        // Gesture recognizers on thumb visuals
        var lowerPan = new PanGestureRecognizer();
        lowerPan.PanUpdated += OnLowerPan;
        _lowerThumb.GestureRecognizers.Add(lowerPan);

        var upperPan = new PanGestureRecognizer();
        upperPan.PanUpdated += OnUpperPan;
        _upperThumb.GestureRecognizers.Add(upperPan);

        // Extended hit area above each thumb (transparent, same pan handlers)
        var lowerExtPan = new PanGestureRecognizer();
        lowerExtPan.PanUpdated += OnLowerPan;
        _lowerHitArea.GestureRecognizers.Add(lowerExtPan);

        var upperExtPan = new PanGestureRecognizer();
        upperExtPan.PanUpdated += OnUpperPan;
        _upperHitArea.GestureRecognizers.Add(upperExtPan);
    }

    // Property changed handlers
    private static void OnThumbSizeChanged(BindableObject bindable, object oldVal, object newVal)
        => ((RangeSlider)bindable).UpdateThumbSize();
    private static void OnTrackHeightChanged(BindableObject bindable, object oldVal, object newVal)
        => ((RangeSlider)bindable).UpdateTrackHeight();
    private static void OnTrackColorChanged(BindableObject bindable, object oldVal, object newVal)
        => ((RangeSlider)bindable).UpdateTrackColors();
    private static void OnHighlightColorChanged(BindableObject bindable, object oldVal, object newVal)
        => ((RangeSlider)bindable).UpdateTrackColors();
    private static void OnMinimumChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var slider = (RangeSlider)bindable;
        int min = (int)newValue;
        if (slider.LowerValue < min) slider.LowerValue = min;
        if (slider.UpperValue < slider.LowerValue + 1) slider.UpperValue = slider.LowerValue + 1;
        slider.UpdateLayoutPositions();
        slider.RangeChanged?.Invoke(slider, EventArgs.Empty);
    }
    private static void OnMaximumChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var slider = (RangeSlider)bindable;
        int oldMax = (int)oldValue;
        int newMax = (int)newValue;
        bool upperWasAtMax = slider.UpperValue == oldMax;
        if (upperWasAtMax) slider.UpperValue = newMax;
        if (slider.UpperValue > newMax) slider.UpperValue = newMax;
        if (slider.LowerValue > slider.UpperValue - 1) slider.LowerValue = slider.UpperValue - 1;
        slider.UpdateLayoutPositions();
        slider.RangeChanged?.Invoke(slider, EventArgs.Empty);
    }
    private static void OnLowerValueChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var slider = (RangeSlider)bindable;
        int lower = (int)newValue;
        int corrected = Math.Clamp(lower, slider.Minimum, slider.Maximum);
        if (corrected >= slider.UpperValue) corrected = slider.UpperValue - 1;
        if (corrected != lower) { slider.LowerValue = corrected; return; }
        slider.UpdateLayoutPositions();
        slider.RangeChanged?.Invoke(slider, EventArgs.Empty);
    }
    private static void OnUpperValueChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var slider = (RangeSlider)bindable;
        int upper = (int)newValue;
        int corrected = Math.Clamp(upper, slider.Minimum, slider.Maximum);
        if (corrected <= slider.LowerValue) corrected = slider.LowerValue + 1;
        if (corrected != upper) { slider.UpperValue = corrected; return; }
        slider.UpdateLayoutPositions();
        slider.RangeChanged?.Invoke(slider, EventArgs.Empty);
    }

    // UI updates
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
    private void UpdateTrackColors()
    {
        _track.Color = TrackColor;
        _highlight.Color = HighlightColor;
    }

    // Pan logic
    private double _dragStartLowerPos;
    private double _dragStartUpperPos;

    private void OnLowerPan(object? sender, PanUpdatedEventArgs e)
    {
        if (!_isInitialized || _layout.Width <= 0) return;
        double trackWidth = _layout.Width - ThumbSize;

        if (e.StatusType == GestureStatus.Started)
        {
            _isDragging = true;
            _dragStartLowerPos = ValueToPosition(LowerValue, trackWidth);
        }
        else if (e.StatusType == GestureStatus.Running)
        {
            double upperPos = ValueToPosition(UpperValue, trackWidth);
            double newPos = Math.Clamp(_dragStartLowerPos + e.TotalX, 0, upperPos - 1);
            int newValue = PositionToValue(newPos, trackWidth);

            if (newValue != LowerValue)
            {
                LowerValue = newValue;
                UpdateThumbPositionsDuringDrag(); // Only update thumb positions, not full layout
            }
        }
        else if (e.StatusType == GestureStatus.Completed || e.StatusType == GestureStatus.Canceled)
        {
            _isDragging = false;
            UpdateLayoutPositions(); // Only update full layout when drag is done
        }
    }

    private void OnUpperPan(object? sender, PanUpdatedEventArgs e)
    {
        if (!_isInitialized || _layout.Width <= 0) return;
        double trackWidth = _layout.Width - ThumbSize;

        if (e.StatusType == GestureStatus.Started)
        {
            _isDragging = true;
            _dragStartUpperPos = ValueToPosition(UpperValue, trackWidth);
        }
        else if (e.StatusType == GestureStatus.Running)
        {
            double lowerPos = ValueToPosition(LowerValue, trackWidth);
            double newPos = Math.Clamp(_dragStartUpperPos + e.TotalX, lowerPos + 1, trackWidth);
            int newValue = PositionToValue(newPos, trackWidth);

            if (newValue != UpperValue)
            {
                UpperValue = newValue;
                UpdateThumbPositionsDuringDrag(); // Only update thumb positions, not full layout
            }
        }
        else if (e.StatusType == GestureStatus.Completed || e.StatusType == GestureStatus.Canceled)
        {
            _isDragging = false;
            UpdateLayoutPositions(); // Only update full layout when drag is done
        }
    }

    private void UpdateThumbPositionsDuringDrag()
    {
        if (!_isInitialized || _layout.Width <= 0 || _layout.Height <= 0) return;

        double trackWidth = _layout.Width - ThumbSize;
        double lowerPos = ValueToPosition(LowerValue, trackWidth);
        double upperPos = ValueToPosition(UpperValue, trackWidth);
        double centerY = _layout.Height - ThumbSize;
        double trackCenterY = _layout.Height - ThumbSize / 2 - TrackHeight / 2;

        // Batch UI updates
        MainThread.BeginInvokeOnMainThread(() =>
        {
            AbsoluteLayout.SetLayoutBounds(_lowerThumb, new Rect(lowerPos, centerY, ThumbSize, ThumbSize));
            AbsoluteLayout.SetLayoutBounds(_upperThumb, new Rect(upperPos, centerY, ThumbSize, ThumbSize));
            AbsoluteLayout.SetLayoutBounds(_highlight, new Rect(lowerPos + ThumbSize / 2, trackCenterY, upperPos - lowerPos, TrackHeight));
            AbsoluteLayout.SetLayoutBounds(_lowerHitArea, new Rect(lowerPos, 0, ThumbSize, centerY));
            AbsoluteLayout.SetLayoutBounds(_upperHitArea, new Rect(upperPos, 0, ThumbSize, centerY));
        });
    }

    // Layout
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width > 0 && height > 0 && _layout.Width > 0 && _layout.Height > 0)
        {
            if (!_isInitialized) _isInitialized = true;
            UpdateLayoutPositions();
        }
    }

    private void UpdateLayoutPositions()
    {
        if (this.Width <= 0 || this.Height <= 0) return;
        double trackWidth = this.Width - ThumbSize;
        double lowerPos = ValueToPosition(LowerValue, trackWidth);
        double upperPos = ValueToPosition(UpperValue, trackWidth);
        double centerY = this.Height - ThumbSize;
        double trackCenterY = this.Height - ThumbSize / 2 - TrackHeight / 2;
        AbsoluteLayout.SetLayoutBounds(_track, new Rect(0, trackCenterY, this.Width, TrackHeight));
        AbsoluteLayout.SetLayoutBounds(_highlight, new Rect(lowerPos + ThumbSize / 2, trackCenterY, upperPos - lowerPos, TrackHeight));
        AbsoluteLayout.SetLayoutBounds(_lowerThumb, new Rect(lowerPos, centerY, ThumbSize, ThumbSize));
        AbsoluteLayout.SetLayoutBounds(_upperThumb, new Rect(upperPos, centerY, ThumbSize, ThumbSize));
        AbsoluteLayout.SetLayoutBounds(_lowerHitArea, new Rect(lowerPos, 0, ThumbSize, centerY));
        AbsoluteLayout.SetLayoutBounds(_upperHitArea, new Rect(upperPos, 0, ThumbSize, centerY));
    }

    public new void ForceLayout()
    {
        if (_layout.Width > 0 && _layout.Height > 0)
        {
            _isInitialized = true;
            UpdateLayoutPositions();
        }
        else
        {
            Dispatcher.Dispatch(() =>
            {
                if (_layout.Width > 0 && _layout.Height > 0)
                {
                    _isInitialized = true;
                    UpdateLayoutPositions();
                }
            });
        }
    }

    // Helpers
    private double ValueToPosition(int value, double trackWidth)
    {
        double range = Maximum - Minimum;
        return range <= 0 ? 0 : (value - Minimum) / range * trackWidth;
    }
    private int PositionToValue(double pos, double trackWidth)
    {
        if (trackWidth <= 0) return Minimum;
        double ratio = Math.Clamp(pos / trackWidth, 0, 1);
        return Minimum + (int)Math.Round(ratio * (Maximum - Minimum));
    }

    protected override void OnPropertyChanged(string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (_isDragging && (propertyName == LowerValueProperty.PropertyName || propertyName == UpperValueProperty.PropertyName))
        {
            // Skip full layout during drag
            return;
        }
    }
}
