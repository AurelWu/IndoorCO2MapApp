using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.ViewModels;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class NewsPage : AppPage
    {
        public NewsPage()
        {
            InitializeComponent();
            BindingContext = new NewsViewModel();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (BindingContext is NewsViewModel vm)
            {
                vm.LoadAsync().SafeFireAndForget("NewsPage|OnAppearing|vm.LoadAsync");
            }
        }
    }
}
