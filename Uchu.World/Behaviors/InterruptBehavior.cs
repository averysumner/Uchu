using System.Threading.Tasks;

namespace Uchu.World.Behaviors
{
    public class InterruptBehavior : BehaviorBase
    {
        public override BehaviorTemplateId Id => BehaviorTemplateId.Interrupt;
        
        public int InterruptAttack { get; set; }
        public int InterruptBlock { get; set; }
        public int InterruptCharge { get; set; }
        public int InteruptAttack { get; set; }
        public int InteruptCharge { get; set; }
        
        public int Target { get; set; }
        
        public override async Task BuildAsync()
        {
            InterruptAttack = await GetParameter<int>("interrupt_attack");
            InterruptBlock = await GetParameter<int>("interrupt_block");
            InterruptCharge = await GetParameter<int>("interrupt_charge");
            InteruptAttack = await GetParameter<int>("interupt_attack");
            InteruptCharge = await GetParameter<int>("interupt_charge");

            Target = await GetParameter<int>("target");
        }

        public override Task ExecuteAsync(ExecutionContext context, ExecutionBranchContext branchContext)
        {
            if (Target != context.Associate)
            {
                context.Reader.ReadBit();
            }

            if (InterruptBlock == 0)
            {
                context.Reader.ReadBit();
            }

            context.Reader.ReadBit();
            
            return Task.CompletedTask;
        }
    }
}