using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.ViewModels;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class StatisticsPage : AppPage
    {
        public StatisticsPage()
        {
            InitializeComponent();
            BindingContext = new StatisticsViewModel();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (BindingContext is StatisticsViewModel vm)
            {
                vm.LoadAsync().SafeFireAndForget("StatisticsPage|OnAppearing|vm.LoadAsync");
            }
        }
    }
}
