namespace RawSocket.Server;

public interface ILogg
{
    void Log(string ipSource, int portSource, string ipDest, int portDest, string sequenceNumber, string flags);
    void Log(string message);
}