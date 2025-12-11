using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using IndoorCO2MapAppV2.PersistentData;

namespace IndoorCO2MapAppV2.ViewModels
{
    public class HistoryViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<CO2RecordingItem> Recordings { get; set; } = new();

        public ICommand ToggleExpandCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ImportCommand { get; }

        public HistoryViewModel()
        {
            ToggleExpandCommand = new Command<CO2RecordingItem>(item =>
            {
                item.IsExpanded = !item.IsExpanded;
            });

            ExportCommand = new Command(async () =>
            {
                bool result = await App.BackupService.ExportDatabaseAsync();
                await App.Current.MainPage.DisplayAlertAsync(
                    "Export",
                    result ? "Export completed!" : "Export failed.",
                    "OK"
                );
            });

            ImportCommand = new Command(async () =>
            {
                bool result = await App.BackupService.ImportDatabaseAsync();

                if (result)
                {
                    // Import succeeded → tell user to restart app
                    await App.Current.MainPage.DisplayAlertAsync(
                        "Import Successful",
                        "Database imported successfully. Please restart the app to load the new database.",
                        "OK"
                    );
                }
                else
                {
                    // Import failed → notify user
                    await App.Current.MainPage.DisplayAlertAsync(
                        "Import Failed",
                        "Could not import the database. Please try again.",
                        "OK"
                    );
                }
            });



            LoadRecordingsAsync();
        }

        public async Task ReloadRecordingsAsync()
        {
            var all = await App.HistoryDatabase.GetAllRecordingsAsync();
            Recordings.Clear();
            foreach (var r in all)
            {
                Recordings.Add(new CO2RecordingItem(r));
            }
        }

        private async Task LoadRecordingsAsync()
        {
            var all = await App.HistoryDatabase.GetAllRecordingsAsync();
            Recordings.Clear();
            foreach (var r in all)
            {
                Recordings.Add(new CO2RecordingItem(r));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }


    public class CO2RecordingItem : PersistentRecording, INotifyPropertyChanged
    {
        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }

        public CO2RecordingItem(PersistentRecording r)
        {
            Id = r.Id;
            DateTime = r.DateTime;
            LocationName = r.LocationName;
            AvgCO2 = r.AvgCO2;
            Values = r.Values;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
