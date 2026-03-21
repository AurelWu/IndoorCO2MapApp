namespace IndoorCO2MapAppV2
{
    public partial class AppShell : Shell
    {
        private bool _prewarmed;

        public AppShell()
        {
            InitializeComponent();
        }

        protected override void OnNavigated(ShellNavigatedEventArgs args)
        {
            base.OnNavigated(args);
            if (!_prewarmed)
            {
                _prewarmed = true;
                _ = PrewarmPagesAsync();
            }
        }

        private async Task PrewarmPagesAsync()
        {
            await Task.Delay(600); // let home page finish rendering first
            foreach (var shellItem in Items)
                foreach (var shellSection in shellItem.Items)
                    foreach (var shellContent in shellSection.Items)
                    {
                        shellContent.ContentTemplate?.CreateContent();
                        await Task.Yield(); // yield between pages to avoid blocking UI
                    }
        }
    }
}
