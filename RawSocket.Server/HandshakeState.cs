namespace RawSocket.Server;

public class HandshakeState
{
    public uint ServerSeq { get; set; }
    public uint ClientSeq { get; set; }
    public DateTime Timestamp { get; set; }
}