namespace RawSocket.Server;

public class Logger : ILogg
{
    private readonly string _logFilePath = "server_log.txt";
    
    public void Log(string ipSource, int portSource, string ipDest, int portDest, string sequenceNumber, string flags)
    {
        string logMessage = $"[{DateTime.Now}] {ipSource}:{portSource} -> {ipDest}:{portDest}, Seq={sequenceNumber}, Флаги={flags}";
        WriteLog(logMessage);
    }

    public void Log(string message)
    {
        string logMessage = $"[{DateTime.Now}] {message}";
        WriteLog(logMessage);
    }

    private void WriteLog(string logMessage)
    {
        Console.WriteLine(logMessage); // Выводим в консоль

        try
        {
            using (StreamWriter sw = new StreamWriter(_logFilePath, true))
            {
                sw.WriteLine(logMessage);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка записи в лог-файл: {ex.Message}");
        }
    }

    /*
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
    }*/
}