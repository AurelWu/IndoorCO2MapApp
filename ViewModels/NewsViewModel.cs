using CommunityToolkit.Mvvm.ComponentModel;
using IndoorCO2MapAppV2.UIUtility;
using System.Security.Cryptography;
using System.Text;

namespace IndoorCO2MapAppV2.ViewModels
{
    public partial class NewsViewModel : ObservableObject
    {
        private const string RemoteNewsUrl = "https://indoorco2map.com/news.md";
        private const string LastSeenHashKey = "LastSeenNewsHash";
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
        private static string? _cachedHtml;
        private static string? _cachedMarkdown;

        public static bool HasUnreadNews { get; private set; }

        [ObservableProperty]
        private HtmlWebViewSource htmlSource = new()
        {
            Html = MarkdownHelper.ToHtml("# News\n\n_Loading..._")
        };

        [ObservableProperty]
        private bool isLoading;

        // Called fire-and-forget from MainPage.OnAppearing.
        // Tries remote first, falls back to local bundle.
        // Pre-warms _cachedMarkdown/_cachedHtml so the News page loads instantly.
        public static async Task CheckForNewNewsAsync()
        {
            try
            {
                if (_cachedMarkdown == null)
                {
                    string? markdown = null;
                    try { markdown = await _http.GetStringAsync(RemoteNewsUrl); } catch { }
                    if (string.IsNullOrWhiteSpace(markdown))
                    {
                        using var stream = await FileSystem.OpenAppPackageFileAsync("news.md");
                        using var reader = new StreamReader(stream);
                        markdown = await reader.ReadToEndAsync();
                    }
                    _cachedMarkdown = markdown;
                    _cachedHtml = MarkdownHelper.ToHtml(markdown);
                }
                HasUnreadNews = ComputeHash(_cachedMarkdown) != Preferences.Get(LastSeenHashKey, "");
            }
            catch { HasUnreadNews = false; }
        }

        // Called from NewsPage after content loads — marks whatever was shown as read.
        public static void MarkAsRead()
        {
            if (_cachedMarkdown == null) return;
            Preferences.Set(LastSeenHashKey, ComputeHash(_cachedMarkdown));
            HasUnreadNews = false;
        }

        private static string ComputeHash(string content)
            => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(content)));

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

                _cachedMarkdown = markdown;
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
