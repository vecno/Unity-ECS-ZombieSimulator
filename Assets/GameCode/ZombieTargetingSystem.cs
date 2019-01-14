using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;

[UpdateAfter(typeof(HumanNavigationSystem))]
class ZombieTargetingSystem : JobComponentSystem
{
    private ComponentGroup zombieDataGroup;
    private ComponentGroup zombieTargetGroup;
    
    protected override void OnStartRunning()
    {
        zombieDataGroup = GetComponentGroup(typeof(Zombie), typeof(Heading), typeof(Position));
        zombieTargetGroup = GetComponentGroup(typeof(Human), typeof(Heading), typeof(Position));
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var zombieData = zombieDataGroup.GetComponentDataArray<Zombie>();
        var zombiePosition = zombieDataGroup.GetComponentDataArray<Position>();
        
        var humanData = zombieTargetGroup.GetComponentDataArray<Human>();
        var humanPosition = zombieTargetGroup.GetComponentDataArray<Position>();

        var zombieTargetJob = new ZombieTargetingJob{
            zombieData = zombieData,
            humansData = humanData,
            humanPositions = humanPosition,
            zombiePositions = zombiePosition
        };

        // Note: Towards the end of the simulation this slows down.
        // It is feasible to swap the length source as humans die off.
        
        return zombieTargetJob.Schedule(
            zombieData.Length, 64, inputDeps
        );
    }
}

[BurstCompile]
public struct ZombieTargetingJob : IJobParallelFor
{
    public ComponentDataArray<Zombie> zombieData;
    
    [ReadOnly]
    public ComponentDataArray<Human> humansData;
    [ReadOnly]
    public ComponentDataArray<Position> humanPositions;
    [ReadOnly]
    public ComponentDataArray<Position> zombiePositions;

    public void Execute(int index)
    {
        var zombie = zombieData[index];
        
        if (zombie.BecomeActive != 1)
            return;

        // Note: Awkward part: What if human is destroyed and
        // the system shuffles the index values around, aka
        // we can not trust the index values as used here.
        
        if (zombie.TargetIndex != -1)
        {
            var human = humansData[zombie.TargetIndex];
            if (human.IsInfected != 1) return;
        }
        
        var idx = -1;
        var distance = float.MaxValue;
        var position = zombiePositions[index].Value.xz;
        for (var i = 0; i < humansData.Length; i++)
        {
            var human = humansData[i];
            if (human.IsInfected == 1)
                continue;

            var distSquared = math.distance(
                position, humanPositions[i].Value.xz
            );
            if (!(distSquared < distance)) continue;
            distance = distSquared;
            idx = i;
        }

        zombie.TargetIndex = idx;
        zombieData[index] = zombie;
    }
}
