using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.Pages;
using IndoorCO2MapAppV2.Recording;
using IndoorCO2MapAppV2.Spatial;
using System.ComponentModel;

namespace IndoorCO2MapAppV2.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {


        public IRelayCommand StartBuildingRecordingCommand { get; }

        public SensorViewModel Sensor { get; }
        public BuildingSearchViewModel BuildingSearch { get; }
        public SettingsViewModel Settings { get; }

        public OverpassFetchState FetchState { get; }

        [ObservableProperty] private bool hasRecordings;

        public async Task RefreshHasRecordingsAsync()
        {
            var all = await App.HistoryDatabase.GetAllRecordingsAsync();
            HasRecordings = all.Count > 0;
        }

        public MainPageViewModel()
        {
            StartBuildingRecordingCommand = new AsyncRelayCommand(StartRecordingAsync);
            Sensor = new SensorViewModel();
            BuildingSearch = new BuildingSearchViewModel();
            Settings = SettingsViewModel.Instance;
            FetchState = OverpassDataFetcher.Instance.State;

            _ = RefreshHasRecordingsAsync();

        Sensor.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Sensor.SelectedDevice) ||
                    e.PropertyName == nameof(Sensor.CurrentCO2))
                {
                    OnPropertyChanged(nameof(CanStartBuildingRecording));
                    OnPropertyChanged(nameof(IsStartRecordingBlocked));
                    OnPropertyChanged(nameof(StartRecordingBlockedReason));
                }
            };

            BuildingSearch.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(BuildingSearch.SelectedBuilding))
                {
                    OnPropertyChanged(nameof(CanStartBuildingRecording));
                    OnPropertyChanged(nameof(IsStartRecordingBlocked));
                    OnPropertyChanged(nameof(StartRecordingBlockedReason));
                }
            };

        }

        private async Task StartRecordingAsync()
        {
            if (BuildingSearch.SelectedBuilding == null ||
                Sensor.SelectedDevice == null ||
                Sensor.CurrentCO2 <= 0)
                return;

            var monitorType = CO2MonitorProviderFactory.DetectFromName(Sensor.SelectedDevice.Name);

            await RecordingManager.Instance.StartRecordingAsync(
                BuildingSearch.SelectedBuilding.Type,
                BuildingSearch.SelectedBuilding.ID,
                BuildingSearch.SelectedBuilding.Latitude,
                BuildingSearch.SelectedBuilding.Longitude,
                BuildingSearch.SelectedBuilding.Name,
                monitorType.ToString() ?? "", //shouldnt ever be "" but just to be safe - if it happens we will notice in backend and can investigate
                Sensor.SelectedDevice.Id,
                Settings.EnablePreRecording
            );
            await AppPage.NavigateAsync("///building");
        }


        

        public bool CanStartBuildingRecording =>
            BuildingSearch.SelectedBuilding != null &&
            Sensor.SelectedDevice != null &&
            Sensor.CurrentCO2 > 0;

        public bool IsStartRecordingBlocked => !CanStartBuildingRecording;

        public string StartRecordingBlockedReason
        {
            get
            {
                if (BuildingSearch.SelectedBuilding == null) return "No building selected";
                if (Sensor.SelectedDevice == null)           return "No sensor connected";
                if (Sensor.CurrentCO2 == 0)                  return "Waiting for CO2 reading (0 ppm)";
                return string.Empty;
            }
        }
    }
}
