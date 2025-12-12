using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        public MainPageViewModel()
        {
            StartBuildingRecordingCommand = new AsyncRelayCommand(StartRecordingAsync);
            Sensor = new SensorViewModel();
            BuildingSearch = new BuildingSearchViewModel();
            Settings = SettingsViewModel.Instance;
            FetchState = OverpassDataFetcher.Instance.State;

        Sensor.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Sensor.SelectedDevice) ||
                    e.PropertyName == nameof(Sensor.CurrentCO2))
                {
                    OnPropertyChanged(nameof(CanStartBuildingRecording));
                }
            };

            BuildingSearch.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(BuildingSearch.SelectedBuilding))
                {
                    OnPropertyChanged(nameof(CanStartBuildingRecording));
                }
            };

        }

        private async Task StartRecordingAsync()
        {
            if (BuildingSearch.SelectedBuilding == null ||
                Sensor.SelectedDevice == null ||
                Sensor.CurrentCO2 <= 0)
                return;

            await RecordingManager.Instance.StartRecordingAsync(
                BuildingSearch.SelectedBuilding.Type,
                BuildingSearch.SelectedBuilding.ID,
                BuildingSearch.SelectedBuilding.Latitude,
                BuildingSearch.SelectedBuilding.Longitude,
                BuildingSearch.SelectedBuilding.Name,
                Sensor.SelectedMonitorType.ToString(),
                Sensor.SelectedDevice.Id
            );
            await AppPage.NavigateAsync("///building");
        }

        public bool CanStartBuildingRecording =>
            BuildingSearch.SelectedBuilding != null &&
            Sensor.SelectedDevice != null &&
            Sensor.CurrentCO2 > 0;
    }
}
