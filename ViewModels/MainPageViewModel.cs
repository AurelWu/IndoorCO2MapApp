using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;

namespace IndoorCO2MapAppV2.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        public SensorViewModel Sensor { get; }
        public BuildingSearchViewModel BuildingSearch { get; }
        public SettingsViewModel Settings { get; }

        public MainPageViewModel()
        {
            Sensor = new SensorViewModel();
            BuildingSearch = new BuildingSearchViewModel();
            Settings = SettingsViewModel.Instance;

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

        public bool CanStartBuildingRecording =>
            BuildingSearch.SelectedBuilding != null &&
            Sensor.SelectedDevice != null &&
            Sensor.CurrentCO2 > 0;
    }
}
