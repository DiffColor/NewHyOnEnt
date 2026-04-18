using System;
using System.Collections.Generic;
using System.Net;
using System.Windows.Threading;
using NewHyOnPlayer.DataManager;
using TurtleTools;

namespace NewHyOnPlayer.PlaybackModes
{
    internal sealed class SeamlessSyncCoordinator : IDisposable
    {
        private readonly MainWindow owner;
        private readonly PortInfoManager portInfoManager;
        private readonly List<IPEndPoint> syncTargets = new List<IPEndPoint>();
        private SeamlessSyncPlaybackContainer playbackContainer;
        private SeamlessSyncUdpService syncService;
        private bool started;

        public SeamlessSyncCoordinator(MainWindow owner, PortInfoManager portInfoManager)
        {
            this.owner = owner;
            this.portInfoManager = portInfoManager;
        }

        public bool IsSyncPlaybackActive => owner?.g_LocalSettingsManager?.Settings?.IsSyncEnabled ?? false;

        public bool IsSyncLeader => IsSyncPlaybackActive && (owner?.g_LocalSettingsManager?.Settings?.IsLeading ?? false);

        public void AttachPlaybackContainer(SeamlessSyncPlaybackContainer container)
        {
            if (ReferenceEquals(playbackContainer, container))
            {
                return;
            }

            if (playbackContainer != null)
            {
                playbackContainer.PlaylistIndexChangeRequested -= PlaybackContainer_PlaylistIndexChangeRequested;
                playbackContainer.SyncIndexRequestNeeded -= PlaybackContainer_SyncIndexRequestNeeded;
            }

            playbackContainer = container;

            if (playbackContainer != null)
            {
                playbackContainer.PlaylistIndexChangeRequested += PlaybackContainer_PlaylistIndexChangeRequested;
                playbackContainer.SyncIndexRequestNeeded += PlaybackContainer_SyncIndexRequestNeeded;
            }
        }

        public void Start()
        {
            if (!IsSyncPlaybackActive || started)
            {
                return;
            }

            int port = ResolveSyncPort();
            syncService = new SeamlessSyncUdpService();
            syncService.MessageReceived += SyncService_MessageReceived;
            syncService.Start(port);
            syncTargets.Clear();
            syncTargets.AddRange(BuildSyncTargets(port));
            started = true;
        }

        public void Stop()
        {
            if (!started)
            {
                return;
            }

            started = false;
            syncTargets.Clear();

            if (syncService == null)
            {
                return;
            }

            syncService.MessageReceived -= SyncService_MessageReceived;
            syncService.Stop();
            syncService.Dispose();
            syncService = null;
        }

        public void Dispose()
        {
            Stop();

            if (playbackContainer != null)
            {
                playbackContainer.PlaylistIndexChangeRequested -= PlaybackContainer_PlaylistIndexChangeRequested;
                playbackContainer.SyncIndexRequestNeeded -= PlaybackContainer_SyncIndexRequestNeeded;
                playbackContainer = null;
            }
        }

        private void PlaybackContainer_PlaylistIndexChangeRequested(int playlistIndex)
        {
            if (!started || !IsSyncLeader || syncService == null || playlistIndex < 0)
            {
                return;
            }

            syncService.SendIndex(syncTargets, playlistIndex);
        }

        private void PlaybackContainer_SyncIndexRequestNeeded()
        {
            if (!started || IsSyncLeader || syncService == null)
            {
                return;
            }

            syncService.SendIndexRequestBroadcast();
        }

