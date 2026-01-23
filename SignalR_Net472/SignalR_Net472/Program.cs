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
                RunWithRestartLoop();
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

        private static void RunWithRestartLoop()
        {
            while (!ShutdownEvent.WaitOne(0))
            {
                try
                {
                    SignalRServerHost.Start();
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
    }
}
