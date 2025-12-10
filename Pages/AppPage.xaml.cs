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

            // Initial update
            UpdateBars();
        }

        private void StatusViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StatusViewModel.AllReady))
            {
                UpdateBars();
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
        private void OnNavigateClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string route)
                NavigateAsync(route).SafeFireAndForget();
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
                .SafeFireAndForget();
        }

        private void OnRequestGPSPermissionDialog(object sender, EventArgs e)
        {
            LocationServicePlatformProvider.CreateOrUse()
                .RequestLocationPermissionAsync()
                .SafeFireAndForget();
        }

        private void OnRequestBluetoothEnableDialog(object sender, EventArgs e)
        {
            BluetoothPlatformProvider.CreateOrUse()
                .RequestBluetoothEnableAsync()
                .SafeFireAndForget();
        }

        private void OnRequestBluetoothPermissionsDialog(object sender, EventArgs e)
        {
            BluetoothPlatformProvider.CreateOrUse()
                .RequestPermissionsAsync()
                .SafeFireAndForget();
        }
    }
}
