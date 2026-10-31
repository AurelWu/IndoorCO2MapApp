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

            // Swiping the list closes the keyboard, as in native iOS lists (an iPhone
            // keyboard has no hide key). Handlers don't exist yet at this point.
            RecordingsScrollIOS.HandlerChanged += DismissKeyboardOnDrag;
            GroupedScrollIOS.HandlerChanged += DismissKeyboardOnDrag;
#else
            // Android / Windows: keep virtualized CollectionView (perf for older Androids).
            RecordingsCollection.ItemsSource = vm.Recordings;
            GroupedCollection.ItemsSource = vm.GroupedRecordings;
            RecordingsScrollIOS.IsVisible = false;
            GroupedScrollIOS.IsVisible = false;
#endif
        }

#if IOS
        private static void DismissKeyboardOnDrag(object? sender, EventArgs e)
        {
            if (sender is ScrollView { Handler.PlatformView: UIKit.UIScrollView scroll })
                scroll.KeyboardDismissMode = UIKit.UIScrollViewKeyboardDismissMode.OnDrag;
        }
#endif

        // The iOS lists aren't virtualised and are filled in chunks: load the next one when
        // the user is about a screen away from the end.
        private void OnListScrolled(object? sender, ScrolledEventArgs e)
        {
            if (sender is ScrollView scroll && BindingContext is HistoryViewModel vm
                && e.ScrollY >= scroll.ContentSize.Height - scroll.Height * 2)
                vm.ShowMore();
        }

        // MAUI's SearchBar keeps the keyboard open after Search; the filter is already applied live.
        private void OnFilterSearchButtonPressed(object? sender, EventArgs e)
        {
            FilterSearchBar.Unfocus();
            FilterSearchBar.HideSoftInputAsync(CancellationToken.None)
                .SafeFireAndForget("HistoryPage|OnFilterSearchButtonPressed");
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
