using CommunityToolkit.Maui;
using IndoorCO2MapAppV2.CO2Monitors;

namespace IndoorCO2MapAppV2.Controls
{
    public partial class LineChartView : GraphicsView
    {
        private readonly LineChartDrawable _drawable;

        // --- Single-series BindableProperty (existing) ---
        public static readonly BindableProperty ReadingsProperty =
            BindableProperty.Create(
                nameof(Readings),
                typeof(List<CO2Reading>),
                typeof(LineChartView),
                null,
                propertyChanged: (bindable, _, newVal) =>
                {
                    if (bindable is LineChartView chart && newVal is List<CO2Reading> data && data.Count > 0)
                        chart.SetData(data, 0, data.Count - 1);
                });

        public List<CO2Reading>? Readings
        {
            get => (List<CO2Reading>?)GetValue(ReadingsProperty);
            set => SetValue(ReadingsProperty, value);
        }

        // --- Multi-series BindableProperty (new) ---
        public static readonly BindableProperty MultiSeriesReadingsProperty =
            BindableProperty.Create(
                nameof(MultiSeriesReadings),
                typeof(List<List<CO2Reading>>),
                typeof(LineChartView),
                null,
                propertyChanged: (bindable, _, newVal) =>
                {
                    if (bindable is LineChartView chart)
                        chart.SetMultiSeriesData(newVal as List<List<CO2Reading>>);
                });

        public List<List<CO2Reading>>? MultiSeriesReadings
        {
            get => (List<List<CO2Reading>>?)GetValue(MultiSeriesReadingsProperty);
            set => SetValue(MultiSeriesReadingsProperty, value);
        }

        public LineChartView()
        {
            _drawable = new LineChartDrawable();
            Drawable = _drawable;
            UpdateThemeColor();
            if (Application.Current != null)
                Application.Current.RequestedThemeChanged += (_, _) => { UpdateThemeColor(); Invalidate(); };
        }

        internal void SetData(List<CO2Reading> newData, int trimStart, int trimEnd)
        {
            int[] ints = new int[newData.Count];
            for (int i = 0; i < newData.Count; i++)
            {
                ints[i] = newData[i].Ppm;
            }
            _drawable.MultiSeriesData = null;
            _drawable.Data = ints;
            _drawable._trimStart = trimStart;
            _drawable._trimEnd = trimEnd;

            Invalidate();
        }

        internal void SetMultiSeriesData(List<List<CO2Reading>>? data)
        {
            if (data == null || data.Count == 0)
            {
                _drawable.MultiSeriesData = null;
            }
            else
            {
                _drawable.MultiSeriesData = data
                    .Select(s => s.Select(r => (int)r.Ppm).ToArray())
                    .ToArray();
            }
            _drawable.Data = Array.Empty<int>();
            Invalidate();
        }

        /// <summary>
        /// Clears the chart completely.
        /// </summary>
        public void Clear()
        {
            _drawable.MultiSeriesData = null;
            _drawable.Data = Array.Empty<int>();
            _drawable._trimStart = 0;
            _drawable._trimEnd = 0;
            Invalidate();
        }

        private void UpdateThemeColor()
        {
            _drawable.LineColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Colors.White
                : Colors.Black;
        }

        private class LineChartDrawable : IDrawable
        {
            public int[] Data { get; set; } = [];
            public int[][]? MultiSeriesData { get; set; } = null;
            public Color LineColor { get; set; } = Colors.Gray;

            public int _trimStart;
            public int _trimEnd;

            private static readonly Color[] SeriesPalette = new[]
            {
                Color.FromArgb("#1565C0"), Color.FromArgb("#C62828"),
                Color.FromArgb("#2E7D32"), Color.FromArgb("#F57F17"),
                Color.FromArgb("#6A1B9A"), Color.FromArgb("#00695C"),
                Color.FromArgb("#E65100"), Color.FromArgb("#AD1457"),
                Color.FromArgb("#0277BD"), Color.FromArgb("#558B2F"),
            };

            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                if (MultiSeriesData != null && MultiSeriesData.Length > 0)
                    DrawMultiSeries(canvas, dirtyRect);
                else
                    DrawSingleSeries(canvas, dirtyRect);
            }

