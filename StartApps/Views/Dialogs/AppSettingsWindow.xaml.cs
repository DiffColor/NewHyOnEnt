using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using StartApps.Models;
using StartApps.Services;
using Wpf.Ui.Controls;
using PasswordBox = System.Windows.Controls.PasswordBox;
using Forms = System.Windows.Forms;

namespace StartApps.Views.Dialogs;

public partial class AppSettingsWindow : FluentWindow
{
    private readonly AppDependencyService _dependencyService;
    private readonly AppDefinition _workingCopy;

    public AppSettingsWindow(AppDefinition definition, AppDependencyService dependencyService)
    {
        InitializeComponent();
        _dependencyService = dependencyService;
        _workingCopy = Clone(definition);

        if (string.IsNullOrWhiteSpace(_workingCopy.ExecutablePath)
            && (_workingCopy.Type == AppType.Rdb
                || _workingCopy.Type == AppType.Ftp
                || _workingCopy.Type == AppType.Msg
                || _workingCopy.Type == AppType.Msg472
                || _workingCopy.Type == AppType.Msg90))
        {
            _workingCopy.ExecutablePath = dependencyService.GetExecutablePath(_workingCopy.Type);
        }

        if (_workingCopy.Type == AppType.Ftp && string.IsNullOrWhiteSpace(_workingCopy.FtpHomeDirectory))
        {
            _workingCopy.FtpHomeDirectory = _dependencyService.GetDefaultFtpHomeDirectory();
        }

        DataContext = _workingCopy;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        WindowStyleCombo.ItemsSource = Enum.GetValues<ProcessWindowStyle>();
        FtpPasswordBox.Password = _workingCopy.FtpPassword ?? string.Empty;
        UpdateFtpHomeDirectoryDisplay();
    }

    public AppDefinition? ResultDefinition { get; private set; }

