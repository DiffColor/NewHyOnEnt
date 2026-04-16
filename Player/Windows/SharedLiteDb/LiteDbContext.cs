using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LiteDB;
using AndoW.Shared;

namespace AndoW.LiteDb
{
    /// <summary>
    /// 단일 파일 LiteDB 컨텍스트.
    /// AppContext.BaseDirectory 기준 경로(local.db)에 DB 파일을 생성하고 공유 커넥션 문자열을 유지한다.
    /// </summary>
    public static class LiteDbContext
    {
        private static readonly object SyncRoot = new object();
        private static readonly HashSet<string> EnsuredCollections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static string _connectionString = string.Empty;
        private static string _databasePath = string.Empty;
        private static bool _initialized;
        private static bool _mapperConfigured;

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

                string baseDirectory = ResolveBaseDirectory();
                _databasePath = Path.Combine(baseDirectory, "local.db");
                Directory.CreateDirectory(Path.GetDirectoryName(_databasePath) ?? baseDirectory);
                _connectionString = $"Filename={_databasePath};Connection=shared;Password=turtle04!9";
                ConfigureMapper();
                _initialized = true;
            }
        }

        private static string ResolveBaseDirectory()
        {
#if PLAYER_SETTINGS_USE_APP_CONTEXT_BASE_DIRECTORY
            string baseDirectory = AppContext.BaseDirectory;
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                throw new InvalidOperationException("AppContext.BaseDirectory를 확인할 수 없습니다.");
            }

            return Path.GetFullPath(baseDirectory);
#else
            string executablePath = ResolveExecutablePath();
            string directory = Path.GetDirectoryName(executablePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("실행 파일 디렉터리를 확인할 수 없습니다.");
            }

            return Path.GetFullPath(directory);
#endif
        }

        private static string ResolveExecutablePath()
        {
#if NET6_0_OR_GREATER
            string? processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
            {
                throw new InvalidOperationException("Environment.ProcessPath를 확인할 수 없습니다.");
            }

            return Path.GetFullPath(processPath);
#else
            try
            {
                using (Process currentProcess = Process.GetCurrentProcess())
                {
                    string mainModulePath = currentProcess.MainModule?.FileName;
                    if (string.IsNullOrWhiteSpace(mainModulePath) == false)
                    {
                        return Path.GetFullPath(mainModulePath);
                    }
                }
            }
            catch
            {
            }

            throw new InvalidOperationException("실행 중인 파일 경로를 확인할 수 없습니다.");
#endif
        }

        private static void ConfigureMapper()
        {
            if (_mapperConfigured)
            {
                return;
            }

            BsonMapper.Global.RegisterType<string>(
                s => new BsonValue(s),
                v =>
                {
                    if (v == null || v.IsNull) return string.Empty;
                    if (v.IsObjectId) return v.AsObjectId.ToString();
                    if (v.IsGuid) return v.AsGuid.ToString();
                    return v.AsString;
                });

            BsonMapper.Global.Entity<PlayerInfoClass>().Id(x => x.Id, false);
            BsonMapper.Global.Entity<WeeklyPlayScheduleInfo>().Id(x => x.Id, false);

            _mapperConfigured = true;
        }

        public static void EnsureCollection<T>(string collectionName)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                throw new ArgumentException("Collection name is required.", nameof(collectionName));
            }

            EnsureInitialized();

            lock (SyncRoot)
            {
                string key = $"{typeof(T).FullName}:{collectionName}";
                if (!EnsuredCollections.Add(key))
                {
                    return;
                }

                using (var db = new LiteDatabase(_connectionString))
                {
                    db.GetCollection<T>(collectionName);
                }
            }
        }

        public static T Execute<T>(Func<LiteDatabase, T> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            EnsureInitialized();

            using (var db = new LiteDatabase(_connectionString))
            {
                return action(db);
            }
        }

        public static void Execute(Action<LiteDatabase> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            EnsureInitialized();

            using (var db = new LiteDatabase(_connectionString))
            {
                action(db);
            }
        }
    }
}
