using IndoorCO2MapAppV2.PersistentData;
using IndoorCO2MapAppV2.Spatial;

namespace IndoorCO2MapAppV2
{
    public partial class App : Application
    {
        public static LocalDatabase HistoryDatabase { get; private set; }
        public static LocationCacheDatabase LocationCacheDb { get; private set; }
        public static DatabaseBackupService BackupService { get; private set; }

        public static string historyDBPath { get; private set; }
        public static string locationCacheDbPath { get; private set; }

        public App()
        {
            InitializeComponent();

            historyDBPath = Path.Combine(FileSystem.AppDataDirectory, "co2data.db3");
            locationCacheDbPath = Path.Combine(FileSystem.AppDataDirectory, "location_cache.db3");
            BackupService = new DatabaseBackupService();
            DatabaseBackupService.ApplyStagedImport();

            HistoryDatabase = new LocalDatabase(historyDBPath);

            LocationCacheDb = new LocationCacheDatabase(locationCacheDbPath);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        public static void ImportDB(LocalDatabase db)
        {
            HistoryDatabase = db;
        }
    }
}