using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IndoorCO2MapAppV2.CO2Monitors;
using IndoorCO2MapAppV2.Pages;
using IndoorCO2MapAppV2.Recording;
using IndoorCO2MapAppV2.Resources.Strings;
using IndoorCO2MapAppV2.Spatial;
using System.ComponentModel;

namespace IndoorCO2MapAppV2.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {

        public IRelayCommand StartBuildingRecordingCommand { get; }
        public IRelayCommand StartTransitRecordingCommand { get; }
        public IRelayCommand RecordingModeChangedCommand { get; }

        public SensorViewModel Sensor { get; }
        public BuildingSearchViewModel BuildingSearch { get; }
        public TransitSearchViewModel Transit { get; }
        public SettingsViewModel Settings { get; }

        public OverpassFetchState FetchState { get; }

        public List<string> RecordingModeOptions { get; } =
            [Localisation.MainMenuModeBuilding, Localisation.MainMenuModeTransit];

        [ObservableProperty] private bool hasRecordings;
        [ObservableProperty] private bool isTransitMode;

        public bool IsBuildingMode => !IsTransitMode;

        public async Task RefreshHasRecordingsAsync()
        {
            var all = await App.HistoryDatabase.GetAllRecordingsAsync();
            HasRecordings = all.Count > 0;
        }

        public MainPageViewModel()
        {
            StartBuildingRecordingCommand = new AsyncRelayCommand(StartRecordingAsync);
            StartTransitRecordingCommand = new AsyncRelayCommand(StartTransitRecordingAsync);
            RecordingModeChangedCommand = new RelayCommand<string>(mode =>
            {
                IsTransitMode = mode == RecordingModeOptions[1];
                OnPropertyChanged(nameof(IsBuildingMode));
                OnPropertyChanged(nameof(CanStartTransitRecording));
                OnPropertyChanged(nameof(IsTransitRecordingBlocked));
                OnPropertyChanged(nameof(TransitRecordingBlockedReason));
            });

            Sensor = new SensorViewModel();
            BuildingSearch = new BuildingSearchViewModel();
            Transit = new TransitSearchViewModel();
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
                    OnPropertyChanged(nameof(CanStartTransitRecording));
                    OnPropertyChanged(nameof(IsTransitRecordingBlocked));
                    OnPropertyChanged(nameof(TransitRecordingBlockedReason));
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

            Transit.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(Transit.SelectedStation) ||
                    e.PropertyName == nameof(Transit.SelectedRoute))
                {
                    OnPropertyChanged(nameof(CanStartTransitRecording));
                    OnPropertyChanged(nameof(IsTransitRecordingBlocked));
                    OnPropertyChanged(nameof(TransitRecordingBlockedReason));
                }
            };
        }

        private async Task StartRecordingAsync()
        {
            if (BuildingSearch.SelectedBuilding == null ||
                Sensor.SelectedDevice == null ||
                Sensor.CurrentCO2 <= 0)
                return;

            var monitorType = Sensor.SelectedDevice.DetectedType
                ?? CO2MonitorProviderFactory.DetectFromName(Sensor.SelectedDevice.Name);

            await RecordingManager.Instance.StartRecordingAsync(
                BuildingSearch.SelectedBuilding.Type,
                BuildingSearch.SelectedBuilding.ID,
                BuildingSearch.SelectedBuilding.Latitude,
                BuildingSearch.SelectedBuilding.Longitude,
                BuildingSearch.SelectedBuilding.Name,
                monitorType?.ToString() ?? "",
                Sensor.SelectedDevice.Id,
                Settings.EnablePreRecording
            );
            await AppPage.NavigateAsync("///building");
        }

        private async Task StartTransitRecordingAsync()
        {
            var station = Transit.SelectedStation;
            var route = Transit.SelectedRoute;
            if (station == null || route == null || Sensor.SelectedDevice == null) return;

            var monitorType = Sensor.SelectedDevice.DetectedType
                ?? CO2MonitorProviderFactory.DetectFromName(Sensor.SelectedDevice.Name);

            await RecordingManager.Instance.StartRecordingAsync(
                route.NWRType,
                route.ID,
                station.Latitude,
                station.Longitude,
                $"{route.ShortenedName.Trim()} ({station.Name})",
                monitorType?.ToString() ?? "",
                Sensor.SelectedDevice.Id,
                Settings.EnablePreRecording);

            var rec = RecordingManager.Instance.ActiveRecording;
            if (rec != null)
            {
                rec.AdditionalDataByParameter.TryAdd("vehicleType", route.VehicleType);
                rec.AdditionalDataByParameter.TryAdd("startNWRType", "node");
                rec.AdditionalDataByParameter.TryAdd("startID", station.ID.ToString());
                rec.AdditionalDataByParameter.TryAdd("startName", station.Name);
                rec.AdditionalDataByParameter.TryAdd("startLat", station.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture));
                rec.AdditionalDataByParameter.TryAdd("startLon", station.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture));
                rec.AdditionalDataByParameter.TryAdd("routeNWRType", route.NWRType);
                rec.AdditionalDataByParameter.TryAdd("routeID", route.ID.ToString());
                rec.AdditionalDataByParameter.TryAdd("routeName", route.Name);
            }

            await AppPage.NavigateAsync("///transit");
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
                if (BuildingSearch.SelectedBuilding == null) return Localisation.MainMenuBlockedNoBuilding;
                if (Sensor.SelectedDevice == null)           return Localisation.MainMenuBlockedNoSensor;
                if (Sensor.CurrentCO2 == 0)                  return Localisation.MainMenuBlockedWaitingCO2;
                return string.Empty;
            }
        }

        public bool CanStartTransitRecording =>
            Transit.SelectedStation != null &&
            Transit.SelectedRoute != null &&
            Sensor.SelectedDevice != null &&
            Sensor.CurrentCO2 > 0;

        public bool IsTransitRecordingBlocked => !CanStartTransitRecording;

        public string TransitRecordingBlockedReason
        {
            get
            {
                if (Transit.SelectedStation == null) return Localisation.MainMenuBlockedNoStation;
                if (Transit.SelectedRoute == null)   return Localisation.MainMenuBlockedNoRoute;
                if (Sensor.SelectedDevice == null)   return Localisation.MainMenuBlockedNoSensor;
                if (Sensor.CurrentCO2 == 0)          return Localisation.MainMenuBlockedWaitingCO2;
                return string.Empty;
            }
        }
    }
}
