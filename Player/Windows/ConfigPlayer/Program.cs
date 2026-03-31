using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ConfigPlayer
{
    static class Program
    {
        /// <summary>
        /// 해당 응용 프로그램의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool newProc;
            Mutex dup = new Mutex(true, "ConfigPlayer", out newProc);

            if (newProc)
            {
                try
                {
                    FirewallRuleBootstrap.EnsurePlayerRules();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("플레이어 방화벽 설정에 실패했습니다.\r\n" + ex.Message);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Make sure the application runs!
                Application.Run(new Form1());
                dup.ReleaseMutex();
            }
        }
    }

    internal static class FirewallRuleBootstrap
    {
        private const string PlayerAllowedAppRuleName = "newhyon_player_allowed_app";
        private const string SyncRuleName = "newhyon_player_sync_port";

        internal static void EnsurePlayerRules()
        {
            EnsureAllowedProgramRule(PlayerAllowedAppRuleName, FNDTools.GetPlayerExeFilePath());
            EnsureSyncPortRule(ResolveSyncPort());
        }

        internal static void EnsureSyncPortRule(int port)
        {
            if (port <= 0)
            {
                return;
            }

            if (HasRule(SyncRuleName))
            {
                ExecuteNetsh(string.Format("advfirewall firewall set rule name=\"{0}\" new dir=in action=allow protocol=UDP localport={1} enable=yes", SyncRuleName, port));
            }
            else
            {
                ExecuteNetsh(string.Format("advfirewall firewall add rule name=\"{0}\" dir=in action=allow protocol=UDP localport={1} enable=yes", SyncRuleName, port));
            }

            ExecuteNetsh(string.Format("advfirewall firewall set rule name=\"{0}\" new enable=yes profile=private,public", SyncRuleName));
        }

        private static int ResolveSyncPort()
        {
            PortInfoManager portInfoManager = new PortInfoManager();
            if (portInfoManager.g_DataClassList.Count > 0 && portInfoManager.g_DataClassList[0].AIF_SYNC > 0)
            {
                return portInfoManager.g_DataClassList[0].AIF_SYNC;
            }

            return NetworkTools.SYNC_PORT;
        }

        private static void EnsureAllowedProgramRule(string ruleName, string executablePath)
        {
            if (string.IsNullOrWhiteSpace(ruleName) || string.IsNullOrWhiteSpace(executablePath) || File.Exists(executablePath) == false)
            {
                return;
            }

            if (HasRule(ruleName))
            {
                ExecuteNetsh(string.Format("advfirewall firewall set rule name=\"{0}\" new dir=in action=allow program=\"{1}\" enable=yes", ruleName, executablePath));
            }
            else
            {
                ExecuteNetsh(string.Format("advfirewall firewall add rule name=\"{0}\" dir=in action=allow program=\"{1}\" enable=yes", ruleName, executablePath));
            }

            ExecuteNetsh(string.Format("advfirewall firewall set rule name=\"{0}\" new enable=yes profile=private,public", ruleName));
        }

        private static bool HasRule(string ruleName)
        {
            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo("netsh", string.Format("advfirewall firewall show rule name=\"{0}\"", ruleName))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                string combined = string.Concat(output, Environment.NewLine, error);
                if (string.IsNullOrWhiteSpace(combined))
                {
                    return false;
                }

                string normalized = combined.Replace(" ", string.Empty).ToLowerInvariant();
                return normalized.Contains("norulesmatchthespecifiedcriteria") == false
                    && normalized.Contains("지정한규칙을찾을수없습니다") == false
                    && normalized.Contains("지정한조건과일치하는규칙이없습니다") == false;
            }
        }

        private static void ExecuteNetsh(string arguments)
        {
            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo("netsh", arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string message = string.IsNullOrWhiteSpace(error) ? output : error;
                    throw new InvalidOperationException(message);
                }
            }
        }
    }
}
