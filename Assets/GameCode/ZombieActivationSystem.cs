using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Burst;
using Unity.Mathematics;

[UpdateAfter(typeof(HumanToZombieSystem))]
class ZombieActivationSystem : JobComponentSystem
{
    private ComponentGroup zombieDataGroup;
    
    protected override void OnStartRunning()
    {
        zombieDataGroup = GetComponentGroup(typeof(Zombie), typeof(Velocity), typeof(Position));
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var zombieData = zombieDataGroup.GetComponentDataArray<Zombie>();
        var zombiePositions = zombieDataGroup.GetComponentDataArray<Position>();
        var zombieVelocities = zombieDataGroup.GetComponentDataArray<Velocity>();
        
        var activationJob = new ZombieActivationJob{
            zombieSpeed = ZombieSettings.Instance.ZombieSpeed,
            zombieData = zombieData,
            zombiePositions = zombiePositions,
            zombieVelocities = zombieVelocities
        };

        return activationJob.Schedule(
            zombieData.Length, 64, inputDeps
        );
    }
}

[BurstCompile]
public struct ZombieActivationJob : IJobParallelFor
{
    public float zombieSpeed;

    public ComponentDataArray<Zombie> zombieData;
    
    public ComponentDataArray<Position> zombiePositions;
    public ComponentDataArray<Velocity> zombieVelocities;
    
    public void Execute(int index)
    {
        var zombie = zombieData[index];
        
        if (zombie.BecomeActive != 1) 
            return;
        if (zombie.FinishedActivation != 0) 
            return;
        
        var position = zombiePositions[index];
        position.Value = new float3(
            zombie.BecomeZombiePosition.x,
            0f, zombie.BecomeZombiePosition.y
        );
        zombiePositions[index] = position;

        var velocity = zombieVelocities[index];
        velocity.Value = 6;
        zombieVelocities[index] = velocity;

        zombie.FinishedActivation = 1;
        zombieData[index] = zombie;
    }
}
