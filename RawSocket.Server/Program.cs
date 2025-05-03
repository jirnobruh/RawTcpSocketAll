using System.Net;
using System.Text;
using SharpPcap;
using SharpPcap.LibPcap;
using RawSocket.Common;
using RawTcpSniffer;

namespace RawSocket.Server;

class Program
{
    static void Main(string[] args)
    {
        Logger logger = new Logger();
        Console.WriteLine("Введите Ip:");
        string ip = "192.168.0.15"; //Console.ReadLine();
        Console.WriteLine("Введите порт (например 5243):");
        int port = 5243; //int.Parse(Console.ReadLine());
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse(ip), port:port); 
        IRawTcpServer server = new RawTcpServer(localEndPoint, logger);
        server.Start();
        while (true)
        {
            var connection = server.AcceptRawTcpConnection();
            Task.Run(() => HandleClient(connection, logger));
        }
    }
    static void HandleClient(IRawTcpConnection connection, Logger logger)
    {
        logger.Log($"Соединение установлено с {connection.RemoteEndPoint}");
        connection.Send(Encoding.UTF8.GetBytes("Hello from RawTcpServer!"));
        
        while (connection.State == RawTcpState.Connected)
        {
            var data = connection.Receive();
            if (data.Length == 0) break;
            var message = Encoding.UTF8.GetString(data);
            Console.WriteLine(message);
            logger.Log($"Получено сообщение от {connection.RemoteEndPoint}: {message}");
            connection.Send(Encoding.UTF8.GetBytes($"Echo: {message}"));
        }
        logger.Log($"Соединение закрыто с {connection.RemoteEndPoint}");
        connection.Close();
    }
}