using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using TurtleTools;

namespace AndoW_Manager
{
    public sealed class ContentPeriodManager : RethinkDbManagerBase<PeriodData>
    {
        private const string TableName = "PeriodData";

        public ContentPeriodManager()
            : base(RethinkDbConfigurator.GetDataDatabaseName(), TableName, "id")
        {
        }

        public List<PeriodData> LoadAll()
        {
            return LoadAllDocuments();
        }

        public PeriodData FindByContentGuid(string contentGuid)
        {
            if (string.IsNullOrWhiteSpace(contentGuid))
            {
                return null;
            }

            return FindOne(x => string.Equals(x.ContentGuid, contentGuid, StringComparison.OrdinalIgnoreCase));
        }

        public List<PeriodData> FindByContentGuids(IEnumerable<string> contentGuids)
        {
            var guidSet = new HashSet<string>(contentGuids ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (guidSet.Count == 0)
            {
                return new List<PeriodData>();
            }

            return Find(x => x != null && !string.IsNullOrWhiteSpace(x.ContentGuid) && guidSet.Contains(x.ContentGuid));
        }

        public void Save(PeriodData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.ContentGuid))
            {
                return;
            }

            data.Id = data.ContentGuid;
            Upsert(data);
        }

        public void DeleteByContentGuid(string contentGuid)
        {
            if (string.IsNullOrWhiteSpace(contentGuid))
            {
                return;
            }

            DeleteById(contentGuid);
        }
    }

    public sealed class PeriodData
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        public string ContentGuid { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string StartDate { get; set; } = string.Empty;

        public string EndDate { get; set; } = string.Empty;
    }
}
