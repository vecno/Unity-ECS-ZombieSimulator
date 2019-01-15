using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;

[UpdateAfter(typeof(HumanInfectionSystem))]
class ZombieTargetingSystem : JobComponentSystem
{
    private ComponentGroup humanDataGroup;
    private ComponentGroup zombieDataGroup;
    
    private NativeHashMap<int, int> humanIndexMap;

    protected override void OnStartRunning()
    {
        humanIndexMap = new NativeHashMap<int, int>(
            ZombieSettings.Instance.HumanCount, Allocator.Persistent
        );
        
        // Select all human and zombie positions of objects tagged as active
        humanDataGroup = GetComponentGroup(typeof(Human), typeof(Active), typeof(Position));
        zombieDataGroup = GetComponentGroup(typeof(Zombie), typeof(Active), typeof(Target), typeof(Position));
    }

    protected override void OnStopRunning()
    {
        humanIndexMap.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var humanEntities = humanDataGroup.GetEntityArray();
        var humanPositions = humanDataGroup.GetComponentDataArray<Position>();
        
        var zombieTargets = zombieDataGroup.GetComponentDataArray<Target>();
        var zombiePositions = zombieDataGroup.GetComponentDataArray<Position>();

        var mapClearJob = new MapClearJob{
            humanIndexMap = humanIndexMap
        };
        var mapEntitiesJob = new MapEntitiesJob{
            humanEntities = humanEntities,
            humanIndexMap = humanIndexMap.ToConcurrent()
        };
        var zombieTargetJob = new ZombieTargetingJob{
            zombieTargets = zombieTargets,
            humanEntities = humanEntities,
            humanIndexMap = humanIndexMap,
            humanPositions = humanPositions,
            zombiePositions = zombiePositions
        };
        
        inputDeps = JobHandle.CombineDependencies(
            inputDeps, mapClearJob.Schedule()
        );
        inputDeps = mapEntitiesJob.Schedule(
            humanEntities.Length, 64, inputDeps
        );
        return zombieTargetJob.Schedule(
            zombieTargets.Length, 64, inputDeps
        );
    }
    
    [BurstCompile]
    private struct MapClearJob : IJob
    {
        public NativeHashMap<int, int> humanIndexMap;
    
        public void Execute()
        {
            // ToDo This feels wrong, but when doing this on main it takes 1.0ms
            // It also needs jobs schedule around it, as it executed now it is a gap.
            humanIndexMap.Clear();
        }
    }
}

[BurstCompile]
public struct MapEntitiesJob : IJobParallelFor
{
    [ReadOnly]
    public EntityArray humanEntities;
    
    public NativeHashMap<int, int>.Concurrent humanIndexMap;
    
    public void Execute(int index)
    {
        var key = humanEntities[index].GetHashCode();
        humanIndexMap.TryAdd(key, index);
    }
}

[BurstCompile]
public struct ZombieTargetingJob : IJobParallelFor
{
    public ComponentDataArray<Target> zombieTargets;
    
    [ReadOnly]
    public EntityArray humanEntities;
    [ReadOnly]
    public NativeHashMap<int, int> humanIndexMap;
    [ReadOnly]
    public ComponentDataArray<Position> humanPositions;
    [ReadOnly]
    public ComponentDataArray<Position> zombiePositions;

    public void Execute(int index)
    {
        var target = zombieTargets[index];

        // Note: Temp zombie disable
        if (target.entity == -2)
            return;
        
        if (target.entity != -1)
        {
            if (humanIndexMap.TryGetValue(target.entity, out var i))
            {
                target.position = humanPositions[i].Value.xz;
                zombieTargets[index] = target;
                return;
            }
        }
        
        // Note: Add some spatial query system;
        // a simple Spatial Map, a QuadTree ...
        
        var idx = -1;
        var distance = float.MaxValue;
        var position = zombiePositions[index].Value.xz;
        for (var i = 0; i < humanPositions.Length; i++)
        {
            var distSquared = math.distance(
                position, humanPositions[i].Value.xz
            );
            if (!(distSquared < distance)) continue;
            distance = distSquared;
            idx = i;
        }

        if (-1 == idx || 15 < distance)
        {
            // Note: Temp zombie disable,
            // works because hash is >= 0.
            target.entity = -2;
            zombieTargets[index] = target;
            return;
        }

        target.entity = humanEntities[idx].GetHashCode();
        target.position = humanPositions[idx].Value.xz;
        zombieTargets[index] = target;
    }
}
