using CommunityToolkit.Maui.Storage;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.PersistentData
{
    public class DatabaseBackupService
    {
        private const string TempImportFileName = "co2data_import.db3";

        /// <summary>
        /// Export the SQLite database using a temporary copy.
        /// </summary>
        public async Task<bool> ExportDatabaseAsync()
        {
            string tempPath = Path.Combine(FileSystem.CacheDirectory, "co2data_backup.db3");

            try
            {
                // Copy live DB to temp
                File.Copy(App.DBPath, tempPath, true);

                // Save file using FileSaver
                FileStream stream = File.OpenRead(tempPath);
                var result = await FileSaver.Default.SaveAsync(
                    "IndoorCO2Map.db",
                    stream,
                    CancellationToken.None
                );

                // Close stream
                stream.Close();
                stream.Dispose();

                // Delete temp
                File.Delete(tempPath);

                return !string.IsNullOrWhiteSpace(result?.FilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Export failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Import a DB file to staging area. User must restart app to load it.
        /// </summary>
        public async Task<bool> ImportDatabaseAsync()
        {
            try
            {
                var file = await FilePicker.Default.PickAsync();
                if (file == null)
                    return false;

                string tempImportPath = Path.Combine(FileSystem.CacheDirectory, TempImportFileName);

                // Copy selected file to temp staging
                using var read = await file.OpenReadAsync();
                using var write = File.Open(tempImportPath, FileMode.Create, FileAccess.Write);
                await read.CopyToAsync(write);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Import failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Check for staging DB and replace live DB. Call this in App constructor before creating LocalDatabase.
        /// </summary>
        public static void ApplyStagedImport()
        {
            string tempImportPath = Path.Combine(FileSystem.CacheDirectory, TempImportFileName);
            if (File.Exists(tempImportPath))
            {
                File.Copy(tempImportPath, App.DBPath, true);
                File.Delete(tempImportPath);
            }
        }
    }
}
