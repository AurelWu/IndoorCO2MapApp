using SQLite;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IndoorCO2MapAppV2.PersistentData
{
    public class LocalDatabase
    {
        private SQLiteAsyncConnection _database;
        private readonly string _dbPath;

        public string DatabasePath => _dbPath;

        public LocalDatabase(string dbPath)
        {
            _dbPath = dbPath;
            _database = new SQLiteAsyncConnection(_dbPath);
            _database.CreateTableAsync<PersistentRecording>().Wait();
            _database.GetConnection().Close();
            _database.CloseAsync();
            SQLiteAsyncConnection.ResetPool();
        }

        public Task<List<PersistentRecording>> GetAllRecordingsAsync() =>
            _database.Table<PersistentRecording>()
                     .OrderByDescending(r => r.DateTime)
                     .ToListAsync();

        public Task<int> SaveRecordingAsync(PersistentRecording recording) =>
            _database.InsertAsync(recording);

        public void Dispose()
        {
            // Dispose the underlying connection
            var conn = _database?.GetConnection();
            conn?.Close();
            conn?.Dispose();
            _database = null;
        }
    }
}
