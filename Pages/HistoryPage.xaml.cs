using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.ViewModels;
using Microsoft.Maui.Controls;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class HistoryPage : AppPage
    {
        protected override bool ShowStatusBars => false;
        public HistoryPage()
        {
            InitializeComponent();
            var vm = new HistoryViewModel();
            BindingContext = vm;

#if IOS
            // iOS: BindableLayout in ScrollView. UICollectionView's self-sizing cell reuse
            // caused expand/collapse whitespace and phantom expansion of reused cells that
            // could not be reliably defeated via InvalidateLayout hacks.
            BindableLayout.SetItemsSource(RecordingsListIOS, vm.Recordings);
            BindableLayout.SetItemsSource(GroupedListIOS, vm.GroupedRecordings);
            RecordingsCollection.IsVisible = false;
            GroupedCollection.IsVisible = false;
#else
            // Android / Windows: keep virtualized CollectionView (perf for older Androids).
            RecordingsCollection.ItemsSource = vm.Recordings;
            GroupedCollection.ItemsSource = vm.GroupedRecordings;
            RecordingsScrollIOS.IsVisible = false;
            GroupedScrollIOS.IsVisible = false;
#endif
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (BindingContext is HistoryViewModel vm)
            {
                vm.ReloadRecordingsAsync().SafeFireAndForget("HistoryPage|OnAppearing|vm.ReloadRecordingsAsync");
            }
        }

        protected override bool OnBackButtonPressed()
        {
            _ = NavigateAsync("///home");
            return true;
        }
    }
}
