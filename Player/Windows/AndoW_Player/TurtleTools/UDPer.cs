using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace TurtleTools
{
    public delegate void MessageReceivedHandler(IPEndPoint endpoint, string msg);

    class UDPer
    {
        int PORT_NUMBER = 80;
        IPAddress RECV_ADDRESS = IPAddress.Any;

        UdpClient udp;
        IAsyncResult ar_ = null;

        public event MessageReceivedHandler MessageReceived;

        public void Start(int port = 80, IPAddress address = null)
        {
            if (ar_ != null)
                return;

            PORT_NUMBER = port;

            udp = new UdpClient(PORT_NUMBER);

            if (address != null)
                RECV_ADDRESS = address;

            StartListening();
        }

        public void Stop()
        {
            try
            {
                udp.Close();
                ar_ = null;
            }
            catch { /* don't care */ }
        }

        private void StartListening()
        {
            ar_ = udp.BeginReceive(Receive, new object());
        }
        
        private void Receive(IAsyncResult ar)
        {
            if (ar_ != null)
            {
                try
                {
                    IPEndPoint ip = new IPEndPoint(RECV_ADDRESS, PORT_NUMBER);
                    byte[] bytes = udp.EndReceive(ar, ref ip);
                    Encoding utf8 = new UTF8Encoding(false);
                    string message = utf8.GetString(bytes);
                    if (MessageReceived != null)
                        MessageReceived(ip, message);
                    StartListening();
                }
                catch (ObjectDisposedException ee) { }
            }
            else
                Thread.Sleep(1000);
        }

        public static void SendBroadcast(string message, int port = 80)
        {
            IPEndPoint ip = new IPEndPoint(IPAddress.Broadcast, port);
            Encoding utf8 = new UTF8Encoding(false);
            byte[] bytes = utf8.GetBytes(message);

            using (UdpClient client = new UdpClient())
            {
                client.SendAsync(bytes, bytes.Length, ip);
                client.Close();
            }
        }

        public static void SendBroadcast(byte[] data, int port = 80)
        {
            IPEndPoint ip = new IPEndPoint(IPAddress.Broadcast, port);
            Encoding utf8 = new UTF8Encoding(false);

            using (UdpClient client = new UdpClient())
            {
                client.SendAsync(data, data.Length, ip);
                client.Close();
            }
        }

        public static void SendUnicast(string message, string ipstr, int port)
        {
            bool isAddress = IPAddress.TryParse(ipstr, out IPAddress _ip);

            if (isAddress == false)
                return;

            IPEndPoint ip = new IPEndPoint(_ip, port);

            Encoding utf8 = new UTF8Encoding(false);
            byte[] bytes = utf8.GetBytes(message);

            using (UdpClient client = new UdpClient())
            {
                client.SendAsync(bytes, bytes.Length, ip);
                client.Close();
            }
        }

        public static void SendUnicast(string message, IPAddress ip_addr, int port)
        {
            IPEndPoint ip = new IPEndPoint(ip_addr, port);
            Encoding utf8 = new UTF8Encoding(false);
            byte[] bytes = utf8.GetBytes(message);

            using (UdpClient client = new UdpClient())
            {
                client.SendAsync(bytes, bytes.Length, ip);
                client.Close();
            }
        }

        public static void SendUnicast(string msg, IEnumerable<string> ips, int port)
        {
            Encoding utf8 = new UTF8Encoding(false);
            byte[] bytes = utf8.GetBytes(msg);

            using (UdpClient client = new UdpClient())
            {
                foreach (string ip in ips)
                {
                    bool isAddress = IPAddress.TryParse(ip, out IPAddress _ip);

                    if (isAddress == false)
                        continue;

                    client.SendAsync(bytes, bytes.Length, new IPEndPoint(_ip, port));
                }
                client.Close();
            }
        }

        public static void SendUnicast(string msg, IEnumerable<IPAddress> ips, int port)
        {
            Encoding utf8 = new UTF8Encoding(false);
            byte[] bytes = utf8.GetBytes(msg);

            using (UdpClient client = new UdpClient())
            {
                foreach (IPAddress ip in ips)
                    client.SendAsync(bytes, bytes.Length, new IPEndPoint(ip, port));
                client.Close();
            }
        }

        public static void SendUnicast(string message, IPEndPoint endpoint)
        {
            Encoding utf8 = new UTF8Encoding(false);
            byte[] bytes = utf8.GetBytes(message);

            using (UdpClient client = new UdpClient())
            {
                client.SendAsync(bytes, bytes.Length, endpoint);
                client.Close();
            }
        }

        public static void SendUnicast(string message, IEnumerable<IPEndPoint> endpoints)
        {
            Encoding utf8 = new UTF8Encoding(false);
            byte[] bytes = utf8.GetBytes(message);

            using (UdpClient client = new UdpClient())
            {
                foreach(IPEndPoint endpoint in endpoints)
                    client.SendAsync(bytes, bytes.Length, endpoint);
                client.Close();
            }
        }
    }
}
