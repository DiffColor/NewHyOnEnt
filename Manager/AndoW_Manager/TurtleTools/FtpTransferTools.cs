using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;

namespace TurtleTools
{
    internal static class FtpTransferTools
    {
        private const int DefaultPort = 21;
        private const int DefaultTimeoutMs = 5000;
        private const string UpgradeApkRelativePath = "UpgradeAPK/AndoW_Player.apk";

        public static bool TryTestConnection(out string error)
        {
            error = null;

            if (!TryGetValidatedSettings(out var settings, out error))
            {
                return false;
            }

            try
            {
                using (var client = new FtpClient(settings.Host, settings.User, settings.Password, settings.Port))
                {
                    ApplyTimeouts(client.Config);
                    client.Connect();
                    client.Disconnect();
                }

                return true;
            }
            catch (Exception ex)
            {
                error = $"FTP 연결 실패: {ex.Message}";
                return false;
            }
        }

        public static Task<string> UploadUpgradeApkAsync(string localPath)
        {
            return UploadFileAsync(localPath, UpgradeApkRelativePath);
        }

        public static async Task<string> UploadFileAsync(string localPath, string remoteRelativePath)
        {
            if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
            {
                return "업로드할 파일이 존재하지 않습니다.";
            }

            if (!TryGetValidatedSettings(out var settings, out string error))
            {
                return error;
            }

            string remotePath = CombineRemotePath(settings.RootPath, remoteRelativePath);

            try
            {
                using (var client = new AsyncFtpClient(settings.Host, settings.User, settings.Password, settings.Port))
                {
                    ApplyTimeouts(client.Config);
                    client.Config.RetryAttempts = 2;
                    await client.Connect();
                    await client.UploadFile(localPath, remotePath, FtpRemoteExists.Overwrite, true, FtpVerify.Retry, null, CancellationToken.None);
                    await client.Disconnect();
                }

                return null;
            }
            catch (Exception ex)
            {
                return $"FTP 업로드 실패: {ex.Message}";
            }
        }

        public static async Task<string> UploadFileAsync(string localPath, string remoteRelativePath, IProgress<TransferProgress> progress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
            {
                return "업로드할 파일이 존재하지 않습니다.";
            }

            if (!TryGetValidatedSettings(out var settings, out string error))
            {
                return error;
            }

            string remotePath = CombineRemotePath(settings.RootPath, remoteRelativePath);
            long totalBytes = 0;
            try
            {
                totalBytes = new FileInfo(localPath).Length;
            }
            catch
            {
                totalBytes = 0;
            }

            try
            {
                using (var client = new AsyncFtpClient(settings.Host, settings.User, settings.Password, settings.Port))
                {
                    ApplyTimeouts(client.Config);
                    client.Config.RetryAttempts = 2;
                    await client.Connect();

                    IProgress<FtpProgress> ftpProgress = null;
                    if (progress != null)
                    {
                        ftpProgress = new Progress<FtpProgress>(p =>
                        {
                            if (p == null)
                            {
                                return;
                            }

                            progress.Report(new TransferProgress(p.TransferredBytes, totalBytes, p.Progress));
                        });
                    }

                    await client.UploadFile(localPath, remotePath, FtpRemoteExists.Overwrite, true, FtpVerify.Retry, ftpProgress, cancellationToken);
                    await client.Disconnect();
                }

                return null;
            }
            catch (OperationCanceledException)
            {
                return "FTP 업로드가 취소되었습니다.";
            }
            catch (Exception ex)
            {
                return $"FTP 업로드 실패: {ex.Message}";
            }
        }

        public static async Task<string> DownloadFileAsync(string remoteRelativePath, string localPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(remoteRelativePath))
            {
                return "Remote path is empty.";
            }

            if (string.IsNullOrWhiteSpace(localPath))
            {
                return "Local path is empty.";
            }

            if (!TryGetValidatedSettings(out var settings, out string error))
            {
                return error;
            }

            string remotePath = CombineRemotePath(settings.RootPath, remoteRelativePath);

