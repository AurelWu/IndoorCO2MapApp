using IndoorCO2MapAppV2.DebugTools;
using IndoorCO2MapAppV2.Enumerations;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace IndoorCO2MapAppV2.PersistentData
{
    public class TransitRouteFavourite
    {
        public long RouteId { get; set; }
        public double Lat { get; set; }  // GPS rounded to 2 decimals (~1 km bucket)
        public double Lon { get; set; }
    }

    public sealed class UserSettings : AutoSaveSettings
    {
        private const string FileName = "usersettings.json";

        private static Lazy<UserSettings> _instance =
            new(() => new UserSettings());

        public static UserSettings Instance => _instance.Value;
        private static bool currentlyLoading = false;

        public UserSettings() { }

        private string _sensorFilter = string.Empty;
        public string SensorFilter
        {
            get => _sensorFilter;
            set => SetProperty(ref _sensorFilter, value);
        }

        private bool _confirmUpload = false;
        public bool ConfirmUpload
        {
            get => _confirmUpload;
            set => SetProperty(ref _confirmUpload, value);
        }

        private bool _enableHistory = true;
        public bool EnableHistory
        {
            get => _enableHistory;
            set => SetProperty(ref _enableHistory, value);
        }

        private bool _enableLocationCaching = true;
        public bool EnableLocationCaching
        {
            get => _enableLocationCaching;
            set => SetProperty(ref _enableLocationCaching, value);
        }

        private bool _sortBuildingsAlphabetical = true;
        public bool SortBuildingsAlphabetical
        {
            get => _sortBuildingsAlphabetical;
            set => SetProperty(ref _sortBuildingsAlphabetical, value);
        }

        private bool _displaySortingModeToggle = true;
        public bool DisplaySortingModeToggle
        {
            get => _displaySortingModeToggle;
            set => SetProperty(ref _displaySortingModeToggle, value);
        }

        private bool _displayBuildingFilterInputField = true;
        public bool DisplayBuildingFilterInputField
        {
            get => _displayBuildingFilterInputField;
            set => SetProperty(ref _displayBuildingFilterInputField, value);
        }

        private Color _defaultButtonColor = new(50, 50, 100);
        public Color DefaultButtonColor
        {
            get => _defaultButtonColor;
            set => SetProperty(ref _defaultButtonColor, value);
        }

        private Color _notPickedToggleButtonColor = new(50, 50, 100);
        public Color NotPickedToggleButtonColor
        {
            get => _notPickedToggleButtonColor;
            set => SetProperty(ref _notPickedToggleButtonColor, value);
        }

        private bool _enablePrerecording;
        public bool EnablePrerecording
        {
            get => _enablePrerecording;
            set => SetProperty(ref _enablePrerecording, value);
        }

        private List<string> _favouriteLocationKeys = new();
        public List<string> FavouriteLocationKeys
        {
            get => _favouriteLocationKeys;
            set => SetProperty(ref _favouriteLocationKeys, value);
        }

        private List<TransitRouteFavourite> _favouriteTransitRoutes = new();
        public List<TransitRouteFavourite> FavouriteTransitRoutes
        {
            get => _favouriteTransitRoutes;
            set => SetProperty(ref _favouriteTransitRoutes, value);
        }

        private int _cacheRangeOverrideMeters = -1;
        public int CacheRangeOverrideMeters
        {
            get => _cacheRangeOverrideMeters;
            set => SetProperty(ref _cacheRangeOverrideMeters, value);
        }

        private bool _useLiveLocationService = false;
        public bool UseLiveLocationService
        {
            get => _useLiveLocationService;
            set => SetProperty(ref _useLiveLocationService, value);
        }

        private bool _showRoutePreview = true;
        public bool ShowRoutePreview
        {
            get => _showRoutePreview;
            set => SetProperty(ref _showRoutePreview, value);
        }

        private bool _showWebsiteButton = true;
        public bool ShowWebsiteButton
        {
            get => _showWebsiteButton;
            set => SetProperty(ref _showWebsiteButton, value);
        }

        private bool _showImprintButton = true;
        public bool ShowImprintButton
        {
            get => _showImprintButton;
            set => SetProperty(ref _showImprintButton, value);
        }



        public static void Init(UserSettings settings) =>
            _instance = new Lazy<UserSettings>(() => settings);

        public static async Task SaveAsync()
        {
            if (currentlyLoading) return; //dont write to the file while we are still loading from it
            try
            {
                string path = Path.Combine(FileSystem.AppDataDirectory, FileName);

                string json = JsonSerializer.Serialize(Instance, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(path, json);
            }
            catch
            {
                Logger.WriteToLog("Error saving user settings",minimumLogMode: LogMode.Default);
            }
        }
        public static void Load()
        {
            currentlyLoading = true;
            try
            {
                string path = Path.Combine(FileSystem.AppDataDirectory, FileName);

                if (!File.Exists(path))
                {
                    currentlyLoading = false;
                    return;
                }
                    

                using var stream = File.OpenRead(path);
                using var reader = new StreamReader(stream);

                string json = reader.ReadToEnd();
                //Console.WriteLine(json);

                var loaded = JsonSerializer.Deserialize<UserSettings>(json);

                if (loaded != null)
                {
                    Init(loaded);
                }
            }
            catch (Exception ex)
            {
                currentlyLoading = false;
                Logger.WriteToLog("Error loading user settings: " + ex.Message);
            }
            currentlyLoading = false;
        }
    }




}
