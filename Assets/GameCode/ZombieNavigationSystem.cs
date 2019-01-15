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
    protected override void OnStartRunning()
    {
        zombieDataGroup = GetComponentGroup(typeof(Zombie), typeof(Target), typeof(Heading), typeof(Velocity), typeof(Position));
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var zombieTargets = zombieDataGroup.GetComponentDataArray<Target>();
        var zombieHeadings = zombieDataGroup.GetComponentDataArray<Heading>();
        var zombiePositions = zombieDataGroup.GetComponentDataArray<Position>();
        var zombieVelocities = zombieDataGroup.GetComponentDataArray<Velocity>();

        var navigationJob = new ZombieNavigationJob{
            zombieSpeed = ZombieSettings.Instance.ZombieSpeed,
            zombieTargets = zombieTargets,
            zombieHeadings = zombieHeadings,
            zombiePositions = zombiePositions,
            zombieVelocities = zombieVelocities
        };

        return navigationJob.Schedule(
            zombieTargets.Length, 64, inputDeps
        );
    }
}

[BurstCompile]
public struct ZombieNavigationJob : IJobParallelFor
{
    public float zombieSpeed;
    
    public ComponentDataArray<Heading> zombieHeadings;
    public ComponentDataArray<Velocity> zombieVelocities;
 
    [ReadOnly]
    public ComponentDataArray<Target> zombieTargets;
    [ReadOnly]
    public ComponentDataArray<Position> zombiePositions;

    public void Execute(int index)
    {
        var target = zombieTargets[index];
        var velocity = zombieVelocities[index];
        
        if (target.entity < -0)
        {
            velocity.Value = 0;
            zombieVelocities[index] = velocity;
            return;
        }
        
        velocity.Value = zombieSpeed;
        zombieVelocities[index] = velocity;   
        
        var from = zombiePositions[index].Value.xz;
        var direction = math.normalize(target.position - from);

        var heading = zombieHeadings[index];
        heading.Angle = math.atan2(direction.x, direction.y);
        heading.Value = direction;
        zombieHeadings[index] = heading;
    }
}
