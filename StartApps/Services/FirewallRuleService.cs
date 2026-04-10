using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using StartApps.Models;

namespace StartApps.Services;

public sealed class FirewallRuleService
{
    private const int VistaMajorVersion = 6;
    private const int DefaultRethinkPort = 28015;
    private const int DefaultSignalRPort = 5000;

    private readonly AppProfile _profile;

    public FirewallRuleService(AppProfile profile)
    {
        _profile = profile;
    }

    public async Task EnsureRulesAsync(AppDefinition definition, string executablePath, CancellationToken cancellationToken = default)
    {
        if (!ShouldManage(definition.Type))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("방화벽 규칙에 사용할 실행 파일 경로가 비어 있습니다.");
        }

        string normalizedPath = Path.GetFullPath(executablePath);
        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException("방화벽 규칙에 사용할 실행 파일을 찾을 수 없습니다.", normalizedPath);
        }

        foreach (FirewallRuleSpec rule in BuildRules(definition, normalizedPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            RuleSyncMode syncMode = await GetRuleSyncModeAsync(rule, cancellationToken);
            switch (syncMode)
            {
                case RuleSyncMode.None:
                    continue;
                case RuleSyncMode.Add:
                    await ExecuteNetshAsync(rule.AddArguments, cancellationToken, throwOnError: true);
                    break;
                case RuleSyncMode.Update:
                    await ExecuteNetshAsync(rule.SetArguments, cancellationToken, throwOnError: true);
                    break;
            }
        }
    }

    private IEnumerable<FirewallRuleSpec> BuildRules(AppDefinition definition, string executablePath)
    {
        yield return FirewallRuleSpec.CreateProgramRule(
            BuildProgramRuleName(definition),
            executablePath);

        int mainPort = ResolveMainPort(definition);
        if (mainPort > 0)
        {
            yield return FirewallRuleSpec.CreatePortRule(
                BuildMainPortRuleName(definition),
                mainPort.ToString(CultureInfo.InvariantCulture));
        }

        if (definition.Type == AppType.Ftp)
        {
            var (minPort, maxPort) = ParsePassiveRange(definition.PassivePortRange);
            yield return FirewallRuleSpec.CreatePortRule(
                BuildPassivePortRuleName(definition),
                $"{minPort}-{maxPort}");
        }
    }

    private static int ResolveMainPort(AppDefinition definition) =>
        definition.Type switch
        {
            AppType.Rdb => definition.Port.GetValueOrDefault(DefaultRethinkPort),
            AppType.Msg or AppType.Msg472 or AppType.Msg10 => definition.Port.GetValueOrDefault(DefaultSignalRPort),
            AppType.Ftp => definition.Port.GetValueOrDefault(AppDependencyService.DefaultFtpPort),
            _ => 0
        };

    private static (int MinPort, int MaxPort) ParsePassiveRange(string? range)
    {
        const int defaultMin = 24000;
        const int defaultMax = 24240;

        if (string.IsNullOrWhiteSpace(range))
        {
            return (defaultMin, defaultMax);
        }

        string[] parts = range.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        int minPort = parts.Length > 0 && int.TryParse(parts[0], out int parsedMin) ? parsedMin : defaultMin;
        int maxPort = parts.Length > 1 && int.TryParse(parts[1], out int parsedMax) ? parsedMax : minPort;

        if (minPort <= 0 || minPort > 65535)
        {
            minPort = defaultMin;
        }

        if (maxPort <= 0 || maxPort > 65535)
        {
            maxPort = defaultMax;
        }

        if (maxPort < minPort)
        {
            (minPort, maxPort) = (maxPort, minPort);
        }

        return (minPort, maxPort);
    }

    private string BuildProgramRuleName(AppDefinition definition) =>
        $"startapps_{BuildServiceKey(definition)}_app";

    private string BuildMainPortRuleName(AppDefinition definition) =>
        $"startapps_{BuildServiceKey(definition)}_port";

    private string BuildPassivePortRuleName(AppDefinition definition) =>
        $"startapps_{BuildServiceKey(definition)}_passive_port";

    private static string BuildServiceKey(AppDefinition definition) =>
        definition.Type switch
        {
            AppType.Rdb => "rethinkdb",
            AppType.Ftp => "ftp",
            AppType.Msg => "signalr",
            AppType.Msg472 => "signalr472",
            AppType.Msg10 => "signalr10",
            _ => "app"
        };

    private async Task<RuleSyncMode> GetRuleSyncModeAsync(FirewallRuleSpec rule, CancellationToken cancellationToken)
    {
        if (Environment.OSVersion.Version.Major < VistaMajorVersion)
        {
            return RuleSyncMode.Add;
        }

        var (exitCode, output, error) = await ExecuteNetshAsync(
            $"advfirewall firewall show rule name=\"{EscapeArgument(rule.Name)}\"",
            cancellationToken,
            throwOnError: false);

        if (exitCode != 0)
        {
            return RuleSyncMode.Add;
        }

        string combined = string.Join(
            Environment.NewLine,
            new[] { output, error }.Where(value => !string.IsNullOrWhiteSpace(value)));

        if (string.IsNullOrWhiteSpace(combined))
        {
            return RuleSyncMode.Add;
        }

        string normalizedOutput = Normalize(combined);
        if (normalizedOutput.Contains("norulesmatchthespecifiedcriteria", StringComparison.OrdinalIgnoreCase)
            || normalizedOutput.Contains("지정한규칙을찾을수없습니다", StringComparison.OrdinalIgnoreCase)
            || normalizedOutput.Contains("지정한조건과일치하는규칙이없습니다", StringComparison.OrdinalIgnoreCase))
        {
            return RuleSyncMode.Add;
        }

        if (!IsRuleEnabled(normalizedOutput))
        {
            return RuleSyncMode.Update;
        }

        if (!IsRuleProfilesCompatible(normalizedOutput))
        {
            return RuleSyncMode.Update;
        }

        if (rule.ProgramPath != null && !normalizedOutput.Contains(Normalize(rule.ProgramPath), StringComparison.OrdinalIgnoreCase))
        {
            return RuleSyncMode.Update;
        }

        if (rule.LocalPort != null
            && !normalizedOutput.Contains($"localport:{Normalize(rule.LocalPort)}", StringComparison.OrdinalIgnoreCase)
            && !normalizedOutput.Contains($"로컬포트:{Normalize(rule.LocalPort)}", StringComparison.OrdinalIgnoreCase))
        {
            return RuleSyncMode.Update;
        }

        return RuleSyncMode.None;
    }

    private static bool IsRuleEnabled(string normalizedOutput) =>
        normalizedOutput.Contains("enabled:yes", StringComparison.OrdinalIgnoreCase)
        || normalizedOutput.Contains("사용:예", StringComparison.OrdinalIgnoreCase);

    private static bool IsRuleProfilesCompatible(string normalizedOutput)
    {
        if (normalizedOutput.Contains("profiles:any", StringComparison.OrdinalIgnoreCase)
            || normalizedOutput.Contains("프로필:모두", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        bool hasPrivate = normalizedOutput.Contains("private", StringComparison.OrdinalIgnoreCase)
            || normalizedOutput.Contains("개인", StringComparison.OrdinalIgnoreCase);
        bool hasPublic = normalizedOutput.Contains("public", StringComparison.OrdinalIgnoreCase)
            || normalizedOutput.Contains("공용", StringComparison.OrdinalIgnoreCase);

        return hasPrivate && hasPublic;
    }

    private static async Task DeleteRuleAsync(string ruleName, CancellationToken cancellationToken)
    {
        await ExecuteNetshAsync(
            $"advfirewall firewall delete rule name=\"{EscapeArgument(ruleName)}\"",
            cancellationToken,
            throwOnError: false);
    }

    private static async Task<(int ExitCode, string Output, string Error)> ExecuteNetshAsync(
        string arguments,
        CancellationToken cancellationToken,
        bool throwOnError)
    {
        using Process process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("방화벽 명령을 시작하지 못했습니다.");
        }

        await process.WaitForExitAsync(cancellationToken);

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken);

        if (throwOnError && process.ExitCode != 0)
        {
            string message = string.IsNullOrWhiteSpace(error) ? output : error;
            throw new InvalidOperationException($"방화벽 규칙 적용에 실패했습니다. {message}".Trim());
        }

        return (process.ExitCode, output, error);
    }

    private static string EscapeArgument(string value) => value.Replace("\"", "\"\"", StringComparison.Ordinal);

    private static string Normalize(string value)
    {
        StringBuilder builder = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            if (!char.IsWhiteSpace(ch) && ch != '"')
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString().Replace('\\', '/');
    }

    private static bool ShouldManage(AppType type) =>
        type == AppType.Rdb
        || type == AppType.Ftp
        || type == AppType.Msg
        || type == AppType.Msg472
        || type == AppType.Msg10;

    private enum RuleSyncMode
    {
        None,
        Add,
        Update
    }

    private sealed record FirewallRuleSpec(string Name, string AddArguments, string SetArguments, string? ProgramPath, string? LocalPort)
    {
        public static FirewallRuleSpec CreateProgramRule(string name, string programPath)
        {
            string addArgs = $"advfirewall firewall add rule name=\"{EscapeArgument(name)}\" dir=in action=allow program=\"{EscapeArgument(programPath)}\" enable=yes profile=private,public";
            string setArgs = $"advfirewall firewall set rule name=\"{EscapeArgument(name)}\" new dir=in action=allow program=\"{EscapeArgument(programPath)}\" enable=yes profile=private,public";
            return new FirewallRuleSpec(name, addArgs, setArgs, programPath, null);
        }

        public static FirewallRuleSpec CreatePortRule(string name, string localPort)
        {
            string addArgs = $"advfirewall firewall add rule name=\"{EscapeArgument(name)}\" dir=in action=allow protocol=TCP localport={EscapeArgument(localPort)} enable=yes profile=private,public";
            string setArgs = $"advfirewall firewall set rule name=\"{EscapeArgument(name)}\" new dir=in action=allow protocol=TCP localport={EscapeArgument(localPort)} enable=yes profile=private,public";
            return new FirewallRuleSpec(name, addArgs, setArgs, null, localPort);
        }
    }
}
