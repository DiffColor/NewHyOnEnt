using System;
using System.IO;
using AndoWSettings;
using LiteDB;

namespace TurtleTools
{
    internal static class LocalDbContext
    {
        private const string DbPassword = "turtle04!9";
        private static readonly object SyncRoot = new object();
        private static bool _initialized;
        private static string _connectionString;
        private static string _plainConnectionString;
        private static string _dbPath;

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                _dbPath = FNDTools.GetLocalDbPath();
                string directory = Path.GetDirectoryName(_dbPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _connectionString = $"Filename={_dbPath};Connection=shared;Password={DbPassword}";
                _plainConnectionString = $"Filename={_dbPath};Connection=shared";
                _initialized = true;
            }
        }

        public static LiteDatabase OpenDatabase()
        {
            EnsureInitialized();
            try
            {
                return new LiteDatabase(_connectionString);
            }
            catch (LiteException ex)
            {
                if (!TryUpgradeToEncrypted())
                {
                    Logger.WriteErrorLog($"local.db open failed: {ex}", Logger.GetLogFileName());
                    throw;
                }
            }

            return new LiteDatabase(_connectionString);
        }

        private static bool TryUpgradeToEncrypted()
        {
            if (string.IsNullOrWhiteSpace(_dbPath) || !File.Exists(_dbPath))
            {
                return false;
            }

            try
            {
                using (var db = new LiteDatabase(_plainConnectionString))
                {
                    db.Rebuild(new LiteDB.Engine.RebuildOptions { Password = DbPassword });
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"local.db encrypt failed: {ex}", Logger.GetLogFileName());
                return false;
            }
        }
    }
}
