using System.Threading.Tasks;
using RakDotNet.IO;

namespace Uchu.World.Behaviors
{
    public class AlterCooldown : Behavior
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.AlterCooldown;
        
        public override async Task Serialize(BitReader reader)
        {
            // TODO
            return;
        }
    }
}