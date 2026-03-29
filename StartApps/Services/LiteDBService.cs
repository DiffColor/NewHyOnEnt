using System.Diagnostics;
using LiteDB;

namespace StartApps.Services
{
    public class LiteDBService : IDisposable
    {
        public static LiteDBService Instance { get; private set; } = new LiteDBService();

        public Dictionary<string, LiteDatabase> sDBDics = new Dictionary<string, LiteDatabase>();


        public LiteDBService()
        {
            string _db_fpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{LDB_Global.SINGLE_DB_NAME}.db");
            Start(_db_fpath, LDB_Global.SINGLE_DB_TABLES, LDB_Global.DB_PW);
        }

        ~LiteDBService() { Dispose(); }


        public void Start(string db_fpath, List<string> tables, string password, bool isShared = true)
        {
            var mapper = BsonMapper.Global;
            mapper.EmptyStringToNull = false;

            string conn_str = ConvertConnectionString(db_fpath, password, isShared);

            SetDBAndTB(Path.GetFileNameWithoutExtension(db_fpath), conn_str, tables);

            //StartFileWatcher($"{db_fpath}");
        }

        public void Start(Dictionary<string, List<string>> dbinfos, string password, bool isShared = true)
        {
            var mapper = BsonMapper.Global;
            mapper.EmptyStringToNull = false;

            foreach (KeyValuePair<string, List<string>> kvp in dbinfos)
            {
                string conn_str = ConvertConnectionString(kvp.Key, password, isShared);
                SetDBAndTB(Path.GetFileNameWithoutExtension(kvp.Key), conn_str, kvp.Value);
                //StartFileWatcher($"{kvp.Key}");
            }
        }

        void SetDBAndTB(string db_name, string conn_str, List<string> tables)
        {
            var db = new LiteDatabase(conn_str);
            sDBDics.Add(db_name, db);

            foreach (string tb in tables)
            {
                if (db.CollectionExists(tb) == false)
                    db.GetCollection(tb);
            }
        }

        #region FileSystemWatcher For FileDB
        //private void StartFileWatcher(string dbFilePath)
        //{
        //    var watcher = new FileSystemWatcher
        //    {
        //        Path = Path.GetDirectoryName(dbFilePath),
        //        Filter = Path.GetFileName(dbFilePath),
        //        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
        //    };

        //    watcher.Changed += (sender, args) => OnDatabaseFileChanged(dbFilePath);
        //    watcher.EnableRaisingEvents = true;
        //    sWatchers[dbFilePath] = watcher;

        //    Console.WriteLine($"[LiteDBTools] '{dbFilePath}' 파일 변경 감지 시작...");
        //}

        //private async Task OnDatabaseFileChanged(string dbFilePath)
        //{
        //    await Task.Delay(500); // 파일 변경이 완료될 시간을 확보

        //    string db_name = Path.GetFileNameWithoutExtension(dbFilePath);
        //    if (sDBDics.ContainsKey(db_name))
        //    {
        //        Console.WriteLine("[Watcher] LiteDB 파일 변경 감지됨. 최신 데이터 확인 중...");
        //        await NotifyObservers(db_name);
        //    }
        //}

        //private async Task NotifyObservers(string db_name)
        //{
        //    var db = sDBDics[db_name];

        //    foreach (var kvp_handle in sEventHandlers) 
        //    {
        //        string collectionName = kvp_handle.Key;
        //        var col = db.GetCollection(collectionName);

        //        Dictionary<string, HashSet<EventHandler<CollectionEventArgs>>> purelist = new Dictionary<string, HashSet<EventHandler<CollectionEventArgs>>>();

        //        foreach (var (cond, handler) in kvp_handle.Value)
        //        {
        //            if (!purelist.ContainsKey(cond))
        //                purelist[cond] = new HashSet<EventHandler<CollectionEventArgs>>(); // 중복 방지를 위해 HashSet 사용

        //            purelist[cond].Add(handler);
        //        }

        //        foreach (var kvp_cond in purelist) {
        //            BsonExpression _be = BsonExpression.Create(kvp_cond.Key);
        //            var eventArgs = new CollectionEventArgs(collectionName, col.Find(_be));
        //            foreach (var handler in kvp_cond.Value)
        //                handler?.Invoke(this, eventArgs);
        //        }
        //    }
        //}

