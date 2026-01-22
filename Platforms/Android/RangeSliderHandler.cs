using Android.Content;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using IndoorCO2MapAppV2.Controls;
using IndoorCO2MapAppV2.Platforms.Android;

namespace IndoorCO2MapAppV2.Platforms.Android.Handlers
{
    public class RangeSliderHandler : ViewHandler<RangeSlider, RangeSliderView>
    {
        public static readonly PropertyMapper<RangeSlider, RangeSliderHandler> Mapper =
            new(ViewMapper)
            {
                [nameof(RangeSlider.Minimum)] = (h, v) => h.PlatformView?.UpdateMinimum(v.Minimum),
                [nameof(RangeSlider.Maximum)] = (h, v) => h.PlatformView?.UpdateMaximum(v.Maximum),
                [nameof(RangeSlider.LowerValue)] = (h, v) => h.PlatformView?.UpdateLowerValue(v.LowerValue),
                [nameof(RangeSlider.UpperValue)] = (h, v) => h.PlatformView?.UpdateUpperValue(v.UpperValue),
                [nameof(RangeSlider.ThumbSize)] = (h, v) => h.PlatformView?.UpdateThumbSize((float)v.ThumbSize),
                [nameof(RangeSlider.TrackHeight)] = (h, v) => h.PlatformView?.UpdateTrackHeight((float)v.TrackHeight),
                [nameof(RangeSlider.TrackColor)] = (h, v) => h.PlatformView?.UpdateTrackColor(v.TrackColor.ToPlatform()),
                [nameof(RangeSlider.HighlightColor)] = (h, v) => h.PlatformView?.UpdateHighlightColor(v.HighlightColor.ToPlatform())
            };

        public RangeSliderHandler() : base(Mapper) { }

        protected override RangeSliderView CreatePlatformView()
        {
            var view = new RangeSliderView(Context);

            view.OnRangeChangedByUser = (low, high) =>
            {
                if (VirtualView == null) return;

                VirtualView.LowerValue = low;
                VirtualView.UpperValue = high;
                VirtualView.RaiseRangeChanged();
            };

            return view;
        }
    }
}
