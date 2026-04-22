using System.Runtime.CompilerServices;

namespace IndoorCO2MapAppV2
{
    public partial class AppShell : Shell
    {
        private bool _prewarmed;

        // Page types to JIT-warm on a background thread before main-thread inflation.
        // Debug pages are intentionally excluded — they are rarely used and not worth
        // the warmup cost on slow devices.
        private static readonly Type[] _preJitTypes =
        {
            typeof(Pages.BuildingMeasurementPage),
            typeof(Pages.HistoryPage),
            typeof(Pages.SettingsPage),
            typeof(Pages.NewsPage),
            typeof(Pages.StatisticsPage),
            typeof(Pages.MapPage),
            typeof(Pages.TransitMeasurementPage),
        };

        public AppShell()
        {
            InitializeComponent();
#if DEBUG
            Routing.RegisterRoute("sensorDebug",           typeof(Pages.DebugSensorPage));
            Routing.RegisterRoute("overpassDebug",         typeof(Pages.DebugOverpassRequestPage));
            Routing.RegisterRoute("buildingRecordingDebug",typeof(Pages.DebugBuildingRecordingPage));
            Routing.RegisterRoute("debugMainMenu",         typeof(Pages.DebugMainMenu));
#endif
        }

        protected override void OnNavigated(ShellNavigatedEventArgs args)
        {
            base.OnNavigated(args);
#if DEBUG
            // In Release AOT, JIT compilation is eliminated and page inflation drops to
            // ~100–400 ms — imperceptible, so warmup is unnecessary and just wastes time.
            if (!_prewarmed)
            {
                _prewarmed = true;
                _ = PrewarmPagesAsync();
            }
#endif
        }

        private async Task PrewarmPagesAsync()
        {
            await Task.Delay(2000); // let home page finish rendering

            // Phase 1 (background thread): force JIT compilation of page types.
            // RuntimeHelpers.RunClassConstructor compiles the static constructor and
            // warms the class loader without touching any MAUI/Android view APIs.
            await Task.Run(() =>
            {
                foreach (var t in _preJitTypes)
                    RuntimeHelpers.RunClassConstructor(t.TypeHandle);
            });

            // Phase 2 (main thread): XAML inflation for non-debug pages.
            // Task.Delay(500) gives the Choreographer time to render frames between
            // each page — Task.Yield() only reschedules the next message and cannot
            // allow vsync callbacks to fire in between.
            foreach (var shellItem in Items)
                foreach (var shellSection in shellItem.Items)
                    foreach (var shellContent in shellSection.Items)
                    {
                        if (shellContent.Route?.IndexOf("debug", StringComparison.OrdinalIgnoreCase) >= 0)
                            continue;

                        shellContent.ContentTemplate?.CreateContent();
                        await Task.Delay(500);
                    }
        }
    }
}
