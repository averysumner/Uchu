using RakDotNet.IO;
using Uchu.World.Parsers;

namespace Uchu.World
{
    [Essential]
    public class RigidBodyPhantomPhysicsComponent : ReplicaComponent
    {
        public override ReplicaComponentsId Id => ReplicaComponentsId.RigidBodyPhantomPhysics;

        public override void FromLevelObject(LevelObject levelObject)
        {
            
        }

        public override void Construct(BitWriter writer)
        {
            Serialize(writer);
        }

        public override void Serialize(BitWriter writer)
        {
            writer.WriteBit(true);
            writer.Write(Transform.Position);
            writer.Write(Transform.Rotation);
        }
    }
}