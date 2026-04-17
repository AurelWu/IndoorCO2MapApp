using CommunityToolkit.Mvvm.ComponentModel;
using IndoorCO2MapAppV2.UIUtility;

namespace IndoorCO2MapAppV2.ViewModels
{
    public partial class NewsViewModel : ObservableObject
    {
        private const string RemoteNewsUrl = "https://indoorco2map.com/news.md";
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
        private static string? _cachedHtml;

        [ObservableProperty]
        private HtmlWebViewSource htmlSource = new()
        {
            Html = MarkdownHelper.ToHtml("# News\n\n_Loading..._")
        };

        [ObservableProperty]
        private bool isLoading;

        public async Task LoadAsync()
        {
            if (_cachedHtml != null)
            {
                HtmlSource = new HtmlWebViewSource { Html = _cachedHtml };
                return;
            }

            IsLoading = true;
            try
            {
                string? markdown = null;

                try
                {
                    markdown = await _http.GetStringAsync(RemoteNewsUrl);
                }
                catch { }

                if (string.IsNullOrWhiteSpace(markdown))
                {
                    using var stream = await FileSystem.OpenAppPackageFileAsync("news.md");
                    using var reader = new StreamReader(stream);
                    markdown = await reader.ReadToEndAsync();
                }

                _cachedHtml = MarkdownHelper.ToHtml(markdown);
                HtmlSource = new HtmlWebViewSource { Html = _cachedHtml };
            }
            catch (Exception ex)
            {
                HtmlSource = new HtmlWebViewSource
                {
                    Html = MarkdownHelper.ToHtml($"# News\n\n_Could not load content: {ex.Message}_")
                };
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
