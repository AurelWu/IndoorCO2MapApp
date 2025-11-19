using Microsoft.Maui.Controls;
using IndoorCO2MapAppV2.ExtensionMethods;

namespace IndoorCO2MapAppV2
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
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

        private static async Task NavigateAsync(string route)
        {
            await Shell.Current.GoToAsync(route);
        }
    }
}
