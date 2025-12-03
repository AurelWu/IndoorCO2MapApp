using IndoorCO2MapAppV2.PersistentData;

namespace IndoorCO2MapAppV2
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            _ = SettingsManager.Instance.LoadAsync();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}