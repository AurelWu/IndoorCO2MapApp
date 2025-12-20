
using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.CO2Monitors
{
    internal class InkbirdProvider : BaseCO2MonitorProvider
    {


        public static Guid InkbirdServiceUUID = Guid.Parse("0000ffe0-0000-1000-8000-00805f9b34fb");
        public static Guid InkbirdCO2NotifyCharacteristic = Guid.Parse("0000ffe4-0000-1000-8000-00805f9b34fb");

        private IService? _service;
        ICharacteristic? _notifyCharacteristic;
        bool _setupDone = false;
        private List<ushort> assembledCO2History = []; // as we dont directly get the history from the sensor (for now at least) we need to build it using the notifications.


        public override async Task<bool> InitializeAsync(IDevice device)
        {
            ActiveDevice = device;
            CO2MonitorManager.Instance.ActiveCO2MonitorProvider = this;
            if (_setupDone == IsGattValid()) return true;
            _service = await TryGetServiceAsync(device, InkbirdServiceUUID);

            if (_service == null)
            {
                Console.WriteLine("Inkbird service not found.");
                return false;
            }
            if (assembledCO2History == null)
            {
                assembledCO2History = new List<ushort>();
            }
            
            _notifyCharacteristic = await TryGetCharacteristicAsync(_service, InkbirdCO2NotifyCharacteristic);

            if (_notifyCharacteristic == null) return false;

            _notifyCharacteristic.ValueUpdated -= OnInkbirdCO2haracteristicValueChanged;
            _notifyCharacteristic.ValueUpdated += OnInkbirdCO2haracteristicValueChanged;
            await _notifyCharacteristic.StartUpdatesAsync();
            _setupDone = true;
            return true;
        }

        public void OnInkbirdCO2haracteristicValueChanged(object? sender, CharacteristicUpdatedEventArgs e)
        {
            if (sender == null) return;
            var data = e.Characteristic.Value;
            if (data == null) return;
            if (data.Length < 11) return;
            if (data.Length != 16) return;
            byte fb = data[9];
            byte sb = data[10];
            byte[] c = new byte[] { sb, fb };
            ushort CO2LiveValue = BitConverter.ToUInt16(c, 0);
            if (CO2LiveValue < 100 || CO2LiveValue >= 10000) //sanity check
            {
                return;
            }
            CurrentCO2Value = CO2LiveValue;
            assembledCO2History.Add(CO2LiveValue);
            //timeOfLastNotifyUpdate = DateTime.Now;
            
        }

        protected override async Task<int> DoReadCurrentCO2Async()
        {
            return CurrentCO2Value; //updated from the notifications (maybe also exposed in advertisement data or another characteristic but not really needed to use that)
        }


        protected override async Task<ushort[]?> DoReadHistoryAsync(ushort startIndex, int sensorUpdateInterval)
        {
            if(assembledCO2History !=  null)
            {
                return [.. assembledCO2History];
            }
            //returns the data build using the notifications for now. if that list is still
            return [];
        }

        protected override async Task<int> DoReadUpdateIntervalAsync()
        {
            //check if that is exposed somewhere, if not we can probably get it from the notify update frequency
            //for now we return 1 minute interval...
            return 60;
        }

        protected override bool IsGattValid()
        {
            if (_service != null && _notifyCharacteristic != null) return true;
            else return false;

        }

        public override async ValueTask DisposeAsync()
        {            

            if (_notifyCharacteristic != null)
            {
                _notifyCharacteristic.ValueUpdated -= OnInkbirdCO2haracteristicValueChanged;
                try { await _notifyCharacteristic.StopUpdatesAsync(); } catch { }
            }

            if (ActiveDevice != null)
            {
                try { await BLEDeviceManager.Instance._adapter.DisconnectDeviceAsync(ActiveDevice); } catch { }
            }

            _service = null;

            _notifyCharacteristic = null;
            ActiveDevice = null;            
            Logger.WriteToLog("Inkbird disposed", minimumLogMode: LogMode.Verbose);
        }
    }

}

