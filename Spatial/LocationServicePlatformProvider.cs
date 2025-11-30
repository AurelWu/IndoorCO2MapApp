namespace IndoorCO2MapAppV2.Spatial
{
    public static class LocationServicePlatformProvider
    {
        private static ILocationService? _instance;

        public static ILocationService CreateOrUse()
        {
            if (_instance != null)
                return _instance;

#if ANDROID
            _instance = new LocationServiceAndroid();
#elif IOS
            _instance = new LocationServiceApple();
#else
            _instance = new LocationServiceDefault();
#endif
            return _instance;
        }
    }
}