        private void SyncService_MessageReceived(SeamlessSyncMessage message)
        {
            if (!started || message == null)
            {
                return;
            }

            Dispatcher dispatcher = owner?.Dispatcher;
            if (dispatcher == null)
            {
                return;
            }

            Action applyAction = () =>
            {
                if (message.Type == SeamlessSyncMessageType.Request)
                {
                    if (!IsSyncLeader || !IsKnownFollower(message.RemoteEndPoint))
                    {
                        return;
                    }

                    if (playbackContainer != null
                        && syncService != null
                        && playbackContainer.TryGetLeaderSyncPosition(out int playlistIndex, out TimeSpan position))
                    {
                        syncService.SendIndexWithPosition(syncTargets, playlistIndex, position);
                        return;
                    }

                    playbackContainer?.TryAdvanceLeaderToNextSyncIndex();
                    return;
                }

                if (IsSyncLeader)
                {
                    return;
                }

                if (message.Type == SeamlessSyncMessageType.IndexWithPosition)
                {
                    playbackContainer?.TryApplySyncPlaylistIndexWithPosition(message.PlaylistIndex, message.Position);
                    return;
                }

                if (message.Type == SeamlessSyncMessageType.Index)
                {
                    playbackContainer?.TryApplySyncPlaylistIndexOnly(message.PlaylistIndex);
                }
            };

            if (dispatcher.CheckAccess())
            {
                applyAction();
                return;
            }

            dispatcher.Invoke(DispatcherPriority.Send, applyAction);
        }

        private bool IsKnownFollower(IPEndPoint remoteEndPoint)
        {
            string remoteIp = remoteEndPoint?.Address?.ToString();
            if (string.IsNullOrWhiteSpace(remoteIp))
            {
                return false;
            }

            var configuredIps = owner?.g_LocalSettingsManager?.Settings?.SyncClientIps;
            if (configuredIps != null)
            {
                foreach (string configuredIp in configuredIps)
                {
                    if (string.Equals(configuredIp?.Trim(), remoteIp, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            if (IPAddress.IsLoopback(remoteEndPoint.Address))
            {
                return true;
            }

            string selfIp = owner?.g_PlayerInfoManager?.g_PlayerInfo?.PIF_IPAddress;
            if (!string.IsNullOrWhiteSpace(selfIp)
                && string.Equals(selfIp.Trim(), remoteIp, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                IPAddress autoIp = NetworkTools.GetAutoIP();
                if (autoIp != null
                    && string.Equals(autoIp.ToString(), remoteIp, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
            }

            return configuredIps == null || configuredIps.Count == 0;
        }

        private List<IPEndPoint> BuildSyncTargets(int port)
        {
            var targets = new List<IPEndPoint>();
            if (!IsSyncPlaybackActive)
            {
                return targets;
            }

            var ipSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var configuredIps = owner?.g_LocalSettingsManager?.Settings?.SyncClientIps;
            if (configuredIps != null)
            {
                foreach (string ip in configuredIps)
                {
                    if (!string.IsNullOrWhiteSpace(ip))
                    {
                        ipSet.Add(ip.Trim());
                    }
                }
            }

            string selfIp = owner?.g_PlayerInfoManager?.g_PlayerInfo?.PIF_IPAddress;
            if (!string.IsNullOrWhiteSpace(selfIp))
            {
                ipSet.Add(selfIp.Trim());
            }

            try
            {
                IPAddress autoIp = NetworkTools.GetAutoIP();
                if (autoIp != null)
                {
                    ipSet.Add(autoIp.ToString());
                }
            }
            catch
            {
            }

            ipSet.Add(IPAddress.Loopback.ToString());

            foreach (string ip in ipSet)
            {
                if (IPAddress.TryParse(ip, out IPAddress parsed))
                {
                    targets.Add(new IPEndPoint(parsed, port));
                }
            }

            return targets;
        }

        private int ResolveSyncPort()
        {
            int port = NetworkTools.SYNC_PORT;
            if (portInfoManager != null && portInfoManager.g_DataClassList.Count > 0)
            {
                port = portInfoManager.g_DataClassList[0].AIF_SYNC;
            }

            if (port <= 0)
            {
                port = NetworkTools.SYNC_PORT;
            }

            return port;
        }
    }
}
