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
            // Double-defer so InvalidateLayout fires after MAUI's DisplayLink-driven
            // layout pass has committed HeightRequest=0 to the native UIView constraint.
            // A single BeginInvokeOnMainThread races the ~16ms DisplayLink tick and
            // UICollectionView re-queries before MAUI has applied the new height.
            MainThread.BeginInvokeOnMainThread(() =>
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (RecordingsCollection?.Handler?.PlatformView is UIKit.UICollectionView cv)
                        cv.CollectionViewLayout.InvalidateLayout();
                    if (GroupedCollection?.Handler?.PlatformView is UIKit.UICollectionView gcv)
                        gcv.CollectionViewLayout.InvalidateLayout();
                }));
#endif
        }

        private void OnHistoryScrolled(object? sender, ItemsViewScrolledEventArgs e)
        {
#if IOS
            // Cell reuse during scroll can leave phantom expanded heights.
            // Force UICollectionView to re-query sizes after MAUI has updated
            // the reused cell's bindings (double-defer for same timing reason).
            MainThread.BeginInvokeOnMainThread(() =>
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (RecordingsCollection?.Handler?.PlatformView is UIKit.UICollectionView cv)
                        cv.CollectionViewLayout.InvalidateLayout();
                    if (GroupedCollection?.Handler?.PlatformView is UIKit.UICollectionView gcv)
                        gcv.CollectionViewLayout.InvalidateLayout();
                }));
#endif
        }

        protected override bool OnBackButtonPressed()
        {
            _ = NavigateAsync("///home");
            return true;
        }
    }
}