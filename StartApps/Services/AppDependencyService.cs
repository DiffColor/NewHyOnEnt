using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using StartApps.Models;

namespace StartApps.Services;

public class AppDependencyService
{
    public const int DefaultFtpPort = 10021;

    private const string RethinkDbArchiveName = "rethinkdb.zip";
    private const string FtpSrvArchiveName = "ftpsrv.zip";
    private const string FtpSrvFolderName = "ftpsrv";
    private const string FileZillaConfigFileName = "FileZilla Server.xml";
    private const string SignalrNet472ArchiveName = "signalr_net472.zip";
    private const string SignalrNet90ArchiveName = "signalr_net90.zip";
    private const string SignalrNet472FolderName = "signalr";
    private const string SignalrNet90FolderName = "signalr_net90";
    private const string SignalrNet472ExeName = "SignalR_Net472.exe";
    private const string SignalrNet90ExeName = "SignalRServer.exe";
    private const string DefaultWorkspaceFolderName = "Turtle Lab";
    private const string DefaultNewHyOnFolderName = "NewHyOn Manger";
    private const string DefaultDataFolderName = "Data";

    public string StorageRoot { get; }

    public AppDependencyService(AppProfile profile)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        StorageRoot = Path.Combine(appData, profile.StorageFolderName, "Apps");
        Directory.CreateDirectory(StorageRoot);
    }

    public string GetExecutablePath(AppType type) =>
        type switch
        {
            AppType.Rdb => Path.Combine(StorageRoot, "rethinkdb", "rethinkdb.exe"),
            AppType.Ftp => Path.Combine(StorageRoot, FtpSrvFolderName, "FileZilla Server.exe"),
            AppType.Msg => Path.Combine(StorageRoot, SignalrNet472FolderName, SignalrNet472ExeName),
            AppType.Msg472 => Path.Combine(StorageRoot, SignalrNet472FolderName, SignalrNet472ExeName),
            AppType.Msg90 => Path.Combine(StorageRoot, SignalrNet90FolderName, SignalrNet90ExeName),
            _ => string.Empty
        };

    public string GetFtpInterfacePath() =>
        Path.Combine(StorageRoot, FtpSrvFolderName, "FileZilla Server Interface.exe");

    public string GetDefaultFtpHomeDirectory()
    {
        var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var defaultPath = Path.Combine(
            myDocuments,
            DefaultWorkspaceFolderName,
            DefaultNewHyOnFolderName,
            DefaultDataFolderName);

        Directory.CreateDirectory(defaultPath);
        return defaultPath;
    }

    public IReadOnlyList<AppDefinition> CreateDefaultAppDefinitions()
    {
        return
        [
            new AppDefinition
            {
                Name = "RDB",
                Type = AppType.Rdb,
                Zone = AppExecutionZone.Parallel,
                IsEnabled = true,
                Port = 28015,
                ExecutablePath = GetExecutablePath(AppType.Rdb),
                WorkingDirectory = Path.GetDirectoryName(GetExecutablePath(AppType.Rdb))
            },
            new AppDefinition
            {
                Name = "SignalR472",
                Type = AppType.Msg472,
                Zone = AppExecutionZone.Parallel,
                IsEnabled = true,
                Port = 5000,
                MsgHubPath = "/Data",
                ShowWindow = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                RequireNetworkAvailable = true,
                ExecutablePath = GetExecutablePath(AppType.Msg472),
                WorkingDirectory = Path.GetDirectoryName(GetExecutablePath(AppType.Msg472))
            },
            new AppDefinition
            {
                Name = "FTP",
                Type = AppType.Ftp,
                Zone = AppExecutionZone.Parallel,
                IsEnabled = true,
                Port = DefaultFtpPort,
                PassivePortRange = "24000-24240",
                FtpHomeDirectory = GetDefaultFtpHomeDirectory(),
                FtpAllowRead = true,
                FtpAllowWrite = true,
                ExecutablePath = GetExecutablePath(AppType.Ftp),
                WorkingDirectory = Path.GetDirectoryName(GetExecutablePath(AppType.Ftp))
            }
        ];
    }

    public async Task EnsureDependenciesAsync(AppType type, CancellationToken cancellationToken = default)
    {
        if (type == AppType.Rdb)
        {
            var executablePath = GetExecutablePath(AppType.Rdb);
            if (!File.Exists(executablePath))
            {
                await ExtractEmbeddedZipAsync(RethinkDbArchiveName, Path.Combine(StorageRoot, "rethinkdb"), cancellationToken);
            }
        }
        else if (type == AppType.Ftp)
        {
            var executablePath = GetExecutablePath(AppType.Ftp);
            if (!File.Exists(executablePath))
            {
                await ExtractEmbeddedZipAsync(FtpSrvArchiveName, Path.Combine(StorageRoot, FtpSrvFolderName), cancellationToken);
            }
        }
        else if (type == AppType.Msg || type == AppType.Msg472)
        {
            var executablePath = GetExecutablePath(type);
            if (!File.Exists(executablePath))
            {
                await ExtractEmbeddedZipAsync(SignalrNet472ArchiveName, Path.Combine(StorageRoot, SignalrNet472FolderName), cancellationToken);
            }
        }
        else if (type == AppType.Msg90)
        {
            var executablePath = GetExecutablePath(AppType.Msg90);
            if (!File.Exists(executablePath))
            {
                await ExtractEmbeddedZipAsync(SignalrNet90ArchiveName, Path.Combine(StorageRoot, SignalrNet90FolderName), cancellationToken);
            }
        }
    }

    public void ApplyFtpConfiguration(AppDefinition definition)
    {
        if (definition.Type != AppType.Ftp)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(definition.FtpHomeDirectory))
        {
            definition.FtpHomeDirectory = GetDefaultFtpHomeDirectory();
        }

        var configPath = Path.Combine(StorageRoot, FtpSrvFolderName, FileZillaConfigFileName);
        if (!File.Exists(configPath))
        {
            return;
        }

        var document = XDocument.Load(configPath);
        var root = document.Root;
        if (root == null)
        {
            return;
        }

        var settingsElement = root.Element("Settings");
        if (settingsElement != null)
        {
            var ftpPort = definition.Port ?? DefaultFtpPort;
            var (passiveMin, passiveMax) = ParsePassiveRange(definition.PassivePortRange);
            definition.PassivePortRange = $"{passiveMin}-{passiveMax}";

            SetItem(settingsElement, "Serverports", ftpPort.ToString(CultureInfo.InvariantCulture));
            SetItem(settingsElement, "Use custom PASV ports", "1");
            SetItem(settingsElement, "Custom PASV min port", passiveMin.ToString(CultureInfo.InvariantCulture));
            SetItem(settingsElement, "Custom PASV max port", passiveMax.ToString(CultureInfo.InvariantCulture));
        }

        var usersElement = root.Element("Users");
        if (usersElement == null)
        {
            usersElement = new XElement("Users");
            root.Add(usersElement);
        }

        usersElement.RemoveNodes();
        var username = string.IsNullOrWhiteSpace(definition.FtpUsername) ? "asdf" : definition.FtpUsername;
        var userElement = new XElement("User", new XAttribute("Name", username));
        usersElement.Add(userElement);

        AddUserOptions(userElement, definition);
        document.Save(configPath);
    }

    private static void SetItem(XElement settingsElement, string name, string value)
    {
        var itemElement = settingsElement.Elements("Item")
            .FirstOrDefault(x => string.Equals((string?)x.Attribute("name"), name, StringComparison.OrdinalIgnoreCase));

        if (itemElement != null)
        {
            itemElement.Value = value;
        }
    }

    private static void AddUserOptions(XElement userElement, AppDefinition definition)
    {
        userElement.Add(
            new XElement("Option", new XAttribute("Name", "Pass"), HashPassword(definition.FtpPassword ?? "")),
            new XElement("Option", new XAttribute("Name", "Group"), string.Empty),
            new XElement("Option", new XAttribute("Name", "Bypass server userlimit"), "0"),
            new XElement("Option", new XAttribute("Name", "User Limit"), "0"),
            new XElement("Option", new XAttribute("Name", "IP Limit"), "0"),
            new XElement("Option", new XAttribute("Name", "Enabled"), "1"),
            new XElement("Option", new XAttribute("Name", "Comments"), string.Empty),
            new XElement("Option", new XAttribute("Name", "ForceSsl"), "0")
        );

        var ipFilter = new XElement("IpFilter",
            new XElement("Disallowed"),
            new XElement("Allowed"));
        userElement.Add(ipFilter);

        var permissions = new XElement("Permissions");
        var home = definition.FtpHomeDirectory;
        if (!string.IsNullOrWhiteSpace(home) && !Directory.Exists(home))
        {
            Directory.CreateDirectory(home);
        }
        var permission = new XElement("Permission", new XAttribute("Dir", home));
        permissions.Add(permission);
        userElement.Add(permissions);

        void SetPermission(string name, bool allow) =>
            permission.Add(new XElement("Option", new XAttribute("Name", name), allow ? "1" : "0"));

        permission.Add(new XElement("Option", new XAttribute("Name", "FileRead"), definition.FtpAllowRead ? "1" : "0"));
        SetPermission("FileWrite", definition.FtpAllowWrite);
        SetPermission("FileDelete", definition.FtpAllowWrite);
        SetPermission("FileAppend", definition.FtpAllowWrite);
        permission.Add(new XElement("Option", new XAttribute("Name", "DirList"), definition.FtpAllowRead ? "1" : "0"));
        SetPermission("DirCreate", definition.FtpAllowWrite);
        SetPermission("DirDelete", definition.FtpAllowWrite);
        permission.Add(new XElement("Option", new XAttribute("Name", "DirSubdirs"), definition.FtpAllowRead ? "1" : "0"));
        permission.Add(new XElement("Option", new XAttribute("Name", "IsHome"), "1"));
        permission.Add(new XElement("Option", new XAttribute("Name", "AutoCreate"), definition.FtpAllowWrite ? "1" : "0"));

        var speedLimits = new XElement("SpeedLimits",
            new XAttribute("DlType", "0"),
            new XAttribute("DlLimit", "10"),
            new XAttribute("ServerDlLimitBypass", "0"),
            new XAttribute("UlType", "0"),
            new XAttribute("UlLimit", "10"),
            new XAttribute("ServerUlLimitBypass", "0"),
            new XElement("Download"),
            new XElement("Upload"));
        userElement.Add(speedLimits);
    }

    private static (int Min, int Max) ParsePassiveRange(string? range)
    {
        const int defaultMin = 24000;
        const int defaultMax = 24240;

        if (string.IsNullOrWhiteSpace(range))
        {
            return (defaultMin, defaultMax);
        }

        var parts = range.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var min = parts.Length > 0 && int.TryParse(parts[0], out var parsedMin) ? parsedMin : defaultMin;
        var max = parts.Length > 1 && int.TryParse(parts[1], out var parsedMax) ? parsedMax : min;

        if (max < min)
        {
            (min, max) = (max, min);
        }

        return (min, max);
    }

    private static string HashPassword(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = MD5.HashData(bytes);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }

    private Task ExtractEmbeddedZipAsync(string resourceFileName, string destinationFolder, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CleanDirectory(destinationFolder);

        using var resourceStream = OpenEmbeddedResourceStream(resourceFileName);
        using var archive = new ZipArchive(resourceStream, ZipArchiveMode.Read, leaveOpen: false);
        archive.ExtractToDirectory(destinationFolder, overwriteFiles: true);

        return Task.CompletedTask;
    }

    private static Stream OpenEmbeddedResourceStream(string resourceFileName)
    {
        var assembly = typeof(AppDependencyService).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(resourceFileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            throw new InvalidOperationException($"{resourceFileName} 리소스를 찾을 수 없습니다.");
        }

        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"{resourceFileName} 리소스를 열 수 없습니다.");
        }

        return stream;
    }

    private static void CleanDirectory(string destinationFolder)
    {
        if (!Directory.Exists(destinationFolder))
        {
            Directory.CreateDirectory(destinationFolder);
            return;
        }

        foreach (var file in Directory.EnumerateFiles(destinationFolder))
        {
            File.Delete(file);
        }

        foreach (var dir in Directory.EnumerateDirectories(destinationFolder))
        {
            Directory.Delete(dir, true);
        }
    }
}
