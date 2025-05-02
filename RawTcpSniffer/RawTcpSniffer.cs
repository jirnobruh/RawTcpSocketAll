using System;
using System.Net;
using SharpPcap;
using SharpPcap.LibPcap;
using PacketDotNet;

namespace RawTcpSniffer;

public class RawTcpSniffer : IDisposable
{
    private LibPcapLiveDevice _device;
    public IPEndPoint LocalEndPoint { get; }
    private Thread _captureThread;

            

    public void Start()
    {
        _device.Open(DeviceModes.Promiscuous, 1000);
        _device.OnPacketArrival += HandlePacket;
        _captureThread = new Thread(() => _device.StartCapture());
        _captureThread.Start();
        /*_device.Filter = "tcp dst port 80";
        _device.Filter = "tcp[tcpflags] & tcp-syn != 0";
        _device.Filter = "src host 192.168.0.10";*/
    }

    public void Stop()
    {
        _device.StopCapture();
        _captureThread?.Join();
        _device.Close();
    }

    public void Dispose()
    {
        Stop();
        _device.OnPacketArrival -= HandlePacket;
    }

    public RawTcpSniffer(IPEndPoint localEndPoint)
    {
        LocalEndPoint = localEndPoint;
        var devices = LibPcapLiveDeviceList.Instance;
        _device = devices.FirstOrDefault(d => d.Interface.Addresses.Any(a =>
            a.Addr.ipAddress?.ToString() ==
            localEndPoint.Address.ToString()));
        if (_device == null)
            throw new Exception("Сетевое устройство не найдено");
    }

    private void HandlePacket(object sender, PacketCapture packetCapture)
    {
        var rawPacket = packetCapture.GetPacket();
        var packet = rawPacket.GetPacket();
        var ipPacket = packet.Extract<IPv4Packet>();
        var tcpPacket = packet.Extract<TcpPacket>();
        
        if (ipPacket == null || tcpPacket == null)
            return;
        
        var logger = new FileLogger();
        logger.Log(ipPacket.SourceAddress.ToString(), tcpPacket.SourcePort, ipPacket.DestinationAddress.ToString(),
            tcpPacket.DestinationPort, tcpPacket.SequenceNumber.ToString() , tcpPacket.Flags.ToString());
        Console.WriteLine("[{0}] {1}:{2} -> {3}:{4}, Seq={5}, Флаги={6}",
            DateTime.Now, ipPacket.SourceAddress.ToString(), tcpPacket.SourcePort, 
            ipPacket.DestinationAddress.ToString(), tcpPacket.DestinationPort, tcpPacket.SequenceNumber.ToString(), 
            tcpPacket.Flags.ToString());
    }
}
