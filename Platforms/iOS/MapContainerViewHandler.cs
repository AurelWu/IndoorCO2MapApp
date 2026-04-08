using Foundation;
using Microsoft.Maui.Handlers;
using UIKit;

namespace IndoorCO2MapAppV2.Platforms.iOS.Handlers;

public class MapContainerViewHandler : ContentViewHandler
{
    protected override Microsoft.Maui.Platform.ContentView CreatePlatformView()
        => new TouchInterceptView();
}

file sealed class TouchInterceptView : Microsoft.Maui.Platform.ContentView
{
    public override void TouchesBegan(NSSet touches, UIEvent? evt)
    {
        SetParentScrollEnabled(false);
        base.TouchesBegan(touches, evt);
    }

    public override void TouchesEnded(NSSet touches, UIEvent? evt)
    {
        SetParentScrollEnabled(true);
        base.TouchesEnded(touches, evt);
    }

    public override void TouchesCancelled(NSSet touches, UIEvent? evt)
    {
        SetParentScrollEnabled(true);
        base.TouchesCancelled(touches, evt);
    }

    private void SetParentScrollEnabled(bool enabled)
    {
        UIResponder? r = NextResponder;
        while (r != null)
        {
            if (r is UIScrollView sv) { sv.ScrollEnabled = enabled; return; }
            r = r.NextResponder;
        }
    }
}
