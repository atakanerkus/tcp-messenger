namespace TcpMessenger.Core.Traffic;

/// <summary>Direction of a traffic entry, shown with the English labels required by the task.</summary>
public enum TrafficDirection
{
    /// <summary>Sent by this application ("Transmit").</summary>
    Transmit,

    /// <summary>Received from the peer ("Receive").</summary>
    Receive,
}
