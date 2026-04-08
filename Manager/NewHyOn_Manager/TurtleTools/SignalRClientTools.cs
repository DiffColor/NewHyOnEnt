using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace TurtleTools
{
    internal static class SignalRClientTools
    {
        private const int DefaultPort = 5000;
        private const string DefaultHost = "127.0.0.1";
        internal const string DefaultHubPath = "/Data";
        private const int ReconnectDelayMs = 15000;
        private static readonly object SyncRoot = new object();
        private static HubConnection _connection;
        private static int _reconnecting;
        private static int _stopping;

        public static event EventHandler<SignalRHeartbeatEventArgs> HeartbeatReceived;

        public static bool IsConnected()
        {
            lock (SyncRoot)
            {
                return _connection != null && _connection.State == HubConnectionState.Connected;
            }
        }

        public static bool IsConnecting()
        {
            lock (SyncRoot)
            {
                return _connection != null
                    && (_connection.State == HubConnectionState.Connecting
                        || _connection.State == HubConnectionState.Reconnecting);
            }
        }

        public static void StartSignalRClient()
        {
            Task.Run(StartAsync);
        }

        public static void StopSignalRClient()
        {
            Interlocked.Exchange(ref _stopping, 1);
            HubConnection local;

            lock (SyncRoot)
            {
                local = _connection;
                _connection = null;
            }

            if (local == null)
            {
                return;
            }

            try
            {
                StopAndDisposeConnection(local);
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR client stop failed: {ex}", Logger.GetLogFileName());
            }
        }

        public static bool TrySendCommandToClient(string clientId, SignalRCommandEnvelope envelope)
        {
            if (string.IsNullOrWhiteSpace(clientId) || envelope == null)
            {
                return false;
            }

            HubConnection localConnection;
            lock (SyncRoot)
            {
                localConnection = _connection;
            }

            if (localConnection == null || localConnection.State != HubConnectionState.Connected)
            {
                ScheduleReconnect();
                return false;
            }

            try
            {
                var sendTask = localConnection.InvokeAsync<bool>("SendCommandToClient", clientId, envelope);
                return sendTask.GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR command send failed: {ex}", Logger.GetLogFileName());
                ScheduleReconnect();
                return false;
            }
        }

        private static async Task StartAsync()
        {
            string url = BuildUrl();
            if (string.IsNullOrWhiteSpace(url))
            {
                Logger.WriteLog("SignalR client skipped: host not configured.", Logger.GetLogFileName());
                return;
            }

            HubConnection local;
            lock (SyncRoot)
            {
                if (_connection != null)
                {
                    return;
                }

                local = BuildConnection(url);
                _connection = local;
            }

            try
            {
                await local.StartAsync();
                Logger.WriteLog($"SignalR client connected: {url}", Logger.GetLogFileName());
                await RegisterManagerGroup();
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR client connect failed: {ex}", Logger.GetLogFileName());
                ScheduleReconnect();
            }
        }

        private static HubConnection BuildConnection(string url)
        {
            var hub = new HubConnectionBuilder()
                .AddNewtonsoftJsonProtocol()
                .WithUrl(url, options =>
                {
                    options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
                })
                .Build();

            hub.On<SignalRHeartbeatPayload>("ReceiveHeartbeat", OnReceiveHeartbeat);
            hub.Closed += ex =>
            {
                OnClosed(ex);
                return Task.CompletedTask;
            };

            return hub;
        }

        private static async Task RegisterManagerGroup()
        {
            HubConnection localConnection;
            lock (SyncRoot)
            {
                localConnection = _connection;
            }

            if (localConnection == null || localConnection.State != HubConnectionState.Connected)
            {
                return;
            }

            try
            {
                await localConnection.InvokeAsync("RegisterManager");
            }
            catch (Exception ex)
            {
                Logger.WriteErrorLog($"SignalR manager registration failed: {ex}", Logger.GetLogFileName());
            }
        }

        private static void OnClosed(Exception ex)
        {
            if (Interlocked.CompareExchange(ref _stopping, 0, 0) == 1)
            {
                return;
            }

            if (ex != null)
            {
                Logger.WriteErrorLog($"SignalR client closed with error: {ex}", Logger.GetLogFileName());
            }
            Logger.WriteLog("SignalR client disconnected. Reconnecting...", Logger.GetLogFileName());
            ScheduleReconnect();
        }

        private static void ScheduleReconnect()
        {
            if (Interlocked.Exchange(ref _reconnecting, 1) == 1)
            {
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    while (Interlocked.CompareExchange(ref _stopping, 0, 0) == 0)
                    {
                        await Task.Delay(ReconnectDelayMs);
                        HubConnection local;
                        lock (SyncRoot)
                        {
                            local = _connection;
                        }

                        if (local == null)
                        {
                            return;
                        }

                        try
                        {
                            await local.StartAsync();
                            Logger.WriteLog("SignalR client reconnected.", Logger.GetLogFileName());
                            await RegisterManagerGroup();
                            return;
                        }
                        catch (Exception ex)
                        {
                            Logger.WriteErrorLog($"SignalR client reconnect failed: {ex}", Logger.GetLogFileName());
                        }
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _reconnecting, 0);
                }
            });
        }

        private static void OnReceiveHeartbeat(SignalRHeartbeatPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            HeartbeatReceived?.Invoke(null, new SignalRHeartbeatEventArgs(payload));
        }

        private static string BuildUrl()
        {
            string host = ResolveHost();
            if (string.IsNullOrWhiteSpace(host))
            {
                host = DefaultHost;
            }

            int port = ResolvePort();
            string hubPath = ResolveHubPath();
            if (!hubPath.StartsWith("/"))
            {
                hubPath = "/" + hubPath;
            }

            return $"http://{host}:{port}{hubPath}";
        }

        private static string ResolveHost()
        {
            var settings = LocalSettingsStore.GetConnectionSettings();
            return string.IsNullOrWhiteSpace(settings?.SignalRHost) ? DefaultHost : settings.SignalRHost.Trim();
        }

        private static int ResolvePort()
        {
            var settings = LocalSettingsStore.GetConnectionSettings();
            if (settings?.SignalRPort > 0 && settings.SignalRPort <= 65535)
            {
                return settings.SignalRPort;
            }

            return DefaultPort;
        }

        private static string ResolveHubPath()
        {
            var settings = LocalSettingsStore.GetConnectionSettings();
            if (string.IsNullOrWhiteSpace(settings?.SignalRHubPath))
            {
                return DefaultHubPath;
            }

            return settings.SignalRHubPath.Trim();
        }

        private static void StopAndDisposeConnection(HubConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            connection.StopAsync().GetAwaiter().GetResult();
            connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
