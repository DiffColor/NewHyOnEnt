using System.IO;
using System.Reflection;

namespace StartApps.Models;

public sealed class AppProfile
{
    public const string DefaultId = "default";
    public const string ManagerId = "manager";
    public const string PlayerId = "player";
    private const string ProfileMetadataKey = "StartAppsProfile";

    public string Id { get; }
    public string DisplayName { get; }
    public string StorageFolderName { get; }

    public bool IsDefault => string.Equals(Id, DefaultId, StringComparison.OrdinalIgnoreCase);

    private AppProfile(string id, string displayName, string storageFolderName)
    {
        Id = id;
        DisplayName = displayName;
        StorageFolderName = storageFolderName;
    }

    public static AppProfile Resolve(string[]? args, string? processPath)
    {
        var profileId = ParseFromArguments(args)
            ?? ParseFromAssemblyMetadata()
            ?? ParseFromExecutableContext(processPath)
            ?? DefaultId;
        return Create(profileId);
    }

    private static AppProfile Create(string profileId)
    {
        return profileId switch
        {
            ManagerId => new AppProfile(ManagerId, "StartApps Manager", "StartApps.Manager"),
            PlayerId => new AppProfile(PlayerId, "StartApps Player", "StartApps.Player"),
            _ => new AppProfile(DefaultId, "StartApps", "StartApps")
        };
    }

    private static string? ParseFromArguments(string[]? args)
    {
        if (args == null || args.Length == 0)
        {
            return null;
        }

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            if (IsProfileSwitch(arg))
            {
                if (i + 1 < args.Length)
                {
                    return Normalize(args[i + 1]);
                }

                continue;
            }

            if (TryParseInlineProfile(arg, out var profile))
            {
                return Normalize(profile);
            }
        }

        return null;
    }

    private static bool IsProfileSwitch(string value) =>
        value.Equals("--profile", StringComparison.OrdinalIgnoreCase)
        || value.Equals("/profile", StringComparison.OrdinalIgnoreCase)
        || value.Equals("-profile", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseInlineProfile(string value, out string profile)
    {
        profile = string.Empty;
        var separators = new[] { "--profile=", "--profile:", "/profile=", "/profile:", "-profile=", "-profile:" };
        foreach (var separator in separators)
        {
            if (!value.StartsWith(separator, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            profile = value[separator.Length..];
            return true;
        }

        return false;
    }

    private static string? ParseFromExecutableContext(string? processPath)
    {
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return null;
        }

        var fileName = Path.GetFileNameWithoutExtension(processPath);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            if (fileName.Contains(ManagerId, StringComparison.OrdinalIgnoreCase))
            {
                return ManagerId;
            }

            if (fileName.Contains(PlayerId, StringComparison.OrdinalIgnoreCase))
            {
                return PlayerId;
            }
        }

        var currentDirectory = Path.GetDirectoryName(processPath);
        while (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            var directoryName = Path.GetFileName(currentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var normalized = Normalize(directoryName);
            if (!string.Equals(normalized, DefaultId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(directoryName, DefaultId, StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            currentDirectory = Path.GetDirectoryName(currentDirectory);
        }

        return null;
    }

    private static string? ParseFromAssemblyMetadata()
    {
        var metadata = typeof(AppProfile).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, ProfileMetadataKey, StringComparison.OrdinalIgnoreCase));

        return metadata == null ? null : Normalize(metadata.Value);
    }

    private static string Normalize(string? rawProfile)
    {
        var normalized = rawProfile?.Trim().ToLowerInvariant();
        return normalized switch
        {
            ManagerId or "mgr" => ManagerId,
            PlayerId or "plr" => PlayerId,
            _ => DefaultId
        };
    }
}
