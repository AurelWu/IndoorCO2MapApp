using IndoorCO2MapAppV2.DebugTools;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace IndoorCO2MapAppV2.PersistentData
{
    public sealed class UserSettings : AutoSaveSettings
    {
        private const string FileName = "usersettings.json";

        private static Lazy<UserSettings> _instance =
            new(() => new UserSettings());

        public static UserSettings Instance => _instance.Value;

        public UserSettings() { }

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



        public static void Init(UserSettings settings) =>
            _instance = new Lazy<UserSettings>(() => settings);

        public static async Task SaveAsync()
        {
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
                Logger.WriteToLog("Error saving user settings");
            }
        }
        public static void Load()
        {
            try
            {
                string path = Path.Combine(FileSystem.AppDataDirectory, FileName);

                if (!File.Exists(path))
                    return;

                using var stream = File.OpenRead(path);
                using var reader = new StreamReader(stream);

                string json = reader.ReadToEnd();
                Console.WriteLine(json);

                var loaded = JsonSerializer.Deserialize<UserSettings>(json);

                if (loaded != null)
                {
                    Init(loaded);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteToLog("Error loading user settings: " + ex.Message);
            }
        }
    }




}
