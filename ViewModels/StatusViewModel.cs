using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Timers;

namespace IndoorCO2MapAppV2.ViewModels
{
    public class StatusViewModel : INotifyPropertyChanged
    {
        // Singleton instance for app-wide usage
        private static StatusViewModel? _instance;
        public static StatusViewModel Instance => _instance ??= new StatusViewModel();

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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        private readonly System.Timers.Timer _statusTimer;

        // Private constructor for singleton
        private StatusViewModel()
        {
            // Example: update every second
            _statusTimer = new System.Timers.Timer(1000);
            _statusTimer.Elapsed += StatusTimer_Elapsed;
            _statusTimer.AutoReset = true;
            _statusTimer.Start();
        }

        private void StatusTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // This runs on a background thread, so use MainThread for UI updates
            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateStatus();
            });
        }

        private void UpdateStatus()
        {
            // Here you check your actual hardware status
            var bluetoothHelper = IndoorCO2MapAppV2.Bluetooth.BluetoothPlatformProvider.Create();

            IsBluetoothOn = bluetoothHelper.CheckIfBTEnabled();
            BluetoothPermissionGranted = bluetoothHelper.CheckStatus();

            // Example GPS checks (replace with actual implementation)
            IsGpsOn = true; // TODO: query device GPS
            GpsPermissionGranted = true; // TODO: query permissions
        }
    }
}
