using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.Enumerations;
using IndoorCO2MapAppV2.ExtensionMethods;
using Microsoft.Maui.Controls;
using System.ComponentModel;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class SensorDebugPage : AppPage, INotifyPropertyChanged
    {
        private readonly BLEDeviceManager _bluetoothManager;
        private readonly List<CO2MonitorType> monitorOptions = [];

        // Current filter string (from Picker)
        private CO2MonitorType monitorTypeFilter = CO2MonitorType.None;

        // Selected device for details panel
        private BluetoothDeviceModel? _selectedDevice;
        public BluetoothDeviceModel? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (_selectedDevice != value)
                {
                    _selectedDevice = value;
                    OnPropertyChanged(nameof(SelectedDevice));
                }
            }
        }

        public bool IsScanning => _bluetoothManager.IsScanning;

        public SensorDebugPage()
        {
            InitializeComponent();

            // BLE setup
            _bluetoothManager = BLEDeviceManager.Instance;
            BluetoothDevicesList.ItemsSource = _bluetoothManager.Devices;

            // Populate Picker with debug dictionary (includes "All Devices")
            SetupMonitorPicker();

            // Selection handling
            BluetoothDevicesList.SelectionChanged += BluetoothDevicesList_SelectionChanged;

            // Listen for scanning property changes
            _bluetoothManager.PropertyChanged += BluetoothManager_PropertyChanged;

            BindingContext = this;
        }

        private void SetupMonitorPicker()
        {
            // Copy dictionary keys into the strongly-typed list
            monitorOptions.AddRange(MonitorTypes.SearchStringByMonitorTypeDebugMode.Keys);

            // Set picker items to display strings
            MonitorTypePicker.ItemsSource = monitorOptions
                .Select(mt => MonitorTypes.SearchStringByMonitorTypeDebugMode[mt])
                .ToList();

            MonitorTypePicker.SelectedIndex = 0; // default selection
            monitorTypeFilter = monitorOptions[0]; // initial filter
        }

        private void BluetoothManager_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_bluetoothManager.IsScanning))
            {
                OnPropertyChanged(nameof(IsScanning));
            }
        }

        private void BluetoothDevicesList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection != null && e.CurrentSelection.Count > 0)
            {
                SelectedDevice = e.CurrentSelection[0] as BluetoothDeviceModel;
            }
            else
            {
                SelectedDevice = null;
            }
        }

        private void MonitorTypePicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (MonitorTypePicker.SelectedIndex >= 0)
            {
                monitorTypeFilter = monitorOptions[MonitorTypePicker.SelectedIndex];
            }
        }

        private void OnSearchBluetoothDevicesClicked(object sender, EventArgs e)
        {
            // Start scanning with the currently selected filter
            _bluetoothManager.StartScanningAsync(filter: monitorTypeFilter).SafeFireAndForget();
        }

        private void OnRetrieveDataFromMonitorClicked(object sender, EventArgs e)
        {
            if (SelectedDevice == null) return;
            if (SelectedDevice.Name == null) return; //All monitors supported so far have a name, if that ever changes this might need to be changed
            
        }
    }
}
