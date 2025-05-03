using System.Net;
using SharpPcap;
using SharpPcap.LibPcap;
using PacketDotNet;

namespace RawSocket.Common;

public interface IRawTcpConnection : IDisposable
{
    IPEndPoint LocalEndPoint { get; }
    IPEndPoint RemoteEndPoint { get; }
    RawTcpState State { get; }
    void Send(byte[] data);
    byte[] Receive();
    Task<byte[]> ReceiveAsync(CancellationToken cancellationToken = default);
    void Close();
    void EnqueueReceivedData(byte[] data);
}