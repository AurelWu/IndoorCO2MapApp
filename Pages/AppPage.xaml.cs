using IndoorCO2MapAppV2.ExtensionMethods;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class AppPage : ContentPage
    {
        public AppPage()
        {
            InitializeComponent(); 
        }

        private void OnNavigateClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string route)
            {
                NavigateAsync(route).SafeFireAndForget();
            }
        }

        protected static async Task NavigateAsync(string route)
        {
            await Shell.Current.GoToAsync(route);
        }

        //protected void SetPageContent(View view)
        //{
        //    PageContent.Content = view;
        //}

        private void OnRequestGPSEnableDialog(object sender, EventArgs e)
        {

        }

        private void OnRequestGPSPermissionDialog(object sender, EventArgs e)
        {

        }

        private void OnRequestBluetoothEnableDialog(object sender, EventArgs e)
        {

        }

        private void OnRequestBluetoothPermissionsDialog(object sender, EventArgs e)
        {

        }
    }
}
