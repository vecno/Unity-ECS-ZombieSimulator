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
    private NativeMultiHashMap<uint, int> humanSpacialMap;
    
    protected override void OnStartRunning()
    {
        humanIndexMap = new NativeHashMap<int, int>(
            ZombieSettings.Instance.HumanCount, Allocator.Persistent
        );
        humanSpacialMap = new NativeMultiHashMap<uint, int>(
            ZombieSettings.Instance.HumanCount, Allocator.Persistent
        );
        // Select all human and zombie positions of objects tagged as active
        humanDataGroup = GetComponentGroup(typeof(Human), typeof(Active), typeof(Transform));
        zombieDataGroup = GetComponentGroup(typeof(Zombie), typeof(Active), typeof(Target), typeof(Transform));
    }

    protected override void OnStopRunning()
    {
        humanIndexMap.Dispose();
        humanSpacialMap.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        // Note: this is the size of the hexes on spacial map, smaller
        // values lowers the load but means less likely to find a target.
        const float scale = 15.0f;
        
        var humanEntities = humanDataGroup.GetEntityArray();
        var humanTransforms = humanDataGroup.GetComponentDataArray<Transform>();
        var computeIndexMap = new ComputeIndexMap{
            humanEntities = humanEntities,
            humanIndexMap = humanIndexMap.ToConcurrent()
        };
        var computeSpacialMap = new ComputeSpacialMap{
            scale = scale,
            layout = EntityUtil.Layout,
            transforms = humanTransforms,
            spacialMap = humanSpacialMap.ToConcurrent()
        };
        inputDeps = JobHandle.CombineDependencies(
            computeIndexMap.Schedule(humanEntities.Length, 64, inputDeps),
            computeSpacialMap.Schedule(humanTransforms.Length, 64, inputDeps)
        );
      
        var zombieTargets = zombieDataGroup.GetComponentDataArray<Target>();
        var zombieTransforms = zombieDataGroup.GetComponentDataArray<Transform>();
        var runZombieTargeting = new RunZombieTargeting{
            scale = scale,
            layout = EntityUtil.Layout,
            zombieTargets = zombieTargets,
            humanEntities = humanEntities,
            humanIndexMap = humanIndexMap,
            humanSpacialMap = humanSpacialMap,
            humanTransforms = humanTransforms,
            zombieTransforms = zombieTransforms
        };
        inputDeps = runZombieTargeting.Schedule(
            zombieTargets.Length, 64, inputDeps
        );
        
        var clrIndexMap = new ClrIndexMap{
            humanIndexMap = humanIndexMap
        };
        var clrSpacialMap = new ClrSpacialMap{
            humanSpacialMap = humanSpacialMap
        };
        return JobHandle.CombineDependencies(
            clrIndexMap.Schedule(inputDeps),
            clrSpacialMap.Schedule(inputDeps)
        );
    }

    [BurstCompile]
    private struct ClrIndexMap : IJob
    {
        public NativeHashMap<int, int> humanIndexMap;
        
        public void Execute()
        { humanIndexMap.Clear(); }
    }
    
    private struct ClrSpacialMap : IJob
    {
        public NativeMultiHashMap<uint, int> humanSpacialMap;
        
        public void Execute()
        { humanSpacialMap.Clear(); }
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
    private struct ComputeSpacialMap : IJobParallelFor
    {
        public float scale;
        public double3 layout;
        
        [ReadOnly]
        public ComponentDataArray<Transform> transforms;
        
        public NativeMultiHashMap<uint, int>.Concurrent spacialMap;
        
        public void Execute(int index)
        {
            var t = transforms[index];
            var p = (double2)t.Position / scale;
            var q = (layout.x * p.x + p.y) * 2.0;
            var r = (layout.y * p.x + layout.z * p.y) * 2.0;
            var m = (int2) math.round(new double2(q, r));
            spacialMap.Add(math.hash(m), index);
        }
    }

    [BurstCompile]
    private struct RunZombieTargeting : IJobParallelFor
    {
        public float scale;
        public double3 layout;
        
        public ComponentDataArray<Target> zombieTargets;
        
        [ReadOnly]
        public EntityArray humanEntities;
        [ReadOnly]
        public NativeHashMap<int, int> humanIndexMap;
        [ReadOnly]
        public NativeMultiHashMap<uint, int> humanSpacialMap;
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
            
            var t = zombieTransforms[index];
            var s = (double2)t.Position / scale;
            var q = (layout.x * s.x + s.y) * 2.0;
            var r = (layout.y * s.x + layout.z * s.y) * 2.0;
            var k = (int2)math.round(new double2(q, r));
            
            // Note: Check the tile the zombie is on and 'All' tiles around it.
            // The zombie might be on the edge of a tile and it should pick the
            // closest target to it self. 'It' should not be running off to the other
            // side of the tile when there is a human right next to it across the border.
            
            var currIndex = -1;
            var currDistance = float.MaxValue;
            currIndex = FindTarget(
                math.hash(k), currIndex, 
                t.Position, currDistance, out currDistance
            );
            currIndex = FindTarget(
                math.hash(new int2(k.x, k.y-1)), currIndex, 
                t.Position, currDistance, out currDistance
            );
            currIndex = FindTarget(
                math.hash(new int2(k.x, k.y+1)), currIndex, 
                t.Position, currDistance, out currDistance
            );
            currIndex = FindTarget(
                math.hash(new int2(k.x+1, k.y)), currIndex, 
                t.Position, currDistance, out currDistance
            );
            currIndex = FindTarget(
                math.hash(new int2(k.x-1, k.y)), currIndex, 
                t.Position, currDistance, out currDistance
            );
            currIndex = FindTarget(
                math.hash(new int2(k.x+1, k.y+1)), currIndex, 
                t.Position, currDistance, out currDistance
            );
            currIndex = FindTarget(
                math.hash(new int2(k.x-1, k.y-1)), currIndex, 
                t.Position, currDistance, out currDistance
            );
            
            if (-1 == currIndex)
            {
                // Note: Temp zombie disable,
                // works because hash is >= 0.
                target.Entity = -2;
                zombieTargets[index] = target;
                return;
            }

            target.Entity = humanEntities[currIndex].GetHashCode();
            target.Position = humanTransforms[currIndex].Position;
            zombieTargets[index] = target;
        }

        private int FindTarget(uint key, int index, float2 position, float inDistance, out float outDistance)
        {
            if (!humanSpacialMap.TryGetFirstValue(key, out var idx, out var itr))
            { outDistance = inDistance; return index; }
                
            
            var distSquared = math.distance(position, humanTransforms[idx].Position);
            if (!(distSquared < inDistance)) { outDistance = inDistance; return index; }
            outDistance = distSquared;
            index = idx;
            
            while (humanSpacialMap.TryGetNextValue(out idx, ref itr))
            {
                distSquared = math.distance(position, humanTransforms[idx].Position);
                if (!(distSquared < outDistance)) continue;
                outDistance = distSquared;
                index = idx;
            }

            return index;
        }
    }
}