            private void DrawSingleSeries(ICanvas canvas, RectF dirtyRect)
            {
                int width = (int)dirtyRect.Width;
                int height = (int)dirtyRect.Height;
                int paddingLeft = 25;
                int paddingRight = 25;
                int paddingTop = 0;
                int paddingBottom = 0;

                int usableWidth = width - paddingLeft - paddingRight;
                int usableHeight = height - paddingTop - paddingBottom;

                if (Data.Length < 2)
                {
                    // Draw axes skeleton then centered placeholder
                    canvas.StrokeColor = LineColor;
                    canvas.StrokeSize = 2;
                    canvas.StrokeDashPattern = [1, 0];
                    canvas.DrawLine(paddingLeft, paddingTop, paddingLeft, height - paddingBottom);
                    canvas.DrawLine(paddingLeft, height - paddingBottom, width - paddingRight, height - paddingBottom);
                    canvas.FontColor = LineColor;
                    canvas.FontSize = 13;
                    canvas.DrawString("Waiting for data…", paddingLeft, 0, usableWidth, height, HorizontalAlignment.Center, VerticalAlignment.Center);
                    return;
                }

                canvas.FillColor = LineColor;

                float xScale = usableWidth / (float)(Data.Length - 1);
                float maxValue = GetMaxDataValue();
                float minValue = 300;
                float yScale = usableHeight / (maxValue - minValue);

                canvas.StrokeColor = LineColor;
                canvas.StrokeSize = 2;
                for (int i = 0; i < Data.Length; i++)
                {
                    float x = paddingLeft + i * xScale;
                    if (i % 5 == 0)
                        canvas.DrawLine(x, height - paddingBottom, x, height - paddingBottom - 10);
                    else
                        canvas.DrawLine(x, height - paddingBottom, x, height - paddingBottom - 5);
                }

                canvas.StrokeColor = LineColor;
                canvas.FontColor = LineColor;
                canvas.FontSize = 8;
                canvas.StrokeSize = 2;
                for (int i = 400; i <= maxValue; i += 400)
                {
                    float y = height - paddingBottom - (i - minValue) * yScale;
                    canvas.DrawLine(paddingLeft, y, paddingLeft + 10, y);
                    canvas.DrawString(i.ToString(), paddingLeft + 12, y - 5, 300, 10, HorizontalAlignment.Left, VerticalAlignment.Center);
                }

                if (Data.Length > 0)
                {
                    canvas.StrokeColor = LineColor;
                    canvas.StrokeSize = 2;
                    float prevX = paddingLeft;
                    float prevY = height - paddingBottom - (Data[0] - minValue) * yScale;

                    for (int i = 0; i < Data.Length; i++)
                    {
                        if (i < _trimStart + 1 || i > _trimEnd)
                        {
                            canvas.StrokeDashPattern = [1, 1];
                            canvas.StrokeColor = Color.FromRgb(155, 155, 155);
                            canvas.FillColor = Color.FromRgb(155, 155, 155);
                        }
                        else
                        {
                            canvas.StrokeDashPattern = [1, 0];
                            canvas.StrokeColor = LineColor;
                            canvas.FillColor = LineColor;
                        }
                        float x = paddingLeft + i * xScale;
                        float y = height - paddingBottom - (Data[i] - minValue) * yScale;
                        if (i > 0)
                            canvas.DrawLine(prevX, prevY, x, y);
                        prevX = x;
                        prevY = y;

                        if (i < _trimStart || i > _trimEnd)
                            canvas.FillColor = Color.FromRgb(155, 155, 155);
                        else
                            canvas.FillColor = LineColor;

                        if (Data.Length <= 25)
                            canvas.FillCircle(x, y, 5);
                        else if (Data.Length <= 50)
                            canvas.FillCircle(x, y, 3);
                        else
                            canvas.FillCircle(x, y, 2);
                    }
                }

                canvas.StrokeColor = LineColor;
                canvas.StrokeDashPattern = [1, 0];
                canvas.StrokeSize = 2;
                canvas.DrawLine(paddingLeft, paddingTop, paddingLeft, height - paddingBottom);
                canvas.DrawLine(paddingLeft, height - paddingBottom, width - paddingRight, height - paddingBottom);
            }