            try
            {
                string localDir = Path.GetDirectoryName(localPath);
                if (string.IsNullOrWhiteSpace(localDir) == false)
                {
                    Directory.CreateDirectory(localDir);
                }

                using (var client = new AsyncFtpClient(settings.Host, settings.User, settings.Password, settings.Port))
                {
                    ApplyTimeouts(client.Config);
                    client.Config.RetryAttempts = 2;
                    await client.Connect();
                    await client.DownloadFile(localPath, remotePath, FtpLocalExists.Overwrite, FtpVerify.Retry, null, cancellationToken);
                    await client.Disconnect();
                }

                return null;
            }
            catch (OperationCanceledException)
            {
                return "FTP download canceled.";
            }
            catch (Exception ex)
            {
                return $"FTP download failed: {ex.Message}";
            }
        }

        public static string BuildContentsRelativePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            string normalized = fileName.Replace("\\", "/").TrimStart('/');
            if (normalized.StartsWith("Contents/", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            return $"Contents/{normalized}";
        }

        public static async Task<HashSet<string>> GetRemoteFileNameSetAsync(string remoteRelativeDir, CancellationToken cancellationToken)
        {
            if (!TryGetValidatedSettings(out var settings, out _))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            string remoteDir = CombineRemotePath(settings.RootPath, remoteRelativeDir);
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (var client = new AsyncFtpClient(settings.Host, settings.User, settings.Password, settings.Port))
                {
                    ApplyTimeouts(client.Config);
                    await client.Connect();

                    try
                    {
                        await client.CreateDirectory(remoteDir, true, cancellationToken);
                    }
                    catch
                    {
                    }

                    var listing = await client.GetListing(remoteDir, cancellationToken);
                    foreach (var item in listing ?? Array.Empty<FtpListItem>())
                    {
                        if (item == null || item.Type != FtpObjectType.File)
                        {
                            continue;
                        }

                        string name = Path.GetFileName(item.FullName);
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            result.Add(name);
                        }
                    }

                    await client.Disconnect();
                }
            }
            catch
            {
                return result;
            }

            return result;
        }

        private static bool TryGetValidatedSettings(out LocalFtpSettings settings, out string error)
        {
            settings = LocalSettingsStore.GetFtpSettings();
            error = null;

            if (settings == null)
            {
                error = "FTP 설정을 불러올 수 없습니다.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.Host))
            {
                error = "FTP 호스트를 설정해 주세요.";
                return false;
            }

            int port = settings.Port > 0 ? settings.Port : DefaultPort;
            if (port <= 0 || port > 65535)
            {
                error = "FTP 포트 설정이 올바르지 않습니다.";
                return false;
            }

            settings.Port = port;

            if (!string.IsNullOrWhiteSpace(settings.User) == false)
            {
                error = "FTP 계정을 설정해 주세요.";
                return false;
            }

            if (settings.PasvMinPort > 0 && settings.PasvMaxPort > 0 && settings.PasvMinPort > settings.PasvMaxPort)
            {
                error = "FTP 패시브 포트 범위가 올바르지 않습니다.";
                return false;
            }

            return true;
        }

        private static void ApplyTimeouts(FtpConfig config)
        {
            if (config == null)
            {
                return;
            }

            config.ConnectTimeout = DefaultTimeoutMs;
            config.ReadTimeout = DefaultTimeoutMs;
            config.DataConnectionConnectTimeout = DefaultTimeoutMs;
            config.DataConnectionReadTimeout = DefaultTimeoutMs;
        }

        private static string CombineRemotePath(string root, string relative)
        {
            string normalizedRoot = NormalizeRemotePath(root);
            if (!normalizedRoot.EndsWith("/"))
            {
                normalizedRoot += "/";
            }

            string normalizedRelative = NormalizeRemotePath(relative).TrimStart('/');
            if (string.IsNullOrWhiteSpace(normalizedRelative))
            {
                return normalizedRoot;
            }

            return normalizedRoot + normalizedRelative;
        }

        private static string NormalizeRemotePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "/";
            }

            string normalized = path.Replace("\\", "/").Trim();
            if (!normalized.StartsWith("/"))
            {
                normalized = "/" + normalized;
            }

            return normalized;
        }
    }

    internal sealed class TransferProgress
    {
        public long TransferredBytes { get; }
        public long TotalBytes { get; }
        public double ProgressPercent { get; }

        public TransferProgress(long transferredBytes, long totalBytes, double progressPercent)
        {
            TransferredBytes = transferredBytes;
            TotalBytes = totalBytes;
            ProgressPercent = progressPercent;
        }
    }
}
