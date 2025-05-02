using System;
using System.Net;
using SharpPcap;
using SharpPcap.LibPcap;
using PacketDotNet;
using PacketDotNet.Utils;
using RawTcpSniffer;

namespace RawTcpSniffer
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("RawTcpSniffer");
            Console.WriteLine("Введите ваш Ip (используйте ipconfig в cmd):");
            string ip = Console.ReadLine();
            Console.WriteLine("Введите порт (например 5243):");
            int port = int.Parse(Console.ReadLine());
            RawTcpSniffer RTS = new RawTcpSniffer(new IPEndPoint(IPAddress.Parse(ip), port:port));
            RTS.Start();
            Thread.Sleep(Timeout.Infinite);
        }
    }
}