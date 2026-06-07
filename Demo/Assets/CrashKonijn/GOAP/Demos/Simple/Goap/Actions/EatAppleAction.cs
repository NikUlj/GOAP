using CrashKonijn.Agent.Core;
using CrashKonijn.Agent.Runtime;
using CrashKonijn.Goap.Demos.Simple.Behaviours;
using CrashKonijn.Goap.Demos.Simple.Behaviours.Benchmark;
using CrashKonijn.Goap.Runtime;
using UnityEngine;

namespace CrashKonijn.Goap.Demos.Simple.Goap.Actions
{
    [GoapId("Simple-EatAppleAction")]
    public class EatAppleAction : GoapActionBase<EatAppleAction.Data>
    {
        public override void Created()
        {
        }

        public override void Start(IMonoAgent agent, Data data)
        {
        }
        
        public override bool IsValid(IActionReceiver agent, Data data)
        {
            if (data.Inventory == null)
                return false;

            if (data.Inventory.HeldAppleNutrition <= 0f)
                return false;
            
            if (data.SimpleHunger == null)
                return false;

            return true;
        }

        public override IActionRunState Perform(IMonoAgent agent, Data data, IActionContext context)
        {
            if (data.Inventory == null)
                return ActionRunState.StopAndLog("Inventory is null.");
            
            if (data.SimpleHunger == null)
                return ActionRunState.StopAndLog("SimpleHunger is null.");

            if (data.Inventory.HeldAppleNutrition <= 0f)
                return ActionRunState.StopAndLog("No apple in inventory.");

            var eatNutrition = context.DeltaTime * 20f;

            data.Inventory.SetHeldAppleNutrition(data.Inventory.HeldAppleNutrition - eatNutrition);
            data.SimpleHunger.hunger -= eatNutrition;

            if (data.Inventory.HeldAppleNutrition <= 0f)
            {
                data.Consumed = true;
                return ActionRunState.Completed;
            }
            
            return ActionRunState.Continue;
        }
        
        public override void Stop(IMonoAgent agent, Data data)
        {
            this.Finish(agent, data);
        }

        public override void Complete(IMonoAgent agent, Data data)
        {
            this.Finish(agent, data);
        }
        
        private void Finish(IMonoAgent agent, Data data)
        {
            if (data.Consumed)
            {
                data.Inventory.Clear();
                SimpleBenchmarkRuntimeStats.RecordAppleEaten();
            }
        }

        public class Data : IActionData
        {
            public ITarget Target { get; set; }
            public bool Consumed { get; set; }
            
            [GetComponent]
            public SimpleHungerBehaviour SimpleHunger { get; set; }
            
            [GetComponent]
            public InventoryBehaviour Inventory { get; set; }
        }
    }
}
