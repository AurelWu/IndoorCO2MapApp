using System;
using System.Collections.Generic;
using System.Text;
using SQLite;

namespace IndoorCO2MapAppV2.PersistentData
{
    public class LocalDatabase
    {
        private readonly SQLiteAsyncConnection _database;

        public LocalDatabase(string dbPath)
        {
            _database = new SQLiteAsyncConnection(dbPath);
            _database.CreateTableAsync<PersistentRecording>().Wait();
        }

        public Task<List<PersistentRecording>> GetAllRecordingsAsync() =>
            _database.Table<PersistentRecording>().OrderByDescending(r => r.DateTime).ToListAsync();

        public Task<int> SaveRecordingAsync(PersistentRecording recording) =>
            _database.InsertAsync(recording);
    }
}
