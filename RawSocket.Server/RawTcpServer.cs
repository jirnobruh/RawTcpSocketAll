using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Xml.Linq;
using SharpPcap;
using SharpPcap.LibPcap;
using PacketDotNet;
using RawSocket.Common;
using RawTcpSniffer;

namespace RawSocket.Server;

public class RawTcpServer : IRawTcpServer
{ 
    private BlockingCollection<IRawTcpConnection> _acceptedConnections = new();
    
    private ConcurrentDictionary<IPEndPoint, IRawTcpConnection> _activeConnections = new();
    
    private readonly ConcurrentDictionary<IPEndPoint, HandshakeState> _handshakeStates = new();
    private IRawTcpServer _rawTcpServerImplementation;
    
    public IPEndPoint LocalEndPoint { get; }
    private LibPcapLiveDevice _device;
    private Thread _captureThread;

    private PhysicalAddress _localMac;
    
    private readonly Logger _logger;

    public RawTcpServer(IPEndPoint localEndPoint, Logger logger)
    {
        LocalEndPoint = localEndPoint;
        _logger = logger;
        var devices = LibPcapLiveDeviceList.Instance;
        _device = devices.FirstOrDefault(d => d.Interface.Addresses.Any(a =>
            a.Addr.ipAddress?.ToString() ==
            localEndPoint.Address.ToString()));
        if (_device == null)
            throw new Exception("Сетевое устройство не найдено");
    }

    private static bool IsSynPacket(TcpPacket tcp) =>
        tcp is { Synchronize: true, Acknowledgment: false };

    private static bool IsFinalAck(TcpPacket tcp) =>
        tcp is { Acknowledgment: true, Synchronize: false };
    
    private static (IPv4Packet? ip, TcpPacket? tcp) ParseTcpPacket(PacketCapture packetCapture)
    {
        var rawPacket = packetCapture.GetPacket();
        var packet = rawPacket.GetPacket();
        var ipPacket = packet.Extract<IPv4Packet>();
        var tcpPacket = packet.Extract<TcpPacket>();
        return (ipPacket, tcpPacket);
    }
    
    public void Start()
    {
        if (_device == null)
        {
            throw new InvalidOperationException("Ошибка: device не инициализирован");
        }

        _device.Open(DeviceModes.Promiscuous, 1000);
        _device.OnPacketArrival += HandlePacket;
        _captureThread = new Thread(() => _device.StartCapture());
        _captureThread.Start();
        _device.Filter = "tcp";
        /*_device.Filter = "tcp dst port 80";
        _device.Filter = "tcp[tcpflags] & tcp-syn != 0";
        _device.Filter = "src host 192.168.0.10";*/
        
        _logger.Log($"Сервер запущен на {LocalEndPoint.Address}:{LocalEndPoint.Port}");
    }

    public void Stop()
    {
        _device.StopCapture();
        _captureThread?.Join();
        _device.Close();
    }
    
    // TODO --- Щито это такое и почему оно нужно? ---
    public IRawTcpConnection AcceptRawTcpConnection()
    {
        var connection = _acceptedConnections.Take();
        _logger.Log($"Принято соединение с {connection.RemoteEndPoint}");
        return connection;
    }
    
