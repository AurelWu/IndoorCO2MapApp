using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IndoorCO2MapAppV2.ViewModels
{
    public partial class StatisticsViewModel : ObservableObject
    {
        private static readonly HttpClient _httpClient = new();

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        [ObservableProperty]
        private ObservableCollection<CountryStatItem> countryStats = [];

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasError))]
        private string? errorMessage;

        public bool HasError => ErrorMessage != null;

        [ObservableProperty]
        private int totalMeasurements;

        public async Task LoadAsync()
        {
            IsLoading = true;
            ErrorMessage = null;

            try
            {
                var json = await _httpClient.GetStringAsync(
                    "https://indoorco2map.com/chartdata/CountByCountry.json"
                );

                var raw = JsonSerializer.Deserialize<List<CountryStatDto>>(json, _jsonOptions);
                if (raw == null) return;

                var filtered = raw
                    .Where(x => !string.IsNullOrWhiteSpace(x.CountryName))
                    .ToList();

                int maxCount = filtered.Count > 0 ? filtered.Max(x => x.Count) : 1;
                TotalMeasurements = raw.Sum(x => x.Count);

                int rank = 1;
                CountryStats = new ObservableCollection<CountryStatItem>(
                    filtered.Select(x => new CountryStatItem
                    {
                        Rank = rank++,
                        CountryName = x.CountryName!,
                        Count = x.Count,
                        BarRatio = (double)x.Count / maxCount
                    })
                );
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load data: " + ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class CountryStatDto
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("countryname")]
        public string? CountryName { get; set; }
    }

    public class CountryStatItem
    {
        public int Rank { get; set; }
        public string CountryName { get; set; } = "";
        public int Count { get; set; }
        public double BarRatio { get; set; }
    }
}
