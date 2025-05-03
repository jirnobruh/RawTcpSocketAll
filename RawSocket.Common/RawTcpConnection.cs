using System.Collections.Concurrent;
using System.Net;

namespace RawSocket.Common;

public class RawTcpConnection : IRawTcpConnection
{
    private readonly Action<byte[]> _sendDelegate;
    private readonly BlockingCollection<byte[]> _receiveQueue = new();

    private TcpHeaderBuilder _tcpHeaderBuilder;
    private uint _currentSeq;
    private uint _currentAck;

    public IPEndPoint LocalEndPoint { get; }
    public IPEndPoint RemoteEndPoint { get; }
    public RawTcpState State { get; private set; }

    /// <summary>
    /// Конструктор принимает локальную и удалённую конечные точки, а такжеделегат для отправки данных.
        /// </summary>
        /// <param name="localEndPoint">Локальная конечная точка.</param>
        /// <param name="remoteEndPoint">Удалённая конечная точка.</param>
        /// <param name="sendDelegate">Делегат, который выполняет отправку данных на физическом уровне
        /// (например, через SharpPcap).</param>
    /// <param name="currentSeq"></param>
    /// <param name="currentAck"></param>
    public RawTcpConnection(IPEndPoint localEndPoint, IPEndPoint remoteEndPoint, Action<byte[]> sendDelegate,
        uint currentSeq, uint currentAck, TcpHeaderBuilder tcpHeaderBuilder)
    {
        LocalEndPoint = localEndPoint;
        RemoteEndPoint = remoteEndPoint;
        _sendDelegate = sendDelegate ?? throw new
            ArgumentNullException(nameof(sendDelegate));
        _currentSeq = currentSeq;
        _currentAck = currentAck;
        _tcpHeaderBuilder = tcpHeaderBuilder;
        State = RawTcpState.Connected;
    }
    
    public void EnqueueReceivedData(byte[] data)
    {
        if (State == RawTcpState.Connected && !_receiveQueue.IsAddingCompleted)
        {
            _receiveQueue.Add(data);
            _currentAck += (uint)data.Length;
        }
    }
    
    public void Send(byte[] data)
    {
        if (State != RawTcpState.Connected)
            throw new InvalidOperationException("Соединение закрыто.");

        var packet = _tcpHeaderBuilder
            .WithSequence(_currentSeq)
            .WithAck(_currentAck)
            .From(LocalEndPoint.Address, LocalEndPoint.Port)
            .To(RemoteEndPoint.Address, RemoteEndPoint.Port)
            .WithPayload(data)
            .Build();

        _sendDelegate(packet.Bytes);
        _currentSeq += (uint)data.Length;
    }

    public byte[] Receive()
    {
        return _receiveQueue.TryTake(out byte[] data, TimeSpan.FromSeconds(5)) ? data : Array.Empty<byte>();
    }

    public void Close()
    {
        State = RawTcpState.Closed;
        _receiveQueue.CompleteAdding();
    }

    // TODO --- Щито это такое и почему оно нужно2? ---
    
    public Task<byte[]> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
    // ------------------------------------------
}