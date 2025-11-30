namespace IndoorCO2MapAppV2.Bluetooth
{
    public static class BluetoothPlatformProvider
    {
        private static IBluetoothHelper? _instance;

        public static IBluetoothHelper CreateOrUse()
        {
            if (_instance != null)
                return _instance;

#if ANDROID
            _instance = new BluetoothHelperAndroid();
#elif IOS
            _instance = new BluetoothHelperApple();
#else
            _instance = new BluetoothHelperDefault();
#endif
            return _instance;
        }
    }
}
