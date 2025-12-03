using System;
using System.Collections.Generic;
using System.Text;

namespace IndoorCO2MapAppV2.PersistentData
{
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Text.Json;

    public sealed class SettingsManager : INotifyPropertyChanged
    {
        private static readonly Lazy<SettingsManager> lazy =
            new Lazy<SettingsManager>(() => new SettingsManager());

        public static SettingsManager Instance => lazy.Value;

        private const string FileName = "user_settings.json";

        private UserSettings _settings = new();

        public event PropertyChangedEventHandler PropertyChanged;

        private SettingsManager() { }

        public UserSettings Settings => _settings;

        // Bindable properties
        public bool SortBuildingsAlphabetical
        {
            get => _settings.SortBuildingsAlphabetical;
            set { _settings.SortBuildingsAlphabetical = value; OnPropertyChanged(); }
        }

        public bool DisplaySortingModeToggle
        {
            get => _settings.DisplaySortingModeToggle;
            set { _settings.DisplaySortingModeToggle = value; OnPropertyChanged(); }
        }

        public bool DisplayBuildingFilterInputField
        {
            get => _settings.DisplayBuildingFilterInputField;
            set { _settings.DisplayBuildingFilterInputField = value; OnPropertyChanged(); }
        }


        // Load / Save
        public async Task LoadAsync()
        {
            try
            {
                string path = Path.Combine(FileSystem.AppDataDirectory, FileName);

                if (!File.Exists(path))
                    return;

                string json = await File.ReadAllTextAsync(path);
                _settings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();

                // Notify that all properties changed
                OnPropertyChanged("");
            }
            catch { }
        }

        public async Task SaveAsync()
        {
            string path = Path.Combine(FileSystem.AppDataDirectory, FileName);

            string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(path, json);
        }


        private void OnPropertyChanged([CallerMemberName] string name = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

}
