using System;
using Android.Content;
using Android.Views;
using Android.Graphics;

using AView = Android.Views.View;
using AColor = Android.Graphics.Color;
using APaint = Android.Graphics.Paint;
using ACanvas = Android.Graphics.Canvas;

namespace IndoorCO2MapAppV2.Platforms.Android
{
    public class RangeSliderView : AView
    {
        public Action<int, int>? OnRangeChangedByUser;

        private readonly APaint _trackPaint = new(PaintFlags.AntiAlias);
        private readonly APaint _highlightPaint = new(PaintFlags.AntiAlias);
        private readonly APaint _thumbPaint = new(PaintFlags.AntiAlias);
        private readonly APaint _thumbBorderPaint = new(PaintFlags.AntiAlias);

        private float _thumbRadius = 24f;
        private float _trackHeight = 8f;
        private float _padding;

        private int _minimum;
        private int _maximum;
        private int _lowerValue;
        private int _upperValue;

        private float _lowerX;
        private float _upperX;

        private bool _dragLower;
        private bool _dragUpper;

        public RangeSliderView(Context context) : base(context)
        {
            _trackPaint.Color = AColor.Gray;
            _highlightPaint.Color = AColor.Blue;

            _thumbPaint.Color = AColor.White;

            _thumbBorderPaint.Color = AColor.Black;
            _thumbBorderPaint.StrokeWidth = 2;
            _thumbBorderPaint.SetStyle(APaint.Style.Stroke);

            _padding = _thumbRadius;
        }

        public void UpdateMinimum(int v) { _minimum = v; UpdateThumbs(); }
        public void UpdateMaximum(int v) { _maximum = v; UpdateThumbs(); }
        public void UpdateLowerValue(int v) { _lowerValue = v; UpdateThumbs(); }
        public void UpdateUpperValue(int v) { _upperValue = v; UpdateThumbs(); }

        public void UpdateThumbSize(float size)
        {
            _thumbRadius = size / 2f;
            _padding = _thumbRadius;
            UpdateThumbs();
        }

        public void UpdateTrackHeight(float h) => _trackHeight = h;
        public void UpdateTrackColor(AColor c) => _trackPaint.Color = c;
        public void UpdateHighlightColor(AColor c) => _highlightPaint.Color = c;

        private void UpdateThumbs()
        {
            if (Width <= 0) return;

            float width = Width - 2 * _padding;
            float range = Math.Max(1, _maximum - _minimum);

            _lowerX = _padding + width * (_lowerValue - _minimum) / range;
            _upperX = _padding + width * (_upperValue - _minimum) / range;

            Invalidate();
        }

        protected override void OnDraw(ACanvas canvas)
        {
            float cy = Height / 2f;

            canvas.DrawRect(_padding, cy - _trackHeight / 2,
                            Width - _padding, cy + _trackHeight / 2, _trackPaint);

            canvas.DrawRect(_lowerX, cy - _trackHeight / 2,
                            _upperX, cy + _trackHeight / 2, _highlightPaint);

            canvas.DrawCircle(_lowerX, cy, _thumbRadius, _thumbPaint);
            canvas.DrawCircle(_lowerX, cy, _thumbRadius, _thumbBorderPaint);

            canvas.DrawCircle(_upperX, cy, _thumbRadius, _thumbPaint);
            canvas.DrawCircle(_upperX, cy, _thumbRadius, _thumbBorderPaint);
        }

        public override bool OnTouchEvent(MotionEvent e)
        {
            float x = e.GetX();
            float touch = _thumbRadius * 2;

            switch (e.Action)
            {
                case MotionEventActions.Down:
                    _dragLower = Math.Abs(x - _lowerX) < touch;
                    _dragUpper = Math.Abs(x - _upperX) < touch;
                    return _dragLower || _dragUpper;

                case MotionEventActions.Move:
                    if (!_dragLower && !_dragUpper) return false;

                    float width = Width - 2 * _padding;
                    float ratio = (Math.Clamp(x, _padding, Width - _padding) - _padding) / width;
                    int value = _minimum + (int)Math.Round(ratio * (_maximum - _minimum));

                    if (_dragLower && value < _upperValue) _lowerValue = value;
                    if (_dragUpper && value > _lowerValue) _upperValue = value;

                    UpdateThumbs();
                    OnRangeChangedByUser?.Invoke(_lowerValue, _upperValue);
                    return true;

                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    _dragLower = _dragUpper = false;
                    return true;
            }

            return base.OnTouchEvent(e);
        }
    }
}
