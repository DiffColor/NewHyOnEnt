using LiteDB;
using Newtonsoft.Json;
using TurtleTools;

namespace AndoWSettings
{
    public class ServerSettingsManager
    {
        public ServerSettings sData { get; private set; }
        private const string CollectionName = "ServerSettings";

        public ServerSettings LoadData()
        {
            using (var db = LocalDbContext.OpenDatabase())
            {
                var collection = db.GetCollection<ServerSettings>(CollectionName);
                sData = collection.FindById(0);

                if (sData == null)
                {
                    sData = collection.FindOne(Query.All());
                    if (sData != null)
                    {
                        sData.Id = 0;
                    }
                    else
                    {
                        sData = new ServerSettings();
                    }
                }

                if (string.IsNullOrWhiteSpace(sData.DataServerIp))
                {
                    sData.DataServerIp = "127.0.0.1";
                }

                if (string.IsNullOrWhiteSpace(sData.MessageServerIp))
                {
                    sData.MessageServerIp = "127.0.0.1";
                }

                collection.Upsert(sData);
            }

            return sData;
        }

        public void SaveData(ServerSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            using (var db = LocalDbContext.OpenDatabase())
            {
                var collection = db.GetCollection<ServerSettings>(CollectionName);
                collection.Upsert(settings);
            }

            sData = settings;
        }
    }

    public class ServerSettings
    {
        [JsonProperty("id")]
        [BsonId(false)]
        public int Id { get; set; } = 0; // ?쒓컻???곗씠?곕쭔 ??ν븯湲??꾪븳 ?꾨뱶
        public int FTP_Port { get; set; } = NetworkTools.FTP_PORT;
        public int FTP_PasvMinPort { get; set; } = NetworkTools.FTP_PASV_MIN_PORT;
        public int FTP_PasvMaxPort { get; set; } = NetworkTools.FTP_PASV_MAX_PORT;
        public bool PreserveAspectRatio { get; set; } = false;
        public string DataServerIp { get; set; } = "127.0.0.1";
        public string MessageServerIp { get; set; } = "127.0.0.1";
    }
}
