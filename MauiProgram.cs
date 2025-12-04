using CommunityToolkit.Maui;
using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.Bluetooth;
using Microsoft.Extensions.Logging;
using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.ExtensionMethods;


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
                .UseMauiCommunityToolkit()                
            // After initializing the .NET MAUI Community Toolkit, optionally add additional fonts

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
