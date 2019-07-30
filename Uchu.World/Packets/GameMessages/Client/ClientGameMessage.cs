using Uchu.World;

namespace Uchu.Core.Client
{
    public abstract class ClientGameMessage : Packet, IGameMessage
    {
        public override RemoteConnectionType RemoteConnectionType => RemoteConnectionType.Client;

        public override uint PacketId => 0x5;
        
        public abstract ushort GameMessageId { get; }
        
        public GameObject Associate { get; set; }
    }
}