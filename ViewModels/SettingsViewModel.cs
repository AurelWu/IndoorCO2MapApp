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

        public bool ShowManualButton
        {
            get => UserSettings.Instance.ShowManualButton;
            set { UserSettings.Instance.ShowManualButton = value; OnPropertyChanged(); }
        }

        public bool ShowNewsNotification
        {
            get => UserSettings.Instance.ShowNewsNotification;
            set { UserSettings.Instance.ShowNewsNotification = value; OnPropertyChanged(); }
        }

        public bool ShowDiscordButton
        {
            get => UserSettings.Instance.ShowDiscordButton;
            set { UserSettings.Instance.ShowDiscordButton = value; OnPropertyChanged(); }
        }

        public bool AllowStartWithoutSensorData
        {
            get => UserSettings.Instance.AllowStartWithoutSensorData;
            set { UserSettings.Instance.AllowStartWithoutSensorData = value; OnPropertyChanged(); }
        }

        public bool StopScanAtFirstSensor
        {
            get => UserSettings.Instance.StopScanAtFirstSensor;
            set { UserSettings.Instance.StopScanAtFirstSensor = value; OnPropertyChanged(); }
        }

        public bool ShowLocationType
        {
            get => UserSettings.Instance.ShowLocationType;
            set { UserSettings.Instance.ShowLocationType = value; OnPropertyChanged(); }
        }

        public bool NoteEnterAddsLineBreak
        {
            get => UserSettings.Instance.NoteEnterAddsLineBreak;
            set { UserSettings.Instance.NoteEnterAddsLineBreak = value; OnPropertyChanged(); }
        }

        public bool ShowChangeRouteInRecording
        {
            get => UserSettings.Instance.ShowChangeRouteInRecording;
            set { UserSettings.Instance.ShowChangeRouteInRecording = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Android only — the row is hidden elsewhere. Toggling mid-recording takes effect
        /// immediately rather than waiting for the next recording to start.
        /// </summary>
        public bool KeepRecordingInBackground
        {
            get => UserSettings.Instance.KeepRecordingInBackground;
            set
            {
                UserSettings.Instance.KeepRecordingInBackground = value;
                OnPropertyChanged();
#if ANDROID
                Recording.RecordingManager.Instance.ApplyBackgroundRecordingSetting();
#endif
            }
        }

        public static bool IsBackgroundRecordingSupported =>
#if ANDROID
            true;
#else
            false;
#endif

        public string AppVersion =>
            $"Version {AppInfo.VersionString} ({AppInfo.BuildString})";

        /// <summary>
        /// App languages as (culture code, button label). Labels are fixed here and must never
        /// come from the resource files: someone who switched to a language they can't read has
        /// to recognise their own language to switch back. A language in a non-Latin script gets
        /// its label in that script (e.g. ("ja", "日本語")); existing ones stay as their codes.
        /// </summary>
        /// <remarks>
        /// A getter, not a static field: the static <see cref="Instance"/> at the top of the class
        /// is constructed during type initialisation, and its constructor reads this table (via
        /// <see cref="LanguageOptions"/>) before a field declared further down would be assigned.
        /// </remarks>
        private static (string Code, string Label)[] AppLanguages =>
        [
            ("en", "EN"),
            ("de", "DE"),
            ("fr", "FR"),
            ("pt", "PT"),
            ("es", "ES"),
            ("ja", "日本語"),
        ];

        public IReadOnlyList<string> LanguageOptions { get; } = AppLanguages.Select(l => l.Label).ToList();

        public string LanguageRestartHint => Localisation.SettingsLanguageRestartHint;

        // Mapped through the table rather than by upper/lower-casing the code, which only works
        // while every label happens to be a Latin-script language code.
        public string SelectedLanguage
        {
            get => AppLanguages.FirstOrDefault(l => l.Code == UserSettings.Instance.Language).Label
                   ?? AppLanguages[0].Label;
            set
            {
                var code = AppLanguages.FirstOrDefault(l => l.Label == value).Code;
                if (code == null || UserSettings.Instance.Language == code) return;
                UserSettings.Instance.Language = code;
                OnPropertyChanged();
            }
        }
    }

}
