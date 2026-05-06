using AndoW.LiteDb;
using AndoW.Shared;
using LiteDB;
using NewHyOnPlayer.DataManager;
using System;
using System.Collections.Generic;
using System.Linq;
using SharedWeeklyPlayScheduleInfo = AndoW.Shared.WeeklyPlayScheduleInfo;

namespace NewHyOnPlayer
{
    public sealed class PlayerInfoRepository : LiteDbRepository<PlayerInfoClass>, IDisposable
    {
        public PlayerInfoRepository() : base("PlayerInfoManager", "Id") { }

        public void Dispose() { }
    }

    public sealed class TTPlayerRepository : LiteDbRepository<TTPlayerInfoClass>, IDisposable
    {
        public TTPlayerRepository() : base("TTPlayerInfoManager", "id") { }

        public void Dispose() { }
    }

    public sealed class PageRepository : LiteDbRepository<PageInfoClass>, IDisposable
    {
        public PageRepository() : base("PageInfoManager", "PIC_GUID") { }

        public void Dispose() { }
    }

    public sealed class PageListRepository : LiteDbRepository<PageListInfoClass>, IDisposable
    {
        public PageListRepository() : base("PageListInfoManager", "id") { }

        public void Dispose() { }
    }

    public sealed class ContentPeriodRepository : LiteDbRepository<ContentPeriodPayload>, IDisposable
    {
        private static readonly object NormalizeLock = new object();
        private static bool IsNormalized;

        public ContentPeriodRepository() : base("PeriodData", "ContentGuid")
        {
            EnsureNormalized();
        }

        private void EnsureNormalized()
        {
            if (IsNormalized)
            {
                return;
            }

            lock (NormalizeLock)
            {
                if (IsNormalized)
                {
                    return;
                }

                LiteDbContext.Execute(db =>
                {
                    var collection = db.GetCollection("PeriodData");
                    var source = collection.FindAll().ToList();
                    var normalized = new Dictionary<string, BsonDocument>(StringComparer.OrdinalIgnoreCase);
                    bool needsRewrite = false;

                    foreach (var document in source)
                    {
                        if (!TryBuildNormalizedDocument(document, out string contentGuid, out BsonDocument normalizedDocument))
                        {
                            needsRewrite = true;
                            continue;
                        }

                        if (!DocumentsEqual(document, normalizedDocument))
                        {
                            needsRewrite = true;
                        }

                        normalized[contentGuid] = normalizedDocument;
                    }

                    if (!needsRewrite && source.Count == normalized.Count)
                    {
                        return;
                    }

                    collection.DeleteAll();
                    foreach (var document in normalized.Values)
                    {
                        collection.Upsert(document);
                    }
                });
                IsNormalized = true;
            }
        }

        public void DeleteByContentGuids(IEnumerable<string> contentGuids)
        {
            var ids = (contentGuids ?? Enumerable.Empty<string>())
                .Where(x => Guid.TryParse(x, out _))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (ids.Count == 0)
            {
                return;
            }

            LiteDbContext.Execute(db =>
            {
                var collection = db.GetCollection("PeriodData");
                foreach (string id in ids)
                {
                    collection.Delete(new BsonValue(id));
                }
            });
        }

        public void UpsertPeriods(IEnumerable<ContentPeriodPayload> periods)
        {
            var normalized = (periods ?? Enumerable.Empty<ContentPeriodPayload>())
                .Where(x => x != null && Guid.TryParse(x.ContentGuid, out _))
                .GroupBy(x => x.ContentGuid.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Last())
                .ToList();
            if (normalized.Count == 0)
            {
                return;
            }

            LiteDbContext.Execute(db =>
            {
                var collection = db.GetCollection("PeriodData");
                foreach (var period in normalized)
                {
                    collection.Upsert(BuildNormalizedDocument(period));
                }
            });
        }

        private static bool TryBuildNormalizedDocument(BsonDocument source, out string contentGuid, out BsonDocument document)
        {
            contentGuid = ExtractContentGuid(source);
            document = null;
            if (!Guid.TryParse(contentGuid, out _))
            {
                return false;
            }

            document = new BsonDocument
            {
                ["_id"] = contentGuid,
                ["FileName"] = ReadString(source, "FileName"),
                ["StartDate"] = ReadString(source, "StartDate"),
                ["EndDate"] = ReadString(source, "EndDate"),
                ["StartTime"] = ReadString(source, "StartTime"),
                ["EndTime"] = ReadString(source, "EndTime")
            };

            return true;
        }