    public async Task<IRawTcpConnection> AcceptRawTcpConnectionAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => _acceptedConnections.Take(cancellationToken),
        cancellationToken);
    }
    
    private void HandlePacket(object sender, PacketCapture capture)
    {
        // Получаем пакет и парсим его в отдельном методе, возвращая кортеж
        var (ipPacket, tcpPacket) = ParseTcpPacket(capture);
        if (tcpPacket is null || ipPacket is null)
            return;

        // Далее нужно установить доверенное соединение.
        // Сохраняйте промежуточные состояния процесса thre-way-handshake
        // в ранее созданный словарь _handshakeStates
        // Если соединение уже было установлено, тогда уже стоит
        // вызвать метод HandlePayloadPacket(...)
        
        var remoteEndpoint = new IPEndPoint(ipPacket.SourceAddress, tcpPacket.SourcePort);
        
        // Проверяем, является ли пакет SYN (начало handshake)
        if (IsSynPacket(tcpPacket))
        {
            HandleSynPacket(remoteEndpoint, tcpPacket, ipPacket);
            return;
        }

        // Проверяем, является ли пакет финальным ACK (завершение handshake)
        if (_handshakeStates.TryGetValue(remoteEndpoint, out var state) && IsFinalAck(tcpPacket))
        {
            _logger.Log($"Получен финальный ACK от {remoteEndpoint}, устанавливаем соединение");
            HandleFinalAck(remoteEndpoint, tcpPacket, state);
            _handshakeStates.Remove(remoteEndpoint, out state); // Удаляем запись, handshake завершен
            return;
        }

        // Если соединение уже установлено, передаем пакет обработчику данных
        if (_activeConnections.TryGetValue(remoteEndpoint, out var connection))
        {
            _logger.Log($"Получены данные от {remoteEndpoint}: {tcpPacket.PayloadData.Length} байт");
            HandlePayloadPacket(connection, tcpPacket, remoteEndpoint);
        }

    }
    
    private void HandleSynPacket(IPEndPoint remoteEndpoint, TcpPacket tcpPacket, IPv4Packet ipPacket)
    {
        Console.WriteLine($"[Listener] Получен SYN от {remoteEndpoint}");
        _logger.Log($"[Listener] Получен SYN от {remoteEndpoint}");

        var serverSeq = (uint)Random.Shared.Next(5000, 10000);
        var clientSeq = tcpPacket.SequenceNumber;

        _handshakeStates[remoteEndpoint] = new HandshakeState
        {
            ServerSeq = serverSeq,
            ClientSeq = clientSeq,
            Timestamp = DateTime.UtcNow
        };

        var synAckPacket = new TcpHeaderBuilder(_localMac)
            .WithSynAck()
            .WithSequence(serverSeq)
            .WithAck(clientSeq + 1)
            .From(LocalEndPoint.Address, LocalEndPoint.Port)
            .To(ipPacket.SourceAddress, tcpPacket.SourcePort)
            .Build();

        SendPacket(synAckPacket.Bytes);
        Console.WriteLine("[Listener] Отправлен SYN-ACK");
    }
    
    private void HandlePayloadPacket(IRawTcpConnection connection, TcpPacket tcpPacket, IPEndPoint remoteEndpoint)
    {
        var payload = tcpPacket.PayloadData;

        if (payload.Length > 0)
        {
            Console.WriteLine($"[Listener] Получены данные от {remoteEndpoint}: {payload.Length} байт");
            connection.EnqueueReceivedData(payload);

            // Можно вернуть ACK клиенту при необходимости
        }
    }
    
    private void HandleFinalAck(IPEndPoint remoteEndpoint, TcpPacket tcpPacket, HandshakeState state)
    {
        if (tcpPacket.AcknowledgmentNumber != state.ServerSeq + 1)
        {
            Console.WriteLine($"[Listener] Получен ACK с неверным номером от {remoteEndpoint}");
            return;
        }

        Console.WriteLine($"[Listener] Получен финальный ACK от {remoteEndpoint}. Соединение установлено.");

        IRawTcpConnection connection = new RawTcpConnection(
            localEndPoint: LocalEndPoint,
            remoteEndPoint: remoteEndpoint,
            SendPacket,
            state.ClientSeq,
            state.ServerSeq,
            new TcpHeaderBuilder(_localMac)
        );

        _activeConnections.TryAdd(remoteEndpoint, connection);
        _acceptedConnections.Add(connection);
    }
    
    private LibPcapLiveDevice FindDeviceFor(IPAddress localAddress)
    {
        return LibPcapLiveDeviceList.Instance
                   .FirstOrDefault(device => HasAddress(device, localAddress))
               ?? throw new Exception($"Сетевое устройство не найдено для IP: {localAddress}");
    }

    private bool HasAddress(LibPcapLiveDevice device, IPAddress address)
    {
        return device.Interface.Addresses
            .Any(a => Equals(a.Addr?.ipAddress, address));
    }

    private void SendPacket(byte[] bytes)
    {
        _logger.Log($"Отправка пакета размером {bytes.Length} байт");
        _device.SendPacket(bytes);
    }

    private PhysicalAddress? GetMacAddress(IPAddress ip) =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.GetIPProperties().UnicastAddresses.Any(addr => addr.Address.Equals(ip)))
            .Select(nic => nic.GetPhysicalAddress())
            .FirstOrDefault();
}