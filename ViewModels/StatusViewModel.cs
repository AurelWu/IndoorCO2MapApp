using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.Enumerations;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Timers;

namespace IndoorCO2MapAppV2.ViewModels
{
    public partial class StatusViewModel : INotifyPropertyChanged
    {
        // Singleton instance
        private static StatusViewModel? _instance;
        public static StatusViewModel Instance => _instance ??= new StatusViewModel();

        // --- Initializing flag (true until first status check completes) ---
        private bool _isInitializing = true;
        public bool IsInitializing
        {
            get => _isInitializing;
            private set => SetProperty(ref _isInitializing, value);
        }

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

        // --- Chip colors/symbols (grey while initializing) ---
        private static readonly Color ColorReady   = Color.FromArgb("#4CAF50");
        private static readonly Color ColorNotReady = Color.FromArgb("#F44336");
        private static readonly Color ColorChecking = Color.FromArgb("#9E9E9E");

        private Color ChipColor(bool value) => IsInitializing ? ColorChecking : (value ? ColorReady : ColorNotReady);
        private string ChipSymbol(bool value) => IsInitializing ? "…" : (value ? "✓" : "✗");

        public Color GpsStatusColor         => ChipColor(IsGpsOn);
        public Color GpsPermissionColor     => ChipColor(GpsPermissionGranted);
        public Color BtStatusColor          => ChipColor(IsBluetoothOn);
        public Color BtPermissionColor      => ChipColor(BluetoothPermissionGranted);

        public string GpsStatusSymbol       => ChipSymbol(IsGpsOn);
        public string GpsPermissionSymbol   => ChipSymbol(GpsPermissionGranted);
        public string BtStatusSymbol        => ChipSymbol(IsBluetoothOn);
        public string BtPermissionSymbol    => ChipSymbol(BluetoothPermissionGranted);

        // --- App status ticker ---
        private static readonly HttpClient _statusHttpClient = new();
        private string? _statusMessage;
        public string? StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (_statusMessage == value) return;
                _statusMessage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusMessage)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasStatusMessage)));
            }
        }
        public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);

        public static async Task FetchAppStatusAsync()
        {
            await FetchInternalAsync(clearOnFailure: true);
        }

        public static void StartPeriodicRefresh()
        {
            _ = PeriodicRefreshLoopAsync();
        }

        private static async Task FetchInternalAsync(bool clearOnFailure)
        {
            try
            {
                var text = (await _statusHttpClient.GetStringAsync("https://indoorco2map.com/appstatus.txt")).Trim();
                Instance.StatusMessage = text.Equals("ok", StringComparison.OrdinalIgnoreCase) ? null : text;
            }
            catch
            {
                if (clearOnFailure)
                    Instance.StatusMessage = null;
                // else: keep previous state
            }
        }

        private static async Task PeriodicRefreshLoopAsync()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(15));
                await FetchInternalAsync(clearOnFailure: false);
            }
        }

        // --- PropertyChanged implementation ---
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            // Notify AllReady and chip display properties whenever any component changes
            if (propertyName != nameof(AllReady))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AllReady)));

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GpsStatusColor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GpsPermissionColor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BtStatusColor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BtPermissionColor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GpsStatusSymbol)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GpsPermissionSymbol)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BtStatusSymbol)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BtPermissionSymbol)));

            return true;
        }

        // --- Timer for periodic updates ---
        private readonly System.Timers.Timer _statusTimer;

        // Private constructor for singleton
        private StatusViewModel()
        {
            _statusTimer = new System.Timers.Timer(5000);
            _statusTimer.Elapsed += StatusTimer_Elapsed;
            _statusTimer.AutoReset = true;
            _statusTimer.Start();
        }

        public Task RefreshNowAsync() => UpdateStatusAsync();

        private void StatusTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // Run all platform checks on the ThreadPool thread (timer callback),
            // only dispatch results to the main thread for property assignment.
            _ = UpdateStatusAsync();
        }

        private async Task UpdateStatusAsync()
        {
            var bluetoothHelper = BluetoothPlatformProvider.CreateOrUse();
            bool btOn   = bluetoothHelper.CheckIfBTEnabled();
            bool btPerm = bluetoothHelper.CheckPermissions();

            var locationHelper = LocationServicePlatformProvider.CreateOrUse();
            bool gpsOn = false, gpsPerm = false;
            try
            {
                gpsOn   = await locationHelper.IsGpsEnabledAsync().ConfigureAwait(false);
                gpsPerm = await locationHelper.HasLocationPermissionAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.WriteToLog($"UpdateStatusAsync GPS failed: {ex.Message}", minimumLogMode: LogMode.Verbose);
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsBluetoothOn              = btOn;
                BluetoothPermissionGranted = btPerm;
                IsGpsOn                    = gpsOn;
                GpsPermissionGranted       = gpsPerm;
                IsInitializing             = false;
            });
        }
    }
}
