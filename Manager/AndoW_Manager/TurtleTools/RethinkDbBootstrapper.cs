using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AndoW_Manager;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;

namespace TurtleTools
{
    public static class RethinkDbBootstrapper
    {
        private static readonly TimeSpan DefaultStartupTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan DefaultTablesReadyTimeout = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan TableReadyCheckInterval = TimeSpan.FromSeconds(2);
        private static readonly string[] DefaultTableNames =
        {
            nameof(PageInfoManager),
            nameof(PageListInfoManager),
            nameof(PlayerInfoManager),
            nameof(PlayerGroupManager),
            nameof(SpecialScheduleInfoManager),
            nameof(TextInfoManager),
            nameof(WeeklyInfoManagerClass),
            "CommandQueue",
            "UpdateThrottleSettings",
            "UpdateLease",
        };

        public static bool EnsureRethinkDbReady()
        {
            return EnsureRethinkDbReadyInternal(DefaultStartupTimeout);
        }

        public static bool EnsureRethinkDbReadyWithWait(TimeSpan? startupTimeout = null)
        {
            return EnsureRethinkDbReadyInternal(startupTimeout ?? DefaultStartupTimeout);
        }

        public static bool IsRethinkDbRunning()
        {
            return RethinkDbContext.TryConnect(false);
        }

        public static async Task<bool> EnsureAndWaitTablesReadyAsync(string databaseName, TimeSpan? totalTimeout = null, CancellationToken cancellationToken = default)
        {
            if (!EnsureRethinkDbReadyInternal(DefaultStartupTimeout))
            {
                return false;
            }

            var timeout = totalTimeout ?? DefaultTablesReadyTimeout;
            var perWaitTimeoutSeconds = (int)Math.Ceiling(TableReadyCheckInterval.TotalSeconds);

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (TryWaitForTables(databaseName, perWaitTimeoutSeconds))
                {
                    return true;
                }

                await Task.Delay(TableReadyCheckInterval, cancellationToken);
            }

            return false;
        }

        private static bool EnsureRethinkDbReadyInternal(TimeSpan startupTimeout)
        {
            if (WaitForRethinkDbReady(startupTimeout))
            {
                return true;
            }

            Logger.WriteErrorLog($"RethinkDB 연결 실패 (timeout {startupTimeout.TotalSeconds:F0}s).", Logger.GetLogFileName());
            return false;
        }

        private static bool TryWaitForTables(string databaseName, int perWaitTimeoutSeconds)
        {
            try
            {
                if (!RethinkDbContext.TryConnect(false))
                {
                    return false;
                }

                EnsureDatabaseAndTables(databaseName);
                Connection conn = RethinkDbContext.GetRawConnection();
                var tableNames = GetTableNames(conn, databaseName);

                RethinkDB.R.Db(databaseName)
                    .Wait_()
                    .OptArg("wait_for", "ready_for_writes")
                    .OptArg("timeout", perWaitTimeoutSeconds)
                    .Run(conn);

                foreach (var table in tableNames)
                {
                    RethinkDB.R.Db(databaseName)
                        .Table(table)
                        .Wait_()
                        .OptArg("wait_for", "ready_for_writes")
                        .OptArg("timeout", perWaitTimeoutSeconds)
                        .Run(conn);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                return false;
            }
        }

        private static List<string> GetTableNames(Connection conn, string databaseName)
        {
            try
            {
                var names = RethinkDB.R.Db(databaseName).TableList().RunAtom<List<string>>(conn);
                if (names == null)
                {
                    return new List<string>();
                }

                return names.Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
                return new List<string>();
            }
        }

        private static void EnsureDatabaseAndTables(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                return;
            }

            try
            {
                RethinkDbConfigurator.EnsureConfigured();

                foreach (string tableName in DefaultTableNames)
                {
                    if (string.IsNullOrWhiteSpace(tableName))
                    {
                        continue;
                    }

                    RethinkDbContext.EnsureTable(databaseName, tableName, "id");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        private static bool WaitForRethinkDbReady(TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                if (RethinkDbContext.TryConnect(false))
                {
                    return true;
                }

                Thread.Sleep(300);
            }

            return false;
        }
    }
}