        //public void RegisterEventHandler(string collectionName, string bson_condition, EventHandler<CollectionEventArgs> handler)
        //{
        //    if (!sEventHandlers.ContainsKey(collectionName))
        //        sEventHandlers[collectionName] = new List<(string, EventHandler<CollectionEventArgs>)>();

        //    if (sEventHandlers[collectionName].Any(h => h.Item1.Equals(bson_condition, StringComparison.InvariantCultureIgnoreCase) && h.Item2 == handler))
        //        return;

        //    sEventHandlers[collectionName].Add((bson_condition, handler));
        //}

        //public void UnregisterEventHandler(EventHandler<CollectionEventArgs> handler)
        //{
        //    sEventHandlers.Values.ToList().ForEach(h => h.RemoveAll(x => x.Handler == handler));
        //}


        //public void DisposeWatcher()
        //{
        //    sEventHandlers.Clear();

        //    foreach (var watcher in sWatchers.Values)
        //        watcher.Dispose();
        //}
        #endregion

        public static List<T> ConvertBsonListToEntities<T>(IEnumerable<BsonDocument> bsonDocument)
        {
            return bsonDocument.Select(doc => BsonMapper.Global.ToObject<T>(doc)).ToList();
        }


        public void Dispose()
        {
            //DisposeWatcher();
            DisposeDB();
        }

        public void DisposeDB()
        {
            foreach (LiteDatabase db in sDBDics.Values)
                db.Dispose();

            Debug.WriteLine("LiteDB 데이터베이스가 모두 해제되었습니다.");
        }

        string ConvertConnectionString(string db_name, string password, bool shared = true)
        {
            return string.Format("Filename={0};Password={1};Connection={2}", db_name, password, shared ? "shared" : "direct");
        }

        public ILiteCollection<T> GetCollection<T>(string db_name, string tb_name)
        {
            return sDBDics[db_name].GetCollection<T>(tb_name);
        }

        public BsonValue Insert<T>(string db_name, string tb_name, T data)
        {
            return sDBDics[db_name].GetCollection<T>(tb_name).Insert(data);
        }

        public BsonValue Insert<T>(string db_name, string tb_name, List<T> data)
        {
            return sDBDics[db_name].GetCollection<T>(tb_name).InsertBulk(data, data.Count);
        }

        public BsonValue Insert(string db_name, string tb_name, IEnumerable<BsonDocument> data)
        {
            return sDBDics[db_name].GetCollection(tb_name).InsertBulk(data);
        }

        public bool Upsert<T>(string db_name, string tb_name, T data)
        {
            return sDBDics[db_name].GetCollection<T>(tb_name).Upsert(data);
        }

        public int Upsert<T>(string db_name, string tb_name, List<T> data)
        {
            return sDBDics[db_name].GetCollection<T>(tb_name).Upsert(data);
        }

        public bool Delete(string db_name, string tb_name, int id)
        {
            return sDBDics[db_name].GetCollection(tb_name).Delete(id);
        }

        public int DeleteAll(string db_name, string tb_name)
        {
            return sDBDics[db_name].GetCollection(tb_name).DeleteAll();
        }

        public IEnumerable<T> FindAll<T>(string db_name, string tb_name)
        {
            return sDBDics[db_name].GetCollection<T>(tb_name).FindAll();
        }

        public T FindById<T>(string db_name, string tb_name, int id)
        {
            return sDBDics[db_name].GetCollection<T>(tb_name).FindById(id);
        }

        public IEnumerable<T> FindByExpStrs<T>(string db_name, string tb_name, string expstrs)
        {
            BsonExpression _be = BsonExpression.Create(expstrs);
            return sDBDics[db_name].GetCollection<T>(tb_name).Find(_be);
        }

        public T FindOneByExpStrs<T>(string db_name, string tb_name, string expstrs)
        {
            BsonExpression _be = BsonExpression.Create(expstrs);
            return sDBDics[db_name].GetCollection<T>(tb_name).FindOne(_be);
        }

        public int DeleteByExpStrs<T>(string db_name, string tb_name, string expstrs)
        {
            BsonExpression _be = BsonExpression.Create(expstrs);
            return sDBDics[db_name].GetCollection<T>(tb_name).DeleteMany(_be);
        }
    }
}
