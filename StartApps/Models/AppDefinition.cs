using System.Diagnostics;
using System.Windows.Input;
using LiteDB;

namespace StartApps.Models;

public class AppDefinition
{
    [BsonId]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public AppType Type { get; set; }
    public AppExecutionZone Zone { get; set; } = AppExecutionZone.Parallel;
    public bool IsEnabled { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public bool ShowWindow { get; set; } = false;
    public ProcessWindowStyle WindowStyle { get; set; } = ProcessWindowStyle.Hidden;
    public bool? RunAsAdministrator { get; set; }
    public int? Port { get; set; }
    public string MsgHubPath { get; set; } = "/Data";
    public string PassivePortRange { get; set; } = "24000-24240";
    public string? WorkingDirectory { get; set; }
    public DateTimeOffset? LastStartedAt { get; set; }
    public bool WaitForExitBeforeNext { get; set; } = false;
    public int DisplayOrder { get; set; }
    public int DelayMinutes { get; set; }
    public int DelaySeconds { get; set; }
    public bool RequireNetworkAvailable { get; set; }
    public ModifierKeys ToggleShortcutModifiers { get; set; } = ModifierKeys.None;
    public Key ToggleShortcutKey { get; set; } = Key.None;

    // FTP-specific settings
    public string FtpUsername { get; set; } = "asdf";
    public string FtpPassword { get; set; } = "Emfndhk!";
    public string FtpHomeDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Turtle Lab",
        "NewHyOn Manager",
        "Data");
    public bool FtpAllowRead { get; set; } = true;
    public bool FtpAllowWrite { get; set; } = true;
}
