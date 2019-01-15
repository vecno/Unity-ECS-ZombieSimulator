using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;

class HumanInfectionSystem : ComponentSystem
{    
    private ComponentGroup humanDataGroup;
    private ComponentGroup zombieDataGroup;
    
    private NativeHashMap<int, int> toZombieMap;
    private NativeHashMap<int, int> humanIndexMap;

    protected override void OnStartRunning()
    {
        toZombieMap = new NativeHashMap<int, int>(
            ZombieSettings.Instance.HumanCount, Allocator.Persistent
        );
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
        toZombieMap.Dispose();
    }
    
    protected override void OnUpdate()
    {
        // ToDo Figure out a way to reduce the load on the command buffer.
        // It is a sync point that is being fed allot of data in this setup.
        
        var humanEntities = humanDataGroup.GetEntityArray();
        var humanPositions = humanDataGroup.GetComponentDataArray<Position>();
        
        var zombieEntities = zombieDataGroup.GetEntityArray();
        var zombieTargets = zombieDataGroup.GetComponentDataArray<Target>();
        var zombiePositions = zombieDataGroup.GetComponentDataArray<Position>();
     
        var mapClearJob = new MapClearJob{
            toZombieMap = toZombieMap,
            humanIndexMap = humanIndexMap
        };
        var mapEntitiesJob = new MapEntitiesJob{
            humanEntities = humanEntities,
            humanIndexMap = humanIndexMap.ToConcurrent()
        };
        var humanInfectionJob = new HumanInfectionJob
        {
            zombieTargets = zombieTargets,
            humanEntities = humanEntities,
            zombieEntities = zombieEntities,
            
            toZombieMap = toZombieMap.ToConcurrent(),
            humanIndexMap = humanIndexMap,
            humanPositions = humanPositions,
            zombiePositions = zombiePositions,
            commandBuffer = PostUpdateCommands.ToConcurrent(),
            infectionDistance = ZombieSettings.Instance.InfectionDistance
        };

        var jobHandle = mapClearJob.Schedule();
        jobHandle = mapEntitiesJob.Schedule(
            humanEntities.Length, 64, jobHandle
        );
        jobHandle = humanInfectionJob.Schedule(
            zombieTargets.Length, 64, jobHandle
        );

        // Nothing to do here?
        jobHandle.Complete();
    }
    
    [BurstCompile]
    private struct MapClearJob : IJob
    {
        public NativeHashMap<int, int> toZombieMap;
        public NativeHashMap<int, int> humanIndexMap;
    
        public void Execute()
        {
            // ToDo This feels wrong, but when doing this on main it takes +1.0ms
            // It also needs jobs schedule around it, as it executed now it is a gap.
            toZombieMap.Clear();
            humanIndexMap.Clear();
        }
    }
}

public struct HumanInfectionJob : IJobParallelFor
{
    public float infectionDistance;
    
    public ComponentDataArray<Target> zombieTargets;
    
    public EntityCommandBuffer.Concurrent commandBuffer;
    
    public NativeHashMap<int, int>.Concurrent toZombieMap;
    
    [ReadOnly]
    public EntityArray humanEntities;
    [ReadOnly]
    public EntityArray zombieEntities;
    [ReadOnly]
    public NativeHashMap<int, int> humanIndexMap;
    [ReadOnly]
    public ComponentDataArray<Position> humanPositions;
    [ReadOnly]
    public ComponentDataArray<Position> zombiePositions;

    public void Execute(int index)
    {
        var target = zombieTargets[index];
        if (target.entity == -1) return;
        
        var zombie = zombieEntities[index];
        if (target.entity == -2)
        {
            commandBuffer.RemoveComponent<Active>(index, zombie);
            commandBuffer.RemoveComponent<Heading>(index, zombie);
            return;
        }

        if (!humanIndexMap.TryGetValue(target.entity, out var idx))
        {
            target.entity = -1;
            zombieTargets[index] = target;
            return;
        }

        var distSquared = math.distance(
            zombiePositions[index].Value.xz,
            humanPositions[idx].Value.xz
        );
        if (infectionDistance < distSquared) 
            return;

        // Note: Bit of a hack, just need
        // to make sure it only happens once.
        // ToDo: Use a native array, fast check
        if (toZombieMap.TryAdd(idx, idx))
        {
            var human = humanEntities[idx];
            commandBuffer.RemoveComponent<Human>(index, human);
            commandBuffer.AddComponent(index, human, new Zombie());
            // Note: The Infected component updates the visuals next frame;
            // as it stands it seems this needs to happen on the main thread.
            commandBuffer.AddComponent<Infected>(index, human, new Infected());
        }

        target.entity = -1;
        zombieTargets[index] = target;
    }
}
