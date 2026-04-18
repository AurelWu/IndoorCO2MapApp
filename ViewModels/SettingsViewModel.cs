using IndoorCO2MapAppV2.Resources.Strings;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using IndoorCO2MapAppV2.PersistentData;

namespace IndoorCO2MapAppV2.ViewModels
{


    public class SettingsViewModel : INotifyPropertyChanged
    {
        public static SettingsViewModel Instance { get; } = new SettingsViewModel();
        private const string FileName = "user_settings.json";


        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = "")
    => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private SettingsViewModel() { }


        // Bindable properties

        public string DefaultSortMode
        {
            get
            {
                // Map the boolean to the localized string
                return UserSettings.Instance.SortBuildingsAlphabetical
                    ? Localisation.Sort_Alphabetical
                    : Localisation.Sort_Distance;
            }

            set
            {
                // Explicit mapping for the setter
                if (value == Localisation.Sort_Alphabetical)
                {
                    UserSettings.Instance.SortBuildingsAlphabetical = true;
                }
                else if (value == Localisation.Sort_Distance)
                {
                    UserSettings.Instance.SortBuildingsAlphabetical = false;
                }

                OnPropertyChanged(nameof(DefaultSortMode));
            }

        }

        public IReadOnlyCollection<string> SortModes => new List<string>
        {
            Localisation.Sort_Distance,
            Localisation.Sort_Alphabetical,
        };

        public bool SortBuildingsAlphabetical
        {
            get => UserSettings.Instance.SortBuildingsAlphabetical;
            set { UserSettings.Instance.SortBuildingsAlphabetical = value; OnPropertyChanged(); }
        }

        public bool DisplaySortingModeToggle
        {
            get => UserSettings.Instance.DisplaySortingModeToggle;
            set { UserSettings.Instance.DisplaySortingModeToggle = value; OnPropertyChanged(); }
        }

        public bool DisplayBuildingFilterInputField
        {
            get => UserSettings.Instance.DisplayBuildingFilterInputField;
            set { UserSettings.Instance.DisplayBuildingFilterInputField = value; OnPropertyChanged(); }
        }

        public bool ConfirmationBeforeRecordingUpload
        {
            get => UserSettings.Instance.ConfirmUpload;
            set { UserSettings.Instance.ConfirmUpload = value; OnPropertyChanged(); }
        }

        public bool EnableLocationCaching
        {
            get => UserSettings.Instance.EnableLocationCaching;
            set { UserSettings.Instance.EnableLocationCaching = value; OnPropertyChanged();}
        }

        public bool EnableHistory
        {
            get => UserSettings.Instance.EnableHistory;
            set { UserSettings.Instance.EnableHistory = value; OnPropertyChanged();}
        }

        public bool EnablePreRecording
        {
            get => UserSettings.Instance.EnablePrerecording;
            set { UserSettings.Instance.EnablePrerecording = value; OnPropertyChanged(); }
        }

        public IReadOnlyCollection<string> CacheRangeOverrideOptions => ["No", "250m", "500m"];

        public string CacheRangeOverride
        {
            get => UserSettings.Instance.CacheRangeOverrideMeters switch
            {
                250 => "250m",
                500 => "500m",
                _   => "No"
            };
            set
            {
                UserSettings.Instance.CacheRangeOverrideMeters = value switch
                {
                    "250m" => 250,
                    "500m" => 500,
                    _      => -1
                };
                OnPropertyChanged();
            }
        }

        public string SensorFilter
        {
            get => UserSettings.Instance.SensorFilter ?? string.Empty;
            set
            {
                if (UserSettings.Instance.SensorFilter != value)
                {
                    UserSettings.Instance.SensorFilter = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool UseLiveLocationService
        {
            get => UserSettings.Instance.UseLiveLocationService;
            set { UserSettings.Instance.UseLiveLocationService = value; OnPropertyChanged(); }
        }

        public bool ShowRoutePreview
        {
            get => UserSettings.Instance.ShowRoutePreview;
            set { UserSettings.Instance.ShowRoutePreview = value; OnPropertyChanged(); }
        }

        public bool ShowWebsiteButton
        {
            get => UserSettings.Instance.ShowWebsiteButton;
            set { UserSettings.Instance.ShowWebsiteButton = value; OnPropertyChanged(); }
        }

        public bool ShowImprintButton
        {
            get => UserSettings.Instance.ShowImprintButton;
            set { UserSettings.Instance.ShowImprintButton = value; OnPropertyChanged(); }
        }

        public string AppVersion =>
            $"Version {AppInfo.VersionString} ({AppInfo.BuildString})";
    }

}
