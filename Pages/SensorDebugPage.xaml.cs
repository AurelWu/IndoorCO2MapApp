using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.ExtensionMethods;
using Microsoft.Maui.Controls;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class SensorDebugPage : AppPage
    {
        private readonly BLEDeviceManager _bluetoothManager;

        public SensorDebugPage()
        {
            InitializeComponent();
            _bluetoothManager = BLEDeviceManager.Instance;
            BindingContext = _bluetoothManager;
            BluetoothDevicesList.ItemsSource = _bluetoothManager.Devices;

            //_bluetoothManager.DeviceDiscovered += OnDeviceDiscovered; //if Observable Collection causes any issues
            BluetoothDevicesList.SelectionChanged += BluetoothDevicesList_SelectionChanged;
        }

        //private void OnDeviceDiscovered(object? sender, BluetoothDeviceModel device)
        //{
        //    //Devices are already in ObservableCollection, UI updates automatically
        //}

        private void BluetoothDevicesList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is BluetoothDeviceModel device)
            {
                // Make details panel visible
                SelectedDeviceDetails.IsVisible = true;

                // Update labels
                DeviceNameLabel.Text = $"Name: {device.Name}";
                DeviceIdLabel.Text = $"Id: {device.Id}";
                DeviceRssiLabel.Text = $"RSSI: {device.Rssi}";
            }
            else
            {
                // Nothing selected
                SelectedDeviceDetails.IsVisible = false;
            }
        }

        private void OnSearchBluetoothDevicesClicked(object sender, EventArgs e)
        {
            _bluetoothManager.StartScanningAsync().SafeFireAndForget();
        }
    }
}
