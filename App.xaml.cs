using IndoorCO2MapAppV2.PersistentData;

namespace IndoorCO2MapAppV2
{
    public partial class App : Application
    {
        public static LocalDatabase Database { get; private set; }
        public static DatabaseBackupService BackupService { get; private set; }

        public static string DBPath { get; private set; }
        public App()
        {
            InitializeComponent();

            DBPath = Path.Combine(FileSystem.AppDataDirectory, "co2data.db3");
            BackupService = new DatabaseBackupService();
            DatabaseBackupService.ApplyStagedImport();

            Database = new LocalDatabase(DBPath);
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        public static void ImportDB(LocalDatabase db)
        {
            Database = db;
        }
    }
}