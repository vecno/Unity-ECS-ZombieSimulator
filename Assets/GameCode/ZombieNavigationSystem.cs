using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Burst;

[UpdateAfter(typeof(ZombieTargetingSystem))]
class ZombieNavigationSystem : JobComponentSystem
{
    private ComponentGroup zombieDataGroup;
    private ComponentGroup zombieTargetGroup;

    protected override void OnStartRunning()
    {
        // Note: In theory this system only needs the position of the human targets,
        // in practice we leave this in to hint to a merger with the targeting system.
        // Reason: Keeping these component groups up-to-date takes time, and this setup
        // matches that of the targeting system, so why is it not a part of this system.
        zombieDataGroup = GetComponentGroup(typeof(Zombie), typeof(Heading), typeof(Velocity), typeof(Position));
        zombieTargetGroup = GetComponentGroup(typeof(Human), typeof(Heading), typeof(Position));
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var zombieData = zombieDataGroup.GetComponentDataArray<Zombie>();
        var zombieHeading = zombieDataGroup.GetComponentDataArray<Heading>();
        var zombieVelocity = zombieDataGroup.GetComponentDataArray<Velocity>();
        var zombiePosition = zombieDataGroup.GetComponentDataArray<Position>();
        
        var humanPosition = zombieTargetGroup.GetComponentDataArray<Position>();
        
        var navigationJob = new ZombieNavigationJob{
            zombieSpeed = ZombieSettings.Instance.ZombieSpeed,
            zombieHeading = zombieHeading,
            zombieVelocity = zombieVelocity,
            zombieData = zombieData,
            humanPosition = humanPosition,
            zombiePosition = zombiePosition
        };

        return navigationJob.Schedule(
            zombieData.Length, 64, inputDeps
        );
    }
}

[BurstCompile]
public struct ZombieNavigationJob : IJobParallelFor
{
    public float zombieSpeed;
    
    public ComponentDataArray<Heading> zombieHeading;
    public ComponentDataArray<Velocity> zombieVelocity;
 
    [ReadOnly]
    public ComponentDataArray<Zombie> zombieData;
    [ReadOnly]
    public ComponentDataArray<Position> humanPosition;
    [ReadOnly]
    public ComponentDataArray<Position> zombiePosition;

    public void Execute(int index)
    {
        var zombie = zombieData[index];
        
        if (zombie.BecomeActive != 1)
            return;
        
        var velocity = zombieVelocity[index];
        if (zombie.TargetIndex == -1)
        {
            velocity.Value = 0;
            zombieVelocity[index] = velocity;
            return;
        }
        velocity.Value = zombieSpeed;
        zombieVelocity[index] = velocity;   
        
        var to = humanPosition[zombie.TargetIndex].Value.xz;
        var from = zombiePosition[index].Value.xz;
        var nextHeading = math.normalize(to - from);

        var heading = zombieHeading[index];
        heading.Value = nextHeading;
        zombieHeading[index] = heading;
    }
}