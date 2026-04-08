using CommunityToolkit.Maui;
using IndoorCO2MapAppV2.Bluetooth;
#if !WINDOWS
using SkiaSharp.Views.Maui.Controls.Hosting;
#endif
using IndoorCO2MapAppV2.Controls;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Spatial;
using Microsoft.Extensions.Logging;
#if ANDROID
using IndoorCO2MapAppV2.Platforms.Android.Handlers;
#endif
#if IOS
using IndoorCO2MapAppV2.Platforms.iOS.Handlers;
#endif


namespace IndoorCO2MapAppV2
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            UserSettings.Load();

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
#if ANDROID
                .ConfigureMauiHandlers(handlers =>
                 {
                     handlers.AddHandler(typeof(RangeSlider), typeof(RangeSliderHandler));
                 })
#endif
#if IOS
                .ConfigureMauiHandlers(handlers =>
                 {
                     handlers.AddHandler(typeof(MapContainerView), typeof(MapContainerViewHandler));
                 })
#endif
                .UseMauiCommunityToolkit()
#if !WINDOWS
                .UseSkiaSharp()
#endif
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });
            
#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
