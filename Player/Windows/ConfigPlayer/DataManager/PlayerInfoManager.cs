using System;
using AndoW.LiteDb;
using AndoW.Shared;
using System.Linq;

namespace ConfigPlayer
{
    public class PlayerInfoManager
    {
        private readonly PlayerInfoRepository repository = new PlayerInfoRepository();
        public PlayerInfoClass g_PlayerInfo = new PlayerInfoClass();

        public PlayerInfoManager()
        {
            LoadData();
        }

        public void LoadData()
        {
            var storedList = repository.LoadAll() ?? new System.Collections.Generic.List<PlayerInfoClass>();
            if (storedList.Count == 0)
            {
                NewPlayerInfo();
                return;
            }

            var preferred = SelectPreferredRecord(storedList);
            g_PlayerInfo = preferred;

            if (storedList.Count > 1)
            {
                preferred.Id = 0;
                repository.ReplaceAll(new[] { preferred });
            }
        }

        public void NewPlayerInfo()
        {
            g_PlayerInfo = new PlayerInfoClass();
            SaveData();
        }

        public void EditPlayerPlayList(string playlist)
        {
            g_PlayerInfo.PIF_CurrentPlayList = playlist;
            SaveData();
        }

        public void EditMacAddress(string macAddress)
        {
            g_PlayerInfo.PIF_MacAddress = macAddress;
            SaveData();
        }

        public void SaveData()
        {
            if (string.IsNullOrWhiteSpace(g_PlayerInfo.PIF_GUID))
            {
                g_PlayerInfo.PIF_GUID = Guid.NewGuid().ToString();
            }

            if (string.IsNullOrWhiteSpace(g_PlayerInfo.PIF_AuthKey))
            {
                g_PlayerInfo.PIF_AuthKey = Guid.NewGuid().ToString("N");
            }

            g_PlayerInfo.Id = 0;
            repository.ReplaceAll(new[] { g_PlayerInfo });
        }

        private PlayerInfoClass SelectPreferredRecord(System.Collections.Generic.List<PlayerInfoClass> records)
        {
            if (records == null || records.Count == 0)
            {
                return new PlayerInfoClass();
            }

            return records
                .OrderByDescending(x => x.Id == 0)
                .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.PIF_GUID))
                .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.PIF_PlayerName))
                .First();
        }

        private class PlayerInfoRepository : LiteDbRepository<PlayerInfoClass>
        {
            public PlayerInfoRepository()
                : base("PlayerInfoManager", "Id")
            {
            }
        }
    }
}
