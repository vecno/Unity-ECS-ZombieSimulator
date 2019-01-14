using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Burst;

[UpdateAfter(typeof(HumanInfectionSystem))]
class HumanToZombieSystem : JobComponentSystem
{
    private ComponentGroup humanDataGroup;
    private ComponentGroup zombieDataGroup;
    
    protected override void OnStartRunning()
    {
        humanDataGroup = GetComponentGroup(typeof(Human), typeof(Velocity), typeof(Position));
        zombieDataGroup = GetComponentGroup(typeof(Zombie));
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var humanData = humanDataGroup.GetComponentDataArray<Human>();
        var humanPositions = humanDataGroup.GetComponentDataArray<Position>();
        var humanVelocities = humanDataGroup.GetComponentDataArray<Velocity>();
        
        var zombieData = zombieDataGroup.GetComponentDataArray<Zombie>();
        
        var toZombieJob = new HumanToZombieJob{
            humanData = humanData,
            zombieData = zombieData,
            humanPositions = humanPositions,
            humanVelocities = humanVelocities
        };

        return toZombieJob.Schedule(
            humanData.Length, 64, inputDeps
        );
    }
}

[BurstCompile]
public struct HumanToZombieJob : IJobParallelFor
{
    public ComponentDataArray<Human> humanData;
    public ComponentDataArray<Zombie> zombieData;

    public ComponentDataArray<Position> humanPositions;
    public ComponentDataArray<Velocity> humanVelocities;

    public void Execute(int index)
    {
        var human = humanData[index];
        
        if (human.IsInfected != 1)
            return;
        if (human.WasConverted != 0)
            return;

        // Note: Awkward part: Assumes that there is a zombie
        // for every human and that this zombie is not active.
        // It also depends on array indices as key's, bad form.
        
        var position = humanPositions[index];
        var originalPosition = position.Value;

        position.Value = EntityUtil.GetOffScreenLocation();
        humanPositions[index] = position;

        var zombie = zombieData[index];
        zombie.BecomeActive = 1;
        zombie.BecomeZombiePosition = originalPosition.xz;
        zombieData[index] = zombie;

        var velocity = humanVelocities[index];
        velocity.Value = 0;
        humanVelocities[index] = velocity;

        human.WasConverted = 1;
        humanData[index] = human;
    }
}
