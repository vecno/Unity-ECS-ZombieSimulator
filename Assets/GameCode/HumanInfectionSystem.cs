using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

class HumanInfectionSystem : ComponentSystem
{
    private ComponentGroup humanDataGroup;
    private ComponentGroup zombieDataGroup;

    private NativeHashMap<int, int> humanIndexMap;
    private NativeHashMap<int, int> humanTransformMap;

    protected override void OnStartRunning()
    {
        humanIndexMap = new NativeHashMap<int, int>(
            ZombieSettings.Instance.HumanCount, Allocator.Persistent
        );
        humanTransformMap = new NativeHashMap<int, int>(
            ZombieSettings.Instance.HumanCount, Allocator.Persistent
        );
        
        // Select all human and zombie positions of objects tagged as active
        humanDataGroup = GetComponentGroup(typeof(Human), typeof(Active), typeof(Renderer), typeof(Transform));
        zombieDataGroup = GetComponentGroup(typeof(Zombie), typeof(Active), typeof(Target), typeof(Transform));
    }
    
    protected override void OnStopRunning()
    {
        humanIndexMap.Dispose();
        humanTransformMap.Dispose();
    }
    
    protected override void OnUpdate()
    {
        // ToDo Remove the remaining dependencies on command buffer.
        // Try to remove all sync points from the systems, smooth frames.
        
        var humanEntities = humanDataGroup.GetEntityArray();
        var humanRenderers = humanDataGroup.GetComponentDataArray<Renderer>();
        var humanTransforms = humanDataGroup.GetComponentDataArray<Transform>();
        var computeIndexMap = new ComputeIndexMap{
            humanEntities = humanEntities,
            humanIndexMap = humanIndexMap.ToConcurrent()
        };
        var inputDeps = computeIndexMap.Schedule(
            humanEntities.Length, 64
        );
        
        var zombieEntities = zombieDataGroup.GetEntityArray();
        var zombieTargets = zombieDataGroup.GetComponentDataArray<Target>();
        var zombieTransforms = zombieDataGroup.GetComponentDataArray<Transform>();
        var humanInfectionJob = new HumanInfectionJob{
            zombieTargets = zombieTargets,
            humanEntities = humanEntities,
            zombieEntities = zombieEntities,
            
            humanTransformMap = humanTransformMap.ToConcurrent(),
            humanIndexMap = humanIndexMap,
            humanTransforms = humanTransforms,
            zombieTransforms = zombieTransforms,
            commandBuffer = PostUpdateCommands.ToConcurrent(),
            infectionDistance = ZombieSettings.Instance.InfectionDistance
        };
        inputDeps = humanInfectionJob.Schedule(
            zombieTargets.Length, 64, inputDeps
        );
        
        var humanTransformJob = new HumanTransformJob{
            humanTransformMap = humanTransformMap,
            humanRenderers = humanRenderers,
        };
        inputDeps = humanTransformJob.Schedule(
            humanRenderers.Length, 64, inputDeps
        );
        
        var clrIndexMap = new ClrIndexMap{
            humanIndexMap = humanIndexMap
        };
        var clrTransformMap = new ClrTransformMap{
            humanTransformMap = humanTransformMap,
        };
        inputDeps = JobHandle.CombineDependencies(
            clrIndexMap.Schedule(inputDeps),
            clrTransformMap.Schedule(inputDeps)
        );
        
        inputDeps.Complete();
    }
    
    [BurstCompile]
    private struct ClrIndexMap : IJob
    {
        public NativeHashMap<int, int> humanIndexMap;

        public void Execute()
        { humanIndexMap.Clear(); }
    }
    
    [BurstCompile]
    private struct ClrTransformMap : IJob
    {
        public NativeHashMap<int, int> humanTransformMap;

        public void Execute()
        { humanTransformMap.Clear(); }
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

    private struct HumanTransformJob : IJobParallelFor
    {
        public ComponentDataArray<Renderer> humanRenderers;
     
        [ReadOnly]
        public NativeHashMap<int, int> humanTransformMap;
        
        public void Execute(int index)
        {
            if (!humanTransformMap.TryGetValue(index, out _))
                return;

            var renderer = humanRenderers[index];
            renderer.Value.x = -.5f;
            renderer.Value.y =  .5f;
            humanRenderers[index] = renderer;
        }
    }

    private struct HumanInfectionJob : IJobParallelFor
    {
        public float infectionDistance;
        
        public ComponentDataArray<Target> zombieTargets;
        
        public EntityCommandBuffer.Concurrent commandBuffer;
        
        public NativeHashMap<int, int>.Concurrent humanTransformMap;
        
        [ReadOnly]
        public EntityArray humanEntities;
        [ReadOnly]
        public EntityArray zombieEntities;
        [ReadOnly]
        public NativeHashMap<int, int> humanIndexMap;
        [ReadOnly]
        public ComponentDataArray<Transform> humanTransforms;
        [ReadOnly]
        public ComponentDataArray<Transform> zombieTransforms;
    
        public void Execute(int index)
        {
            var target = zombieTargets[index];
            
            if (target.Entity == -1) 
                return;
            if (target.Entity == -2)
            {
                var zombie = zombieEntities[index];
                commandBuffer.RemoveComponent<Active>(index, zombie);
                commandBuffer.RemoveComponent<Heading>(index, zombie);
                return;
            }
    
            if (!humanIndexMap.TryGetValue(target.Entity, out var idx))
            {
                target.Entity = -1;
                zombieTargets[index] = target;
                return;
            }
    
            var distSquared = math.distance(
                humanTransforms[idx].Position,
                zombieTransforms[index].Position
            );
            if (infectionDistance < distSquared) 
                return;
    
            // Note: Bit of a hack, just need
            // to make sure it only happens once.
            // ToDo: Use a native array, fast check?
            if (humanTransformMap.TryAdd(idx, idx))
            {
                var human = humanEntities[idx];
                commandBuffer.RemoveComponent<Human>(index, human);
                commandBuffer.AddComponent(index, human, new Zombie());
            }

            target.Entity = -1;
            zombieTargets[index] = target;
        }
    }
}
