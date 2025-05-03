namespace RawTcpSniffer;

public class FileLogger : ILogger
{
    public void Log(string ipSource, int portSource, string ipDest, int portDest, string sequenceNumber, string flags)
    {
        using (StreamWriter sw = new StreamWriter("tcp_log.txt", true))
        {
            sw.WriteLine("[{0}] {1}:{2} -> {3}:{4}, Seq={5}, Флаги={6}",
                DateTime.Now, ipSource, portSource, ipDest, portDest, sequenceNumber, flags);
        }

    }

    public void Log(string message)
    {
        // throw new NotImplementedException();
    }
}