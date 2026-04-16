using System;
using System.Diagnostics;
using System.IO;

namespace NewHyOn.Player.Settings.Services;

public static class FndTools
{
    public static string GetPlayerProcName() => "NewHyOn Player";

    public static string GetAgentProcName() => "NewHyOn Agent";

    public static string GetPcsProcName() => "PCScheduler";

    public static string GetEmergScrollProcName() => "EmergencyScrollText";

    public static string GetPptViewerProcName() => "PPTVIEW";

    public static string GetPlayerExeFilePath()
    {
        string? runningPath = TryGetRunningProcessPath(GetPlayerProcName());
        if (!string.IsNullOrWhiteSpace(runningPath))
        {
            return runningPath;
        }

        string fileName = $"{GetPlayerProcName()}.exe";
        string executableDirectory = GetCurrentExecutableDirectory();
        string directPath = Path.GetFullPath(Path.Combine(executableDirectory, fileName));
        if (File.Exists(directPath))
        {
            return directPath;
        }

        DirectoryInfo? parent = Directory.GetParent(executableDirectory);
        if (parent != null)
        {
            string parentPath = Path.GetFullPath(Path.Combine(parent.FullName, fileName));
            if (File.Exists(parentPath))
            {
                return parentPath;
            }
        }

        return directPath;
    }

    public static string GetCurrentExecutableDirectory()
    {
        string executablePath = GetCurrentExecutablePath();
        return Path.GetDirectoryName(executablePath)
            ?? throw new InvalidOperationException("실행 파일 디렉터리를 확인할 수 없습니다.");
    }

    public static string GetCurrentExecutablePath()
    {
#if NET6_0_OR_GREATER
        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            return Path.GetFullPath(processPath);
        }
#endif

        try
        {
            using Process currentProcess = Process.GetCurrentProcess();
            string? mainModulePath = currentProcess.MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(mainModulePath))
            {
                return Path.GetFullPath(mainModulePath);
            }
        }
        catch
        {
        }

        string[] commandLineArgs = Environment.GetCommandLineArgs();
        if (commandLineArgs.Length > 0 && string.IsNullOrWhiteSpace(commandLineArgs[0]) == false)
        {
            return Path.GetFullPath(commandLineArgs[0]);
        }

        throw new InvalidOperationException("실행 중인 파일 경로를 확인할 수 없습니다.");
    }

    private static string? TryGetRunningProcessPath(string processName)
    {
        try
        {
            foreach (Process process in Process.GetProcessesByName(processName))
            {
                try
                {
                    string? fileName = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(fileName) && File.Exists(fileName))
                    {
                        return Path.GetFullPath(fileName);
                    }
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        return null;
    }
}
