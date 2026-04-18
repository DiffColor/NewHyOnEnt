using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using TurtleTools;

namespace NewHyOnPlayer.PlaybackModes
{
    internal enum SeamlessSyncMessageType
    {
        Index,
        IndexWithPosition,
        Request
    }

    internal sealed class SeamlessSyncMessage
    {
        public SeamlessSyncMessage(SeamlessSyncMessageType type, int playlistIndex, TimeSpan position, IPEndPoint remoteEndPoint, string raw)
        {
            Type = type;
            PlaylistIndex = playlistIndex;
            Position = position;
            RemoteEndPoint = remoteEndPoint;
            Raw = raw;
        }

        public SeamlessSyncMessageType Type { get; }
        public int PlaylistIndex { get; }
        public TimeSpan Position { get; }
        public IPEndPoint RemoteEndPoint { get; }
        public string Raw { get; }
    }

    internal sealed class SeamlessSyncUdpService : IDisposable
    {
        private const byte BinaryMessagePrefix = (byte)'N';
        private const byte BinaryIndexMessageType = (byte)'I';
        private const byte BinaryIndexWithPositionMessageType = (byte)'P';
        private const byte BinaryRequestMessageType = (byte)'R';
        private const int BinaryIndexMessageLength = 6;
        private const int BinaryIndexWithPositionMessageLength = 14;
        private const int BinaryRequestMessageLength = 2;
        private static readonly Encoding MessageEncoding = new UTF8Encoding(false);
        private static readonly byte[] EmptyPayload = Array.Empty<byte>();

        public event Action<SeamlessSyncMessage> MessageReceived;

        private UdpClient receiver;
        private UdpClient sender;
        private bool disposed;
        public int sPort { get; private set; }

        public void Start(int port)
        {
            Stop();

            sPort = port;
            receiver = new UdpClient(AddressFamily.InterNetwork);
            receiver.ExclusiveAddressUse = false;
            receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            receiver.EnableBroadcast = true;
            receiver.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            sender = new UdpClient(AddressFamily.InterNetwork);
            sender.EnableBroadcast = true;
            disposed = false;
            BeginReceive();
        }

