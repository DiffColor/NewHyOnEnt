using System;
using System.Reflection;
using System.Threading;
using TurtleTools;

namespace HyOnPlayer
{
    internal sealed class HeartbeatReporter : IDisposable
    {
        private readonly MainWindow owner;
        private readonly SignalRClientService signalRClientService;
        private readonly MultimediaTimer.Timer timer;
        private readonly int intervalMs;
        private int isExecuting;
        private bool disposed;

        public HeartbeatReporter(MainWindow owner, SignalRClientService signalRClientService, int intervalMs = 5000)
        {
            this.owner = owner;
            this.signalRClientService = signalRClientService;
            this.intervalMs = Math.Max(1000, intervalMs);

            timer = new MultimediaTimer.Timer
            {
                Mode = MultimediaTimer.TimerMode.Periodic,
                Period = this.intervalMs,
                Resolution = 1
            };
            timer.Tick += OnElapsed;
        }

        public void Start()
        {
            if (disposed) return;
            timer.Start();
        }

        public void Stop()
        {
            if (disposed) return;
            timer.Stop();
        }

        public void SendHeartbeatNow()
        {
            ThreadPool.QueueUserWorkItem(_ => SendHeartbeatInternal());
        }

        public void SendStopped()
        {
            try
            {
                var payload = BuildPayload("stopped");
                if (payload != null)
                {
                    payload.Process = 0;
                    payload.CurrentPage = string.Empty;
                    payload.HdmiState = false;
                    SendHeartbeatBySignalR(payload);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                timer.Stop();
                timer.Dispose();
            }
            catch
            {
            }
        }

        private void OnElapsed(object sender, EventArgs e)
        {
            if (Interlocked.Exchange(ref isExecuting, 1) == 1)
            {
                return;
            }

            Stop();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    SendHeartbeatInternal();
                }
                finally
                {
                    Interlocked.Exchange(ref isExecuting, 0);
                    Start();
                }
            });
        }

        private void SendHeartbeatInternal()
        {
            try
            {
                var payload = BuildPayload();
                if (payload != null)
                {
                    SendHeartbeatBySignalR(payload);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        private void SendHeartbeatBySignalR(HeartbeatPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            signalRClientService?.SendHeartbeat(payload);
        }

        private HeartbeatPayload BuildPayload(string overrideStatus = null)
        {
            var player = owner?.g_PlayerInfoManager?.g_PlayerInfo;
            if (player == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(player.PIF_GUID))
            {
                return null;
            }

            string status = overrideStatus;
            int process = 0;

            if (string.IsNullOrWhiteSpace(status))
            {
                try
                {
                    var commandService = owner?.CommandService;
                    if (commandService != null && commandService.TryGetHeartbeatUpdate(out string updateStatus, out int updateProgress))
                    {
                        status = updateStatus;
                        process = updateProgress;
                    }
                }
                catch
                {
                }
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                if (owner != null && owner.IsUpdating)
                {
                    status = "updating";
                }
                else if (owner != null && owner.IsPlaying)
                {
                    status = "playing";
                }
                else
                {
                    status = "idle";
                }
            }

            bool hdmiState = true;
            if (string.Equals(status, "stopped", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "idle", StringComparison.OrdinalIgnoreCase))
            {
                hdmiState = false;
            }

            return new HeartbeatPayload
            {
                ClientId = player.PIF_GUID,
                Status = status,
                Process = NormalizeHeartbeatProcess(process),
                Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0",
                CurrentPage = owner?.g_CurrentPageName ?? string.Empty,
                HdmiState = hdmiState
            };
        }

        private static int NormalizeHeartbeatProcess(int progress)
        {
            int value = progress;
            if (value < 0)
            {
                value = 0;
            }
            else if (value > 100)
            {
                value = 100;
            }

            return value;
        }
    }

    internal sealed class HeartbeatPayload
    {
        public string ClientId { get; set; }
        public string Status { get; set; }
        public int Process { get; set; }
        public string Version { get; set; }
        public string CurrentPage { get; set; }
        public bool HdmiState { get; set; }
    }
}
