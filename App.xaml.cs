using IndoorCO2MapAppV2.PersistentData;

namespace IndoorCO2MapAppV2
{
    public partial class App : Application
    {
        public static LocalDatabase Database { get; private set; }
        public App()
        {
            InitializeComponent();
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "co2data.db3");
            Database = new LocalDatabase(dbPath);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}