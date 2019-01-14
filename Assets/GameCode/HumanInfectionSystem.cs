using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;

class HumanInfectionSystem : JobComponentSystem
{
    private ComponentGroup humanDataGroup;
    private ComponentGroup zombieDataGroup;

    protected override void OnStartRunning()
    {
        humanDataGroup = GetComponentGroup(typeof(Human), typeof(Position));
        zombieDataGroup = GetComponentGroup(typeof(Zombie), typeof(Position));
    }
    
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var humanData = humanDataGroup.GetComponentDataArray<Human>();
        var humanPosition = humanDataGroup.GetComponentDataArray<Position>();
        
        var zombieData = zombieDataGroup.GetComponentDataArray<Zombie>();
        var zombiePosition = zombieDataGroup.GetComponentDataArray<Position>();
        
        var job = new HumanInfectionJob
        {
            humanData = humanData,
            zombieData = zombieData,
            humanPositions = humanPosition,
            zombiePositions = zombiePosition,
            infectionDistance = ZombieSettings.Instance.InfectionDistance
        };

        return job.Schedule(zombieData.Length, humanData.Length, inputDeps);
    }
}

[BurstCompile]
public struct HumanInfectionJob : IJobParallelFor
{
    public float infectionDistance;
    
    public ComponentDataArray<Human> humanData;
    
    [ReadOnly]
    public ComponentDataArray<Zombie> zombieData;
    [ReadOnly]
    public ComponentDataArray<Position> humanPositions;
    [ReadOnly]
    public ComponentDataArray<Position> zombiePositions;

    public void Execute(int index)
    {
        var zombie = zombieData[index];
        
        if (zombie.TargetIndex == -1)
            return;
        if (zombie.BecomeActive != 1)
            return;

        var human = humanData[zombie.TargetIndex];
        if (human.IsInfected == 1) return;

        var distSquared = math.distance(
            zombiePositions[index].Value.xz,
            humanPositions[zombie.TargetIndex].Value.xz
        );

        if (infectionDistance < distSquared) 
            return;
        
        human.IsInfected = 1;
        humanData[zombie.TargetIndex] = human;
    }
}
