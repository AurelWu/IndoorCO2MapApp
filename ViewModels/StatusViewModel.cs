using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Spatial;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Timers;

namespace IndoorCO2MapAppV2.ViewModels
{
    public partial class StatusViewModel : INotifyPropertyChanged
    {
        // Singleton instance
        private static StatusViewModel? _instance;
        public static StatusViewModel Instance => _instance ??= new StatusViewModel();

        // --- Bluetooth ---
        private bool _isBluetoothOn;
        public bool IsBluetoothOn
        {
            get => _isBluetoothOn;
            set => SetProperty(ref _isBluetoothOn, value);
        }

        private bool _bluetoothPermissionGranted;
        public bool BluetoothPermissionGranted
        {
            get => _bluetoothPermissionGranted;
            set => SetProperty(ref _bluetoothPermissionGranted, value);
        }

        // --- GPS ---
        private bool _isGpsOn;
        public bool IsGpsOn
        {
            get => _isGpsOn;
            set => SetProperty(ref _isGpsOn, value);
        }

        private bool _gpsPermissionGranted;
        public bool GpsPermissionGranted
        {
            get => _gpsPermissionGranted;
            set => SetProperty(ref _gpsPermissionGranted, value);
        }

        // --- is GPS and BT Ready ---
        public bool AllReady => IsGpsOn && GpsPermissionGranted && IsBluetoothOn && BluetoothPermissionGranted;

        // --- PropertyChanged implementation ---
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            // Notify AllReady if any component changed
            if (propertyName != nameof(AllReady))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AllReady)));

            return true;
        }

        // --- Timer for periodic updates ---
        private readonly System.Timers.Timer _statusTimer;

        // Private constructor for singleton
        private StatusViewModel()
        {
            _statusTimer = new System.Timers.Timer(1000); 
            _statusTimer.Elapsed += StatusTimer_Elapsed;
            _statusTimer.AutoReset = true;
            _statusTimer.Start();
        }

        private void StatusTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // continuous updates on main thread
            MainThread.BeginInvokeOnMainThread(UpdateStatus);
        }

        private void UpdateStatus()
        {
            // --- Bluetooth status ---
            var bluetoothHelper = BluetoothPlatformProvider.CreateOrUse();
            IsBluetoothOn = bluetoothHelper.CheckIfBTEnabled();
            BluetoothPermissionGranted = bluetoothHelper.CheckPermissions();

            // --- GPS status ---
            var locationHelper = LocationServicePlatformProvider.CreateOrUse();
            UpdateGpsStatusAsync(locationHelper).SafeFireAndForget();
        }

        private async Task UpdateGpsStatusAsync(ILocationService locationHelper)
        {
            try
            {
                bool gpsOn = await locationHelper.IsGpsEnabledAsync();
                bool permGranted = await locationHelper.HasLocationPermissionAsync();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IsGpsOn = gpsOn;
                    GpsPermissionGranted = permGranted;
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IsGpsOn = false;
                    GpsPermissionGranted = false;
                });
                Console.WriteLine($"UpdateGpsStatusAsync failed: {ex.Message}");
            }
        }
    }
}
