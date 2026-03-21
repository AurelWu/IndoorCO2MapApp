using CommunityToolkit.Mvvm.ComponentModel;
using IndoorCO2MapAppV2.UIUtility;

namespace IndoorCO2MapAppV2.ViewModels
{
    public partial class NewsViewModel : ObservableObject
    {
        [ObservableProperty]
        private HtmlWebViewSource htmlSource = new()
        {
            Html = MarkdownHelper.ToHtml("# News\n\n_Loading..._")
        };

        [ObservableProperty]
        private bool isLoading;

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("news.md");
                using var reader = new StreamReader(stream);
                var markdown = await reader.ReadToEndAsync();
                HtmlSource = new HtmlWebViewSource { Html = MarkdownHelper.ToHtml(markdown) };
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