    private static AppDefinition Clone(AppDefinition definition)
    {
        return new AppDefinition
        {
            Id = definition.Id,
            Name = definition.Name,
            Type = definition.Type,
            Zone = definition.Zone,
            IsEnabled = definition.IsEnabled,
            ExecutablePath = definition.ExecutablePath,
            Arguments = definition.Arguments,
            ShowWindow = definition.ShowWindow,
            WindowStyle = definition.WindowStyle,
            Port = definition.Port,
            MsgHubPath = definition.MsgHubPath,
            PassivePortRange = definition.PassivePortRange,
            WorkingDirectory = definition.WorkingDirectory,
            WaitForExitBeforeNext = definition.WaitForExitBeforeNext,
            DisplayOrder = definition.DisplayOrder,
            DelayMinutes = definition.DelayMinutes,
            DelaySeconds = definition.DelaySeconds,
            RequireNetworkAvailable = definition.RequireNetworkAvailable,
            FtpUsername = definition.FtpUsername,
            FtpPassword = definition.FtpPassword,
            FtpHomeDirectory = definition.FtpHomeDirectory,
            FtpAllowRead = definition.FtpAllowRead,
            FtpAllowWrite = definition.FtpAllowWrite
        };
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_workingCopy.Name))
        {
            System.Windows.MessageBox.Show(this, "앱 이름을 입력하세요.");
            return;
        }

        if (_workingCopy.Type == AppType.App && string.IsNullOrWhiteSpace(_workingCopy.ExecutablePath))
        {
            System.Windows.MessageBox.Show(this, "실행 파일을 지정하세요.");
            return;
        }

        if (_workingCopy.Type == AppType.Ftp)
        {
            if (string.IsNullOrWhiteSpace(_workingCopy.FtpUsername))
            {
                System.Windows.MessageBox.Show(this, "FTP 사용자 이름을 입력하세요.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_workingCopy.FtpPassword))
            {
                System.Windows.MessageBox.Show(this, "FTP 비밀번호를 입력하세요.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_workingCopy.FtpHomeDirectory))
            {
                System.Windows.MessageBox.Show(this, "FTP 홈 디렉터리를 선택하세요.");
                return;
            }
        }
        else if (_workingCopy.Type == AppType.Msg
                 || _workingCopy.Type == AppType.Msg472
                 || _workingCopy.Type == AppType.Msg90)
        {
            if (_workingCopy.Port is null || _workingCopy.Port <= 0)
            {
                System.Windows.MessageBox.Show(this, "MSG 포트를 입력하세요.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_workingCopy.MsgHubPath))
            {
                System.Windows.MessageBox.Show(this, "허브 경로를 입력하세요.");
                return;
            }
        }

        NormalizeDelayInputs();

        ResultDefinition = _workingCopy;
        DialogResult = true;
    }

    private void NormalizeDelayInputs()
    {
        if (_workingCopy.DelayMinutes < 0)
        {
            _workingCopy.DelayMinutes = 0;
        }

        if (_workingCopy.DelaySeconds < 0)
        {
            _workingCopy.DelaySeconds = 0;
        }

        if (_workingCopy.DelaySeconds >= 60)
        {
            _workingCopy.DelayMinutes += _workingCopy.DelaySeconds / 60;
            _workingCopy.DelaySeconds = _workingCopy.DelaySeconds % 60;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnBrowseExecutable(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "실행 파일 (*.exe;*.bat)|*.exe;*.bat|모든 파일 (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _workingCopy.ExecutablePath = dialog.FileName;
            ExecutablePathBox.Text = _workingCopy.ExecutablePath ?? string.Empty;
        }
    }

    private void OnBrowseWorkingDirectory(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            _workingCopy.WorkingDirectory = dialog.SelectedPath;
            WorkingDirectoryBox.Text = _workingCopy.WorkingDirectory ?? string.Empty;
        }
    }

    private void OnBrowseFtpHome(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog();
        dialog.SelectedPath = _workingCopy.FtpHomeDirectory;
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            _workingCopy.FtpHomeDirectory = dialog.SelectedPath;
            UpdateFtpHomeDirectoryDisplay();
        }
    }

    private void UpdateFtpHomeDirectoryDisplay()
    {
        var fullPath = _workingCopy.FtpHomeDirectory ?? string.Empty;
        FtpHomeDirectoryBox.Text = BuildFtpHomeDirectoryPreview(fullPath);
        FtpHomeDirectoryBox.ToolTip = fullPath;
    }

    private static string BuildFtpHomeDirectoryPreview(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrEmpty(normalized))
        {
            return string.Empty;
        }

        var root = Path.GetPathRoot(normalized);
        if (!string.IsNullOrEmpty(root) &&
            string.Equals(
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                normalized,
                StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        var segments = normalized.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length >= 2)
        {
            return $@"...\{segments[^2]}\{segments[^1]}";
        }

        return $@"...\{segments[0]}";
    }

    private void OnFtpPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            _workingCopy.FtpPassword = passwordBox.Password;
        }
    }

    private void OnLaunchFtpInterface(object sender, RoutedEventArgs e)
    {
        try
        {
            string interfacePath;
            string? workingDirectory = _workingCopy.WorkingDirectory;

            if (_workingCopy.Type == AppType.Ftp)
            {
                _dependencyService.ApplyFtpConfiguration(_workingCopy);
                workingDirectory = Path.GetDirectoryName(_dependencyService.GetExecutablePath(AppType.Ftp));
                interfacePath = _dependencyService.GetFtpInterfacePath();
            }
            else
            {
                interfacePath = _dependencyService.GetFtpInterfacePath();
            }

            if (!File.Exists(interfacePath))
            {
                System.Windows.MessageBox.Show(this, "FileZilla 인터페이스 파일을 찾을 수 없습니다. FTP 앱을 추가하여 리소스를 다운로드하세요.");
                return;
            }

            Process.Start(new ProcessStartInfo(interfacePath)
            {
                UseShellExecute = true,
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(interfacePath)
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"인터페이스 실행 중 오류가 발생했습니다.\n{ex.Message}");
        }
    }
}