        private static BsonDocument BuildNormalizedDocument(ContentPeriodPayload period)
        {
            return new BsonDocument
            {
                ["_id"] = period.ContentGuid?.Trim() ?? string.Empty,
                ["FileName"] = period.FileName ?? string.Empty,
                ["StartDate"] = period.StartDate ?? string.Empty,
                ["EndDate"] = period.EndDate ?? string.Empty,
                ["StartTime"] = period.StartTime ?? string.Empty,
                ["EndTime"] = period.EndTime ?? string.Empty
            };
        }

        private static string ExtractContentGuid(BsonDocument document)
        {
            string contentGuid = ReadString(document, "ContentGuid");
            if (Guid.TryParse(contentGuid, out _))
            {
                return contentGuid.Trim();
            }

            if (document != null && document.TryGetValue("_id", out BsonValue idValue))
            {
                string id = ConvertBsonValueToString(idValue);
                if (Guid.TryParse(id, out _))
                {
                    return id.Trim();
                }
            }

            return string.Empty;
        }

        private static string ReadString(BsonDocument document, string key)
        {
            if (document == null || string.IsNullOrWhiteSpace(key) || !document.TryGetValue(key, out BsonValue value))
            {
                return string.Empty;
            }

            return ConvertBsonValueToString(value);
        }

        private static string ConvertBsonValueToString(BsonValue value)
        {
            if (value == null || value.IsNull)
            {
                return string.Empty;
            }

            if (value.IsString)
            {
                return value.AsString ?? string.Empty;
            }

            if (value.IsGuid)
            {
                return value.AsGuid.ToString();
            }

            if (value.IsObjectId)
            {
                return value.AsObjectId.ToString();
            }

            return value.RawValue?.ToString() ?? string.Empty;
        }

        private static bool DocumentsEqual(BsonDocument left, BsonDocument right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            if (left.Keys.Count != right.Keys.Count)
            {
                return false;
            }

            foreach (string key in right.Keys)
            {
                if (!left.TryGetValue(key, out BsonValue leftValue))
                {
                    return false;
                }

                if (ConvertBsonValueToString(leftValue) != ConvertBsonValueToString(right[key]))
                {
                    return false;
                }
            }

            return true;
        }

        public void Dispose() { }
    }

    public sealed class TextRepository : LiteDbRepository<TextInfoClass>, IDisposable
    {
        // TextInfoClass PK는 CIF_Id
        public TextRepository() : base("TextInfoManager", "CIF_Id") { }

        public void Dispose() { }
    }

    public sealed class UpdateQueueRepository : LiteDbRepository<UpdateQueue>, IDisposable
    {
        public UpdateQueueRepository() : base("UpdateQueue", "id") { }

        public void Dispose() { }
    }

    public sealed class CommandHistoryRepository : LiteDbRepository<CommandHistory>, IDisposable
    {
        public CommandHistoryRepository() : base("CommandHistory", "id") { }

        public void Dispose() { }
    }

    public sealed class WeeklyScheduleRepository : LiteDbRepository<SharedWeeklyPlayScheduleInfo>, IDisposable
    {
        public WeeklyScheduleRepository() : base("WeeklyInfoManagerClass", "id") { }

        public void Dispose() { }
    }

    public sealed class LocalPlayerSettingsRepository : LiteDbRepository<LocalPlayerSettings>, IDisposable
    {
        public LocalPlayerSettingsRepository() : base("LocalPlayerSettings", "id") { }

        public void Dispose() { }
    }

    public sealed class SpecialScheduleCache
    {
        public string Id { get; set; } = string.Empty;

        public string PlayerId { get; set; } = string.Empty;

        public string PlayerName { get; set; } = string.Empty;

        public string UpdatedAt { get; set; } = string.Empty;

        public List<SpecialSchedulePayload> Schedules { get; set; } = new List<SpecialSchedulePayload>();

        public List<SchedulePlaylistPayload> Playlists { get; set; } = new List<SchedulePlaylistPayload>();
    }

    public sealed class SpecialScheduleCacheRepository : LiteDbRepository<SpecialScheduleCache>, IDisposable
    {
        public SpecialScheduleCacheRepository() : base("SpecialScheduleCache", "Id") { }

        public void Dispose() { }
    }
}
