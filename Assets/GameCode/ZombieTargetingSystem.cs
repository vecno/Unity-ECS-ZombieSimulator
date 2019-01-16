using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

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
        humanDataGroup = GetComponentGroup(typeof(Human), typeof(Active), typeof(Transform));
        zombieDataGroup = GetComponentGroup(typeof(Zombie), typeof(Active), typeof(Target), typeof(Transform));
    }

    protected override void OnStopRunning()
    {
        humanIndexMap.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var humanEntities = humanDataGroup.GetEntityArray();
        var computeIndexMap = new ComputeIndexMap{
            humanEntities = humanEntities,
            humanIndexMap = humanIndexMap.ToConcurrent()
        };
        inputDeps = computeIndexMap.Schedule(
            humanEntities.Length, 64, inputDeps
        );

                
        var zombieTargets = zombieDataGroup.GetComponentDataArray<Target>();
        var humanTransforms = humanDataGroup.GetComponentDataArray<Transform>();
        var zombieTransforms = zombieDataGroup.GetComponentDataArray<Transform>();
        var runZombieTargeting = new RunZombieTargeting{
            zombieTargets = zombieTargets,
            humanEntities = humanEntities,
            humanIndexMap = humanIndexMap,
            humanTransforms = humanTransforms,
            zombieTransforms = zombieTransforms
        };
        inputDeps = runZombieTargeting.Schedule(
            zombieTargets.Length, 64, inputDeps
        );
        
        var clrIndexMap = new ClrIndexMap{
            humanIndexMap = humanIndexMap
        };
        return clrIndexMap.Schedule(
            inputDeps
        );
    }
    
    [BurstCompile]
    private struct ClrIndexMap : IJob
    {
        public NativeHashMap<int, int> humanIndexMap;
        
        public void Execute()
        {
            humanIndexMap.Clear();
        }
    }
    
    [BurstCompile]
    private struct ComputeIndexMap : IJobParallelFor
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
    private struct RunZombieTargeting : IJobParallelFor
    {
        public ComponentDataArray<Target> zombieTargets;
        
        [ReadOnly]
        public EntityArray humanEntities;
        [ReadOnly]
        public NativeHashMap<int, int> humanIndexMap;
        [ReadOnly]
        public ComponentDataArray<Transform> humanTransforms;
        [ReadOnly]
        public ComponentDataArray<Transform> zombieTransforms;
    
        public void Execute(int index)
        {
            var target = zombieTargets[index];
    
            // Note: Temp zombie disable
            if (target.Entity == -2)
                return;
            
            if (target.Entity != -1)
            {
                if (humanIndexMap.TryGetValue(target.Entity, out var i))
                {
                    target.Position = humanTransforms[i].Position;
                    zombieTargets[index] = target;
                    return;
                }
            }
            
            // Note: Add some spatial query system;
            // a simple Spatial Map, a QuadTree ...
            
            var idx = -1;
            var distance = float.MaxValue;
            var position = zombieTransforms[index].Position;
            for (var i = 0; i < humanTransforms.Length; i++)
            {
                var distSquared = math.distance(
                    position, humanTransforms[i].Position
                );
                if (!(distSquared < distance)) continue;
                distance = distSquared;
                idx = i;
            }
    
            if (-1 == idx || 15 < distance)
            {
                // Note: Temp zombie disable,
                // works because hash is >= 0.
                target.Entity = -2;
                zombieTargets[index] = target;
                return;
            }
    
            target.Entity = humanEntities[idx].GetHashCode();
            target.Position = humanTransforms[idx].Position;
            zombieTargets[index] = target;
        }
    }
}
