using Microsoft.EntityFrameworkCore;
using RakDotNet;
using Uchu.Core;

namespace Uchu.World
{
    public class SyncSkillMessage : ClientGameMessage
    {
        public override ushort GameMessageId => 0x0479;

        public bool IsDone { get; set; }

        public byte[] Data { get; set; }

        public uint BehaviorHandle { get; set; }

        public uint SkillHandle { get; set; }

        public override void Deserialize(BitStream stream)
        {
            IsDone = stream.ReadBit();
            Data = stream.Read((int) stream.ReadUInt());
            BehaviorHandle = stream.ReadUInt();
            SkillHandle = stream.ReadUInt();
        }
    }
}