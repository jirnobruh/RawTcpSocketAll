namespace RawSocket.Common;

public interface IRawTcpServer
{
    void Start();
    void Stop();
    IRawTcpConnection AcceptRawTcpConnection();
    Task<IRawTcpConnection> AcceptRawTcpConnectionAsync(CancellationToken cancellationToken = default);
}