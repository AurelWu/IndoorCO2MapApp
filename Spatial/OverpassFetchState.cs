using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace IndoorCO2MapAppV2.Spatial
{
    public partial class OverpassFetchState : INotifyPropertyChanged
    {
        private bool _isFetching;
        public bool IsFetching
        {
            get => _isFetching;
            set { _isFetching = value; OnPropertyChanged(); }
        }

        private bool _lastFailed;
        public bool LastFailed
        {
            get => _lastFailed;
            set { _lastFailed = value; OnPropertyChanged(); }
        }

        private bool _usingAlternative;
        public bool UsingAlternative
        {
            get => _usingAlternative;
            set { _usingAlternative = value; OnPropertyChanged(); }
        }

        private string? _lastError;
        public string? LastError
        {
            get => _lastError;
            set { _lastError = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

}
