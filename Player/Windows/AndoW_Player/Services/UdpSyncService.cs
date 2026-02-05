using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TurtleTools;

namespace HyOnPlayer.Services
{
    internal enum UdpSyncMessageType
    {
        Prepare,
        Commit
    }

    internal sealed class UdpSyncMessage
    {
        public UdpSyncMessage(UdpSyncMessageType type, int index, IPEndPoint remoteEndPoint, string raw)
        {
            Type = type;
            Index = index;
            RemoteEndPoint = remoteEndPoint;
            Raw = raw;
        }

        public UdpSyncMessageType Type { get; }
        public int Index { get; }
        public IPEndPoint RemoteEndPoint { get; }
        public string Raw { get; }
    }

    internal sealed class UdpSyncService : IDisposable
    {
        public event Action<UdpSyncMessage> MessageReceived;

        public UDPer sUDP;
        public int sPort { get; set; }


        public void Start(int port)
        {
            if (sUDP != null)
                sUDP.Stop();

            sPort = port;
            sUDP = new UDPer();
            sUDP.Start(port);
            sUDP.MessageReceived += UDPMessageReceived;
        }

        private void UDPMessageReceived(IPEndPoint endpoint, string msg)
        {
            try
            {
                if (TryParseMessage(msg, endpoint, out UdpSyncMessage parsed))
                {
                    MessageReceived?.Invoke(parsed);
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLog(ex.ToString(), Logger.GetLogFileName());
            }
        }

        public void Stop()
        {
            sUDP.MessageReceived -= UDPMessageReceived;
            sUDP.Stop();
        }

        public void SendPrepare(IEnumerable<IPEndPoint> targets, int index)
        {
            SendMessage("P", targets, index);
        }

        public void SendCommit(IEnumerable<IPEndPoint> targets, int index)
        {
            SendMessage("C", targets, index);
        }

        public void Dispose()
        {
            Stop();
        }

        private void SendMessage(string prefix, IEnumerable<IPEndPoint> targets, int index)
        {
            if (targets == null)
            {
                return;
            }

            string payload = string.Concat(prefix, ",", index.ToString());

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    using (UdpClient sender = new UdpClient())
                        UDPer.SendUnicast(payload, targets);
                }
                catch (Exception ex)
                {
                    Logger.WriteLog(ex.ToString(), Logger.GetLogFileName());
                }
            });
        }

        private static bool TryParseMessage(string message, IPEndPoint remote, out UdpSyncMessage parsed)
        {
            parsed = null;
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
            string indexToken = parts[1].Trim();

            if (!int.TryParse(indexToken, out int index))
            {
                return false;
            }

            UdpSyncMessageType type;
            if (string.Equals(typeToken, "P", StringComparison.OrdinalIgnoreCase))
            {
                type = UdpSyncMessageType.Prepare;
            }
            else if (string.Equals(typeToken, "C", StringComparison.OrdinalIgnoreCase))
            {
                type = UdpSyncMessageType.Commit;
            }
            else
            {
                return false;
            }

            parsed = new UdpSyncMessage(type, index, remote, message);
            return true;
        }
    }
}
