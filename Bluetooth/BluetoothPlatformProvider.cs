

namespace IndoorCO2MapAppV2.Bluetooth
{
    public static class BluetoothPlatformProvider
    {
        public static IBluetoothHelper Create()
        {
#if ANDROID
        return new BluetoothHelperAndroid();
#elif IOS
            return new BluetoothHelperApple();
#else
        return new BluetoothHelperDefault();   
#endif
        }
    }
}