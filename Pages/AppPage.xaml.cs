using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.Spatial;
using IndoorCO2MapAppV2.ViewModels;
using IndoorCO2MapAppV2.ExtensionMethods;
using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class AppPage : ContentPage
    {
        // References to bars in the ControlTemplate
        private View? LargeBar;
        private View? SmallBar;
        private View? TickerContainer;
        private Label? TickerLabel;
        private CancellationTokenSource? _tickerCts;

        public AppPage()
        {
            InitializeComponent();

            // Subscribe to StatusViewModel changes
            StatusViewModel.Instance.PropertyChanged += StatusViewModel_PropertyChanged;
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Find template elements
            LargeBar = this.GetTemplateChild("LargeBar") as View;
            SmallBar = this.GetTemplateChild("SmallBar") as View;
            TickerContainer = this.GetTemplateChild("StatusTicker") as View;
            TickerLabel = this.GetTemplateChild("TickerLabel") as Label;

            // Initial update
            UpdateBars();
            UpdateTicker();
        }

        private void StatusViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StatusViewModel.AllReady))
                UpdateBars();

            if (e.PropertyName == nameof(StatusViewModel.HasStatusMessage))
                MainThread.BeginInvokeOnMainThread(UpdateTicker);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            UpdateTicker();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopTicker();
        }

        private void UpdateTicker()
        {
            StopTicker();
            if (StatusViewModel.Instance.HasStatusMessage)
            {
                _tickerCts = new CancellationTokenSource();
                _ = RunTickerAsync(_tickerCts.Token);
            }
        }

        private void StopTicker()
        {
            _tickerCts?.Cancel();
            _tickerCts = null;
            if (TickerLabel != null)
                Microsoft.Maui.Controls.ViewExtensions.CancelAnimations(TickerLabel);
        }

        private async Task RunTickerAsync(CancellationToken ct)
        {
            if (TickerLabel == null || TickerContainer == null) return;
            await Task.Delay(400, ct).ConfigureAwait(false); // let layout settle

            // Measure the label at unconstrained width to get natural text width,
            // then set AbsoluteLayout bounds so the label renders its full text
            // (Grid constrains child WidthRequest but AbsoluteLayout does not)
            double naturalWidth = 200;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var size = TickerLabel.Measure(double.PositiveInfinity, double.PositiveInfinity);
                if (size.Width > 0) naturalWidth = size.Width;
                AbsoluteLayout.SetLayoutBounds(TickerLabel, new Rect(0, 0, naturalWidth, 28));
            });

            while (!ct.IsCancellationRequested)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    double containerWidth = TickerContainer.Width > 0 ? TickerContainer.Width : 400;
                    TickerLabel.TranslationX = containerWidth;
                });

                if (ct.IsCancellationRequested) break;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    double containerWidth = TickerContainer.Width > 0 ? TickerContainer.Width : 400;
                    uint duration = (uint)((containerWidth + naturalWidth) * 12); // ~83 px/s
                    await TickerLabel.TranslateTo(-naturalWidth, 0, duration, Easing.Linear);
                });

                if (ct.IsCancellationRequested) break;
                await Task.Delay(300, ct).ConfigureAwait(false);
            }
        }

        private void UpdateBars()
        {
            if (LargeBar == null || SmallBar == null)
                return;

            bool allReady = StatusViewModel.Instance.AllReady;

            LargeBar.IsVisible = !allReady;
            SmallBar.IsVisible = allReady;
        }

        // --- Navigation helper ---
        public void OnNavigateClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string route)
                NavigateAsync(route).SafeFireAndForget("OnNavigateClicked|NavigateAsync");
        }

        public static async Task NavigateAsync(string route)
        {
            await Shell.Current.GoToAsync(route);
        }

        // --- GPS/Bluetooth click handlers ---
        private void OnRequestGPSEnableDialog(object sender, EventArgs e)
        {
            LocationServicePlatformProvider.CreateOrUse()
                .ShowEnableGpsDialogAsync()
                .SafeFireAndForget("OnRequestGPSEnableDialog|ShowEnableGpsDialogAsync");
        }

        private void OnRequestGPSPermissionDialog(object sender, EventArgs e)
        {
            LocationServicePlatformProvider.CreateOrUse()
                .RequestLocationPermissionAsync()
                .SafeFireAndForget("OnRequestGPSPermissionDialog|RequestLocationPermissionAsync");
        }

        private void OnRequestBluetoothEnableDialog(object sender, EventArgs e)
        {
            BluetoothPlatformProvider.CreateOrUse()
                .RequestBluetoothEnableAsync()
                .SafeFireAndForget("OnRequestBluetoothEnableDialog");
        }

        private void OnRequestBluetoothPermissionsDialog(object sender, EventArgs e)
        {
            BluetoothPlatformProvider.CreateOrUse()
                .RequestPermissionsAsync()
                .SafeFireAndForget("OnRequestBluetoothPermissionsDialog");
        }
    }
}
