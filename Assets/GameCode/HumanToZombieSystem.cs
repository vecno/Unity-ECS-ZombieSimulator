using Unity.Entities;

[UpdateAfter(typeof(HumanInfectionSystem))]
class HumanToZombieSystem : ComponentSystem
{
    private ComponentGroup infectedDataGroup;
    
    protected override void OnStartRunning()
    {
        infectedDataGroup = GetComponentGroup(typeof(Infected));
    }

    protected override void OnUpdate()
    {
        // Note: The only reason this is here is because,
        // MeshInstanceRenderer can not be used inside jobs.
        
        var zombieLook = ZombieSimulatorBootstrap.ZombieLook;
        var infectedEntities = infectedDataGroup.GetEntityArray();

        for (var i = 0; i <infectedEntities.Length; i++)
        {
            var entity = infectedEntities[i];
            PostUpdateCommands.RemoveComponent<Infected>(entity);
            PostUpdateCommands.SetSharedComponent(entity, zombieLook);
        }
    }
}
