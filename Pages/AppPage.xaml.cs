using IndoorCO2MapAppV2.Bluetooth;
using IndoorCO2MapAppV2.ExtensionMethods;
using IndoorCO2MapAppV2.Spatial;

namespace IndoorCO2MapAppV2.Pages
{
    public partial class AppPage : ContentPage
    {

        //private readonly IBluetoothHelper bluetoothHelper;

        // References to template controls
        private ImageButton ButtonBluetoothStatus = null!; //set in OnApplyTemplate
        private ImageButton ButtonBluetoothPermissions = null!;
        private ImageButton ButtonGPSStatus = null!;
        private ImageButton ButtonGPSPermission = null!;
        public AppPage()
        {
            InitializeComponent();
            //bluetoothHelper = BluetoothPlatformProvider.CreateOrUse();
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Get references from ControlTemplate
            ButtonBluetoothStatus =
                this.FindByName<ImageButton>("ButtonBluetoothStatus");

            ButtonBluetoothPermissions =
                this.FindByName<ImageButton>("ButtonBluetoothPermissions");

            ButtonGPSStatus =
                this.FindByName<ImageButton>("ButtonGPSStatus");

            ButtonGPSPermission =
                this.FindByName<ImageButton>("ButtonGPSPermission");

        }


        private void OnNavigateClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string route)
            {
                NavigateAsync(route).SafeFireAndForget();
            }
        }

        protected static async Task NavigateAsync(string route)
        {
            await Shell.Current.GoToAsync(route);
        }


        private void OnRequestGPSEnableDialog(object sender, EventArgs e)
        {
            //LocationServicePlatformProvider.CreateOrUse() => should probably be changed to just get instance of the singleton instead which then internally creates it if it doesnt already...)
            LocationServicePlatformProvider.CreateOrUse().ShowEnableGpsDialogAsync().SafeFireAndForget();
        }

        private void OnRequestGPSPermissionDialog(object sender, EventArgs e)
        {
            LocationServicePlatformProvider.CreateOrUse().RequestLocationPermissionAsync().SafeFireAndForget();
        }

        private void OnRequestBluetoothEnableDialog(object sender, EventArgs e)
        {
            BluetoothPlatformProvider.CreateOrUse().RequestBluetoothEnableAsync().SafeFireAndForget();
        }

        private void OnRequestBluetoothPermissionsDialog(object sender, EventArgs e)
        {
            BluetoothPlatformProvider.CreateOrUse().RequestPermissionsAsync().SafeFireAndForget();
        }        
    }
}
