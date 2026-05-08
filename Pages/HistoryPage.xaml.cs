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

        private void OnExpandTapped(object? sender, TappedEventArgs e)
        {
#if IOS
            // iOS UICollectionView caches cell heights and won't resize when MAUI
            // view sizes change. Posting InvalidateLayout() on the next run-loop
            // cycle (after MAUI processes binding updates) forces a re-measure.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (RecordingsCollection?.Handler?.PlatformView is UIKit.UICollectionView cv)
                    cv.CollectionViewLayout.InvalidateLayout();
                if (GroupedCollection?.Handler?.PlatformView is UIKit.UICollectionView gcv)
                    gcv.CollectionViewLayout.InvalidateLayout();
            });
#endif
        }

        protected override bool OnBackButtonPressed()
        {
            _ = NavigateAsync("///home");
            return true;
        }
    }
}