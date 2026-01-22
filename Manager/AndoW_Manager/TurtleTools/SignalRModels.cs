using System;

namespace TurtleTools
{
    public sealed class SignalRHeartbeatPayload
    {
        public string ClientId { get; set; }
        public string Status { get; set; }
        public int Process { get; set; }
        public string Version { get; set; }
        public string CurrentPage { get; set; }
        public bool HdmiState { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public sealed class SignalRHeartbeatEventArgs : EventArgs
    {
        public SignalRHeartbeatEventArgs(SignalRHeartbeatPayload payload)
        {
            Payload = payload;
        }

        public SignalRHeartbeatPayload Payload { get; }
    }

    public sealed class SignalRCommandEnvelope
    {
        public string CommandId { get; set; }
        public string Command { get; set; }
        public string PlayerId { get; set; }
        public string PayloadJson { get; set; }
        public string CreatedAt { get; set; }
        public bool IsUrgent { get; set; }
    }

    public class SignalRMessage
    {
        public string From { get; set; } = "Server";

        public string To { get; set; } = "All";

        public string Command { get; set; } = "Update";

        public string DataType { get; set; } = "String";

        public object Data { get; set; } = null;
    }

    public class StateMessage
    {
        public string Who { get; set; } = "Unknown";

        public string State { get; set; } = "Disconnected";

        public string Description { get; set; } = "";
    }

    public class ProgressData
    {
        public string FromGUID { get; set; }

        public int Porgress { get; set; }
    }
}
