using System;
using System.Threading;

namespace SignalRNet472
{
    internal static class Program
    {
        private static readonly ManualResetEvent ShutdownEvent = new ManualResetEvent(false);

        private static int Main(string[] args)
        {
            Console.CancelKeyPress += OnCancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            try
            {
                SignalRServerHost.Start();
                Console.WriteLine("SignalR_Net472 started. Press Ctrl+C to stop.");
                ShutdownEvent.WaitOne();
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"SignalR_Net472 failed to start: {ex}");
                Logger.WriteErrorLog($"SignalR_Net472 failed to start: {ex}", Logger.GetLogFileName());
                return 1;
            }
            finally
            {
                SignalRServerHost.Stop();
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
    }
}