        private void BeginReceive()
        {
            try
            {
                if (receiver == null || disposed)
                {
                    return;
                }

                receiver.BeginReceive(ReceiveCallback, null);
            }
            catch (Exception ex)
            {
                Logger.WriteLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, 0);

            try
            {
                if (receiver == null || disposed)
                {
                    return;
                }

                byte[] payload = receiver.EndReceive(ar, ref endpoint) ?? EmptyPayload;
                if (TryParseMessage(payload, payload.Length, endpoint, out SeamlessSyncMessage parsed))
                {
                    MessageReceived?.Invoke(parsed);
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                if (disposed)
                {
                    return;
                }
                Logger.WriteLog("sync udp receive socket exception", Logger.GetLogFileName());
            }
            catch (Exception ex)
            {
                Logger.WriteLog(ex.ToString(), Logger.GetLogFileName());
            }
            finally
            {
                BeginReceive();
            }
        }

        public void Stop()
        {
            disposed = true;

            try
            {
                receiver?.Close();
            }
            catch
            {
            }

            try
            {
                sender?.Close();
            }
            catch
            {
            }

            receiver = null;
            sender = null;
        }

        public void SendIndex(IEnumerable<IPEndPoint> targets, int playlistIndex)
        {
            if (targets == null)
            {
                return;
            }

            int safeIndex = Math.Max(0, playlistIndex);
            byte[] payload = new byte[BinaryIndexMessageLength];
            payload[0] = BinaryMessagePrefix;
            payload[1] = BinaryIndexMessageType;
            Buffer.BlockCopy(BitConverter.GetBytes(safeIndex), 0, payload, 2, sizeof(int));
            SendPayload(payload, targets);
        }

        public void SendIndexWithPosition(IEnumerable<IPEndPoint> targets, int playlistIndex, TimeSpan position)
        {
            if (targets == null)
            {
                return;
            }

            int safeIndex = Math.Max(0, playlistIndex);
            long safePositionMilliseconds = Math.Max(0L, (long)position.TotalMilliseconds);
            byte[] payload = new byte[BinaryIndexWithPositionMessageLength];
            payload[0] = BinaryMessagePrefix;
            payload[1] = BinaryIndexWithPositionMessageType;
            Buffer.BlockCopy(BitConverter.GetBytes(safeIndex), 0, payload, 2, sizeof(int));
            Buffer.BlockCopy(BitConverter.GetBytes(safePositionMilliseconds), 0, payload, 6, sizeof(long));
            SendPayload(payload, targets);
        }

        public void SendIndexRequestBroadcast()
        {
            if (sPort <= 0)
            {
                return;
            }

            byte[] payload = new byte[BinaryRequestMessageLength];
            payload[0] = BinaryMessagePrefix;
            payload[1] = BinaryRequestMessageType;
            SendPayload(payload, BuildDirectedBroadcastTargets());
        }

        public void Dispose()
        {
            Stop();
        }

        private void SendPayload(byte[] payload, IEnumerable<IPEndPoint> targets)
        {
            if (targets == null || payload == null || payload.Length == 0 || sender == null || disposed)
            {
                return;
            }

            try
            {
                foreach (IPEndPoint target in targets)
                {
                    if (target == null)
                    {
                        continue;
                    }

                    sender.Send(payload, payload.Length, target);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        private IEnumerable<IPEndPoint> BuildDirectedBroadcastTargets()
        {
            var targets = new List<IPEndPoint>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (IPAddress address in GetDirectedBroadcastAddresses())
            {
                string text = address.ToString();
                if (seen.Add(text))
                {
                    targets.Add(new IPEndPoint(address, sPort));
                }
            }

            return targets;
        }

        private static IEnumerable<IPAddress> GetDirectedBroadcastAddresses()
        {
            var results = new List<IPAddress>();

            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter == null
                        || adapter.OperationalStatus != OperationalStatus.Up
                        || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback
                        || adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    IPInterfaceProperties properties = adapter.GetIPProperties();
                    if (properties == null)
                    {
                        continue;
                    }

                    foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
                    {
                        IPAddress address = unicast?.Address;
                        IPAddress mask = unicast?.IPv4Mask;
                        if (address == null
                            || mask == null
                            || address.AddressFamily != AddressFamily.InterNetwork
                            || mask.AddressFamily != AddressFamily.InterNetwork)
                        {
                            continue;
                        }

                        byte[] addressBytes = address.GetAddressBytes();
                        byte[] maskBytes = mask.GetAddressBytes();
                        if (addressBytes.Length != 4 || maskBytes.Length != 4)
                        {
                            continue;
                        }

                        var broadcastBytes = new byte[4];
                        for (int i = 0; i < 4; i++)
                        {
                            broadcastBytes[i] = (byte)(addressBytes[i] | (~maskBytes[i]));
                        }

                        var broadcastAddress = new IPAddress(broadcastBytes);
                        if (!broadcastAddress.Equals(address))
                        {
                            results.Add(broadcastAddress);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLog(ex.ToString(), Logger.GetLogFileName());
            }

            return results;
        }

        private static bool TryParseMessage(byte[] payload, int length, IPEndPoint remote, out SeamlessSyncMessage parsed)
        {
            parsed = null;
            if (payload == null || length <= 0)
            {
                return false;
            }

            if (length >= BinaryIndexMessageLength
                && payload[0] == BinaryMessagePrefix
                && payload[1] == BinaryIndexMessageType)
            {
                int playlistIndex = BitConverter.ToInt32(payload, 2);
                parsed = new SeamlessSyncMessage(
                    SeamlessSyncMessageType.Index,
                    Math.Max(0, playlistIndex),
                    TimeSpan.Zero,
                    remote,
                    string.Empty);
                return true;
            }

            if (length >= BinaryIndexWithPositionMessageLength
                && payload[0] == BinaryMessagePrefix
                && payload[1] == BinaryIndexWithPositionMessageType)
            {
                int playlistIndex = BitConverter.ToInt32(payload, 2);
                long positionMilliseconds = BitConverter.ToInt64(payload, 6);
                parsed = new SeamlessSyncMessage(
                    SeamlessSyncMessageType.IndexWithPosition,
                    Math.Max(0, playlistIndex),
                    TimeSpan.FromMilliseconds(Math.Max(0L, positionMilliseconds)),
                    remote,
                    string.Empty);
                return true;
            }

            if (length >= BinaryRequestMessageLength
                && payload[0] == BinaryMessagePrefix
                && payload[1] == BinaryRequestMessageType)
            {
                parsed = new SeamlessSyncMessage(
                    SeamlessSyncMessageType.Request,
                    -1,
                    TimeSpan.Zero,
                    remote,
                    string.Empty);
                return true;
            }

            string message = MessageEncoding.GetString(payload, 0, length);
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            string[] parts = message.Split(',');
            if (parts.Length < 2)
            {
                return false;
            }

            string typeToken = parts[0].Trim();
            if (string.Equals(typeToken, "R", StringComparison.OrdinalIgnoreCase))
            {
                parsed = new SeamlessSyncMessage(SeamlessSyncMessageType.Request, -1, TimeSpan.Zero, remote, message);
                return true;
            }

            if (string.Equals(typeToken, "P", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length < 3)
                {
                    return false;
                }

                string positionIndexToken = parts[1].Trim();
                string positionToken = parts[2].Trim();
                if (!int.TryParse(positionIndexToken, out int parsedPositionPlaylistIndex)
                    || !long.TryParse(positionToken, out long parsedPositionMilliseconds))
                {
                    return false;
                }

                parsed = new SeamlessSyncMessage(
                    SeamlessSyncMessageType.IndexWithPosition,
                    Math.Max(0, parsedPositionPlaylistIndex),
                    TimeSpan.FromMilliseconds(Math.Max(0L, parsedPositionMilliseconds)),
                    remote,
                    message);
                return true;
            }

            if (!string.Equals(typeToken, "I", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string indexToken = parts[1].Trim();
            if (!int.TryParse(indexToken, out int parsedPlaylistIndex))
            {
                return false;
            }

            parsed = new SeamlessSyncMessage(SeamlessSyncMessageType.Index, parsedPlaylistIndex, TimeSpan.Zero, remote, message);
            return true;
        }
    }
}
