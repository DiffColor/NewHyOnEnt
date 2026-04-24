using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32.SafeHandles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StartApps.Models;
using StartApps.Services;
using StartApps.ViewModels;
using StartApps.Views;
using Wpf.Ui.Appearance;

namespace StartApps
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        // The.NET Generic Host provides dependency injection, configuration, logging, and other services.
        // https://docs.microsoft.com/dotnet/core/extensions/generic-host
        // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
        // https://docs.microsoft.com/dotnet/core/extensions/configuration
        // https://docs.microsoft.com/dotnet/core/extensions/logging
        private static readonly IHost _host = Host
            .CreateDefaultBuilder()
            .ConfigureAppConfiguration(c =>
            {
                var basePath = Path.GetDirectoryName(AppContext.BaseDirectory) ?? AppContext.BaseDirectory;
                c.SetBasePath(basePath);
            })
            .ConfigureServices((context, services) =>
            {
                var profile = AppProfile.Resolve(Environment.GetCommandLineArgs(), Environment.ProcessPath);
                services.AddSingleton(profile);
                services.AddSingleton<AppDataStore>();
                services.AddSingleton<AppDependencyService>();
                services.AddSingleton<FirewallRuleService>();
                services.AddSingleton<GlobalHotkeyService>();
                services.AddSingleton<AppManager>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            }).Build();

        /// <summary>
        /// Gets services.
        /// </summary>
        public static IServiceProvider Services
        {
            get { return _host.Services; }
        }

        /// <summary>
        /// Occurs when the application is loading.
        /// </summary>
        private async void OnStartup(object sender, StartupEventArgs e)
        {
            if (!IsRunningAsAdministrator())
            {
                RelaunchAsAdministratorOrExit(e.Args);
                Shutdown();
                return;
            }

            await _host.StartAsync();

            ApplicationThemeManager.Apply(ApplicationTheme.Dark);

            var mainWindow = Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            var globalHotkeyService = Services.GetRequiredService<GlobalHotkeyService>();
            globalHotkeyService.Initialize(mainWindow);
            var viewModel = Services.GetRequiredService<MainWindowViewModel>();
            await viewModel.InitializeAsync();
            mainWindow.Hide();
        }

        /// <summary>
        /// Occurs when the application is closing.
        /// </summary>
        private async void OnExit(object sender, ExitEventArgs e)
        {
            await _host.StopAsync();

            _host.Dispose();
        }

        /// <summary>
        /// Occurs when an exception is thrown by an application but not handled.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
        }

        private static bool IsRunningAsAdministrator()
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return IsTokenElevated(identity.AccessToken);
        }

        private static void RelaunchAsAdministratorOrExit(IEnumerable<string> args)
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new InvalidOperationException("현재 실행 파일 경로를 확인할 수 없습니다.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Environment.CurrentDirectory
            };

            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            try
            {
                Process.Start(startInfo);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                System.Windows.MessageBox.Show(
                    "관리자 권한 승인이 취소되어 StartApps를 종료합니다.",
                    "StartApps",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"관리자 권한으로 다시 실행하지 못했습니다.\n{ex.Message}",
                    "StartApps",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static bool IsTokenElevated(SafeAccessTokenHandle accessToken)
        {
            int tokenInformationLength = Marshal.SizeOf<TOKEN_ELEVATION>();
            IntPtr tokenInformation = Marshal.AllocHGlobal(tokenInformationLength);

            try
            {
                if (!GetTokenInformation(accessToken, TOKEN_INFORMATION_CLASS.TokenElevation, tokenInformation, tokenInformationLength, out _))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "현재 프로세스의 승격 토큰 정보를 확인하지 못했습니다.");
                }

                TOKEN_ELEVATION elevation = Marshal.PtrToStructure<TOKEN_ELEVATION>(tokenInformation);
                return elevation.TokenIsElevated != 0;
            }
            finally
            {
                Marshal.FreeHGlobal(tokenInformation);
            }
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetTokenInformation(
            SafeAccessTokenHandle tokenHandle,
            TOKEN_INFORMATION_CLASS tokenInformationClass,
            IntPtr tokenInformation,
            int tokenInformationLength,
            out int returnLength);

        private enum TOKEN_INFORMATION_CLASS
        {
            TokenElevation = 20
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_ELEVATION
        {
            public int TokenIsElevated;
        }
    }
}