            private void DrawMultiSeries(ICanvas canvas, RectF dirtyRect)
            {
                int width = (int)dirtyRect.Width;
                int height = (int)dirtyRect.Height;
                const int paddingLeft = 25, paddingRight = 25, paddingTop = 0, paddingBottom = 0;
                int usableWidth = width - paddingLeft - paddingRight;
                int usableHeight = height - paddingTop - paddingBottom;

                float maxValue = GetMaxMultiSeriesValue();
                float minValue = 300;
                float yScale = usableHeight / (maxValue - minValue);

                // Y-axis labels
                canvas.StrokeColor = LineColor;
                canvas.FontColor = LineColor;
                canvas.FontSize = 8;
                canvas.StrokeSize = 2;
                for (int i = 400; i <= maxValue; i += 400)
                {
                    float y = height - paddingBottom - (i - minValue) * yScale;
                    canvas.DrawLine(paddingLeft, y, paddingLeft + 10, y);
                    canvas.DrawString(i.ToString(), paddingLeft + 12, y - 5, 300, 10, HorizontalAlignment.Left, VerticalAlignment.Center);
                }

                // Shared x-scale based on the longest series so shorter ones don't stretch to fill
                int maxLength = MultiSeriesData!.Max(s => s.Length);
                float xScale = maxLength > 1 ? usableWidth / (float)(maxLength - 1) : 0;

                // Draw each series
                for (int s = 0; s < MultiSeriesData!.Length; s++)
                {
                    int[] series = MultiSeriesData[s];
                    if (series.Length == 0) continue;

                    Color seriesColor = SeriesPalette[s % SeriesPalette.Length];
                    canvas.StrokeColor = seriesColor;
                    canvas.FillColor = seriesColor;
                    canvas.StrokeSize = 2;
                    canvas.StrokeDashPattern = [1, 0];

                    float prevX = paddingLeft;
                    float prevY = height - paddingBottom - (series[0] - minValue) * yScale;

                    for (int i = 0; i < series.Length; i++)
                    {
                        float x = paddingLeft + i * xScale;
                        float y = height - paddingBottom - (series[i] - minValue) * yScale;
                        if (i > 0)
                            canvas.DrawLine(prevX, prevY, x, y);
                        prevX = x;
                        prevY = y;

                        if (maxLength <= 25)
                            canvas.FillCircle(x, y, 4);
                        else if (maxLength <= 50)
                            canvas.FillCircle(x, y, 2);
                        else
                            canvas.FillCircle(x, y, 1);
                    }
                }

                // Axes
                canvas.StrokeColor = LineColor;
                canvas.StrokeDashPattern = [1, 0];
                canvas.StrokeSize = 2;
                canvas.DrawLine(paddingLeft, paddingTop, paddingLeft, height - paddingBottom);
                canvas.DrawLine(paddingLeft, height - paddingBottom, width - paddingRight, height - paddingBottom);
            }

            private float GetMaxDataValue()
            {
                if (Data.Length == 0)
                    return 1000;

                int max = Data[0];
                foreach (int value in Data)
                    if (value > max) max = value;

                return ((((max + 399) / 400) * 400) + 50);
            }

            private float GetMaxMultiSeriesValue()
            {
                int max = 1000;
                foreach (var series in MultiSeriesData!)
                    foreach (int v in series)
                        if (v > max) max = v;
                return ((((max + 399) / 400) * 400) + 50);
            }
        }
    }
}
