using LiteDB;
using StartApps.Models;

namespace StartApps.Services;

public class AppDataStore : IDisposable
{
    private const string DatabaseFileName = "startapps.db";
    private const string CollectionName = "apps";

    private readonly LiteDatabase _database;
    private readonly ILiteCollection<AppDefinition> _collection;

    public AppDataStore(AppProfile profile)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, profile.StorageFolderName);
        Directory.CreateDirectory(appFolder);

        var databasePath = Path.Combine(appFolder, DatabaseFileName);
        var mapper = BsonMapper.Global;
        mapper.EmptyStringToNull = false;

        _database = new LiteDatabase(databasePath);
        _collection = _database.GetCollection<AppDefinition>(CollectionName);
        _collection.EnsureIndex(x => x.Id, true);
        _collection.EnsureIndex(x => x.Zone);
    }

    public Task<IList<AppDefinition>> LoadAsync()
    {
        IList<AppDefinition> result = _collection.FindAll().ToList();
        return Task.FromResult(result);
    }

    public Task SaveAsync(IEnumerable<AppDefinition> apps)
    {
        var snapshot = apps.Select(CloneDefinition).ToList();
        return Task.Run(() =>
        {
            _database.BeginTrans();
            try
            {
                _collection.DeleteAll();
                if (snapshot.Count > 0)
                {
                    _collection.InsertBulk(snapshot);
                }
                _database.Commit();
            }
            catch
            {
                _database.Rollback();
                throw;
            }
        });
    }

    private static AppDefinition CloneDefinition(AppDefinition source)
    {
        return new AppDefinition
        {
            Id = source.Id,
            Name = source.Name,
            Type = source.Type,
            Zone = source.Zone,
            IsEnabled = source.IsEnabled,
            ExecutablePath = source.ExecutablePath,
            Arguments = source.Arguments,
            ShowWindow = source.ShowWindow,
            WindowStyle = source.WindowStyle,
            RunAsAdministrator = source.RunAsAdministrator,
            Port = source.Port,
            MsgHubPath = source.MsgHubPath,
            PassivePortRange = source.PassivePortRange,
            WorkingDirectory = source.WorkingDirectory,
            WaitForExitBeforeNext = source.WaitForExitBeforeNext,
            DisplayOrder = source.DisplayOrder,
            LastStartedAt = source.LastStartedAt,
            DelayMinutes = source.DelayMinutes,
            DelaySeconds = source.DelaySeconds,
            RequireNetworkAvailable = source.RequireNetworkAvailable,
            FtpUsername = source.FtpUsername,
            FtpPassword = source.FtpPassword,
            FtpHomeDirectory = source.FtpHomeDirectory,
            FtpAllowRead = source.FtpAllowRead,
            FtpAllowWrite = source.FtpAllowWrite
        };
    }

    public void Dispose()
    {
        _database?.Dispose();
    }
}
