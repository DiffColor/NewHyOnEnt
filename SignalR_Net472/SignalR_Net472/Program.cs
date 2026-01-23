using System;
using System.Threading;
using System.Threading.Tasks;

namespace SignalRNet472
{
    internal static class Program
    {
        private static readonly ManualResetEvent ShutdownEvent = new ManualResetEvent(false);

        private static int Main(string[] args)
        {
            Console.CancelKeyPress += OnCancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            try
            {
                StartupOptions startupOptions = ParseStartupOptions(args);
                RunWithRestartLoop(startupOptions);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"SignalR_Net472 fatal error: {ex}");
                Logger.WriteErrorLog($"SignalR_Net472 fatal error: {ex}", Logger.GetLogFileName());
                return 1;
            }
        }

        private static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            ShutdownEvent.Set();
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            ShutdownEvent.Set();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.WriteErrorLog($"Unhandled exception: {e.ExceptionObject}", Logger.GetLogFileName());
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Logger.WriteErrorLog($"Unobserved task exception: {e.Exception}", Logger.GetLogFileName());
            e.SetObserved();
        }

        private static void RunWithRestartLoop(StartupOptions startupOptions)
        {
            while (!ShutdownEvent.WaitOne(0))
            {
                try
                {
                    SignalRServerHost.Start(startupOptions?.PortOverride, startupOptions?.HubPathOverride);
                    Console.WriteLine("SignalR_Net472 started. Press Ctrl+C to stop.");
                    while (!ShutdownEvent.WaitOne(1000))
                    {
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"SignalR_Net472 error: {ex}");
                    Logger.WriteErrorLog($"SignalR_Net472 error: {ex}", Logger.GetLogFileName());
                }
                finally
                {
                    SignalRServerHost.Stop();
                }

                if (!ShutdownEvent.WaitOne(0))
                {
                    Thread.Sleep(5000);
                }
            }
        }

        private static StartupOptions ParseStartupOptions(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return new StartupOptions(null, null);
            }

            int? portOverride = null;
            string hubPathOverride = null;

            for (int i = 0; i < args.Length; i++)
            {
                string value;
                if (TryReadOption(args, ref i, new[] { "-p", "--port", "/port" }, out value))
                {
                    if (TryParsePort(value, out int port))
                    {
                        portOverride = port;
                    }
                    else
                    {
                        WriteOptionWarning("port", value);
                    }
                }
                else if (TryReadOption(args, ref i, new[] { "-h", "--hub", "/hub", "--hubPath", "/hubPath" }, out value))
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        WriteOptionWarning("hub", value);
                    }
                    else
                    {
                        hubPathOverride = value.Trim();
                    }
                }
            }

            return new StartupOptions(portOverride, hubPathOverride);
        }

        private static bool TryReadOption(string[] args, ref int index, string[] keys, out string value)
        {
            string arg = args[index];
            foreach (string key in keys)
            {
                if (arg.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 < args.Length)
                    {
                        value = args[++index];
                        return true;
                    }

                    value = string.Empty;
                    return true;
                }

                string equalsPrefix = key + "=";
                if (arg.StartsWith(equalsPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    value = arg.Substring(equalsPrefix.Length);
                    return true;
                }

                string colonPrefix = key + ":";
                if (arg.StartsWith(colonPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    value = arg.Substring(colonPrefix.Length);
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static bool TryParsePort(string value, out int port)
        {
            port = 0;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!int.TryParse(value.Trim(), out port))
            {
                return false;
            }

            return port > 0 && port <= 65535;
        }

        private static void WriteOptionWarning(string optionName, string value)
        {
            string message = $"Invalid {optionName} option '{value ?? string.Empty}'. Falling back to config/default.";
            Console.Error.WriteLine(message);
            Logger.WriteLog(message, Logger.GetLogFileName());
        }

        private sealed class StartupOptions
        {
            public StartupOptions(int? portOverride, string hubPathOverride)
            {
                PortOverride = portOverride;
                HubPathOverride = hubPathOverride;
            }

            public int? PortOverride { get; }

            public string HubPathOverride { get; }
        }
    }
}
