using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.ViewModels;
using Microsoft.Maui.Controls;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class HistoryPage : AppPage
    {
        public HistoryPage()
        {
            InitializeComponent();
            BindingContext = new HistoryViewModel();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (BindingContext is HistoryViewModel vm)
            {
                vm.ReloadRecordingsAsync().SafeFireAndForget("HistoryPage|OnAppearing|vm.ReloadRecordingsAsync");
            }
        }
    }
}