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
        BindableProperty.Create(nameof(LowerValue), typeof(int), typeof(RangeSlider), 20, BindingMode.TwoWay);

    public static readonly BindableProperty UpperValueProperty =
        BindableProperty.Create(nameof(UpperValue), typeof(int), typeof(RangeSlider), 80, BindingMode.TwoWay);

    public int Minimum { get => (int)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public int Maximum { get => (int)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public int LowerValue { get => (int)GetValue(LowerValueProperty); set => SetValue(LowerValueProperty, value); }
    public int UpperValue { get => (int)GetValue(UpperValueProperty); set => SetValue(UpperValueProperty, value); }


    // Visual Elements
    private readonly BoxView _track = new() { Color = Colors.Gray, HeightRequest = 4, CornerRadius = 2 };
    private readonly BoxView _highlight = new() { Color = Colors.DodgerBlue, HeightRequest = 4, CornerRadius = 2 };

    private readonly Border _lowerThumb = new()
    {
        Background = Colors.White,
        Stroke = Colors.Black,
        StrokeThickness = 1,
        WidthRequest = 24,
        HeightRequest = 24,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center,
        StrokeShape = new RoundRectangle { CornerRadius = 12 }
    };

    private readonly Border _upperThumb = new()
    {
        Background = Colors.White,
        Stroke = Colors.Black,
        StrokeThickness = 1,
        WidthRequest = 24,
        HeightRequest = 24,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center,
        StrokeShape = new RoundRectangle { CornerRadius = 12 }
    };

    private readonly AbsoluteLayout _layout = [];

    public RangeSlider()
    {
        // Add elements to layout
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


    // --------------------------------------------------------------------
    // Pan logic
    // --------------------------------------------------------------------
    private double _startLowerX;
    private double _startUpperX;

    private void OnLowerPan(object? sender, PanUpdatedEventArgs e)
    {
        if (e.StatusType == GestureStatus.Started)
            _startLowerX = _lowerThumb.TranslationX;

        if (e.StatusType == GestureStatus.Running)
        {
            double trackWidth = TrackWidth();
            double newX = _startLowerX + e.TotalX;

            double maxX = _upperThumb.TranslationX - ThumbWidth();
            newX = Math.Clamp(newX, 0, maxX);

            LowerValue = PositionToValue(newX, trackWidth);
            LowerValue = Math.Min(LowerValue, UpperValue);

            UpdateLayoutPositions();
        }
    }

    private void OnUpperPan(object? sender, PanUpdatedEventArgs e)
    {
        if (e.StatusType == GestureStatus.Started)
            _startUpperX = _upperThumb.TranslationX;

        if (e.StatusType == GestureStatus.Running)
        {
            double trackWidth = TrackWidth();
            double newX = _startUpperX + e.TotalX;

            double minX = _lowerThumb.TranslationX + ThumbWidth();
            double maxX = trackWidth;

            newX = Math.Clamp(newX, minX, maxX);

            UpperValue = PositionToValue(newX, trackWidth);
            UpperValue = Math.Max(UpperValue, LowerValue);

            UpdateLayoutPositions();
        }
    }

    // --------------------------------------------------------------------
    // Layout + rendering
    // --------------------------------------------------------------------
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        UpdateLayoutPositions();
    }

    private void UpdateLayoutPositions()
    {
        if (_layout.Width <= 0) return;

        double trackWidth = TrackWidth();
        double lw = ValueToPosition(LowerValue, trackWidth);
        double uw = ValueToPosition(UpperValue, trackWidth);

        // Position thumbs
        _lowerThumb.TranslationX = lw;
        _upperThumb.TranslationX = uw;

        // Track
        AbsoluteLayout.SetLayoutBounds(_track,
            new Rect(0, Height / 2 - 2, _layout.Width, 4));

        // Highlight
        AbsoluteLayout.SetLayoutBounds(_highlight,
            new Rect(lw + ThumbWidth() / 2, Height / 2 - 2,
                     uw - lw, 4));
    }

    // --------------------------------------------------------------------
    // Helpers
    // --------------------------------------------------------------------
    private double ThumbWidth() => _lowerThumb.WidthRequest;

    private double TrackWidth()
        => Math.Max(0, _layout.Width - ThumbWidth());

    private double ValueToPosition(int value, double trackWidth)
    {
        double range = Maximum - Minimum;
        double ratio = (value - Minimum) / range;
        return ratio * trackWidth;
    }

    private int PositionToValue(double pos, double trackWidth)
    {
        if (trackWidth <= 0) return Minimum;

        double ratio = Math.Clamp(pos / trackWidth, 0, 1);
        int raw = Minimum + (int)Math.Round(ratio * (Maximum - Minimum));
        return raw;
    }
}
