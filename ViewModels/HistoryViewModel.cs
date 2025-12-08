using System;
using System.Collections.Generic;
using System.Text;
using IndoorCO2MapAppV2.PersistentData;

namespace IndoorCO2MapAppV2.ViewModels
{
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Windows.Input;

    public class HistoryViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<CO2RecordingItem> Recordings { get; set; } = new();

        public ICommand ToggleExpandCommand { get; }

        public HistoryViewModel()
        {
            ToggleExpandCommand = new Command<CO2RecordingItem>(item =>
            {
                item.IsExpanded = !item.IsExpanded;
            });

            LoadRecordings();
        }

        private async void LoadRecordings()
        {
            var all = await App.Database.GetAllRecordingsAsync();
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
            set { _isExpanded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded))); }
        }

        public CO2RecordingItem(PersistentRecording r)
        {
            Id = r.Id;
            DateTime = r.DateTime;
            LocationName = r.LocationName;
            AvgCO2 = r.AvgCO2;
            Values = r.Values;
            IsExpanded = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

}
