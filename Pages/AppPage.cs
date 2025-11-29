//using IndoorCO2MapAppV2.ExtensionMethods;
//
//namespace IndoorCO2MapAppV2.Pages
//{
//    public partial class AppPage : ContentPage
//    {
//        private void OnNavigateClicked(object sender, EventArgs e)
//        {
//            if (sender is Button button && button.CommandParameter is string route)
//            {
//                NavigateAsync(route).SafeFireAndForget();
//            }
//        }
//
//        protected static async Task NavigateAsync(string route)
//        {
//            await Shell.Current.GoToAsync(route);
//        }
//    }
//}
