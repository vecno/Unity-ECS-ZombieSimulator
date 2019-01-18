using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

class ZombieTargetingSystem : JobComponentSystem
{
    private ComponentGroup actorDataGroup;
    
    private NativeHashMap<int, int> indexMap;
    private NativeMultiHashMap<uint, int> spacialMap;
    
    protected override void OnStartRunning()
    {
        indexMap = new NativeHashMap<int, int>(
            ZombieSettings.Instance.HumanCount, Allocator.Persistent
        );
        spacialMap = new NativeMultiHashMap<uint, int>(
            ZombieSettings.Instance.HumanCount, Allocator.Persistent
        );
        actorDataGroup = GetComponentGroup(typeof(Actor), typeof(Target), typeof(Transform));
    }

    protected override void OnStopRunning()
    {
        indexMap.Dispose();
        spacialMap.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        // Note: this is the size of the hexes on spacial map, smaller
        // values lowers the load but means less likely to find a target.
        const float scale = 10.0f;

        var entities = actorDataGroup.GetEntityArray();
        
        var actors = actorDataGroup.GetComponentDataArray<Actor>();
        var targets = actorDataGroup.GetComponentDataArray<Target>();
        var transforms = actorDataGroup.GetComponentDataArray<Transform>();
        
        var computeIndexMap = new ComputeIndexMap{
            actors = actors,
            entities = entities,
            indexMap = indexMap.ToConcurrent()
        };
        var computeSpacialMap = new ComputeSpacialMap{
            scale = scale,
            actors = actors,
            layout = EntityUtil.Layout,
            transforms = transforms,
            spacialMap = spacialMap.ToConcurrent()
        };
        inputDeps = JobHandle.CombineDependencies(
            computeIndexMap.Schedule(actors.Length, 64, inputDeps),
            computeSpacialMap.Schedule(actors.Length, 64, inputDeps)
        );

        var runZombieTargeting = new RunZombieTargeting{
            scale = scale,
            actors = actors,
            layout = EntityUtil.Layout,
            targets = targets,
            entities = entities,
            indexMap = indexMap,
            spacialMap = spacialMap,
            transforms = transforms
        };
        inputDeps = runZombieTargeting.Schedule(
            actors.Length, 64, inputDeps
        );
        
        var clrIndexMap = new ClrIndexMap{
            indexMap = indexMap
        };
        var clrSpacialMap = new ClrSpacialMap{
            spacialMap = spacialMap
        };
        return JobHandle.CombineDependencies(
            clrIndexMap.Schedule(inputDeps),
            clrSpacialMap.Schedule(inputDeps)
        );
    }

    [BurstCompile]
    private struct ClrIndexMap : IJob
    {
        public NativeHashMap<int, int> indexMap;
        
        public void Execute()
        { indexMap.Clear(); }
    }
    
    private struct ClrSpacialMap : IJob
    {
        public NativeMultiHashMap<uint, int> spacialMap;
        
        public void Execute()
        { spacialMap.Clear(); }
    }
    
    [BurstCompile]
    private struct ComputeIndexMap : IJobParallelFor
    {
        [ReadOnly]
        public EntityArray entities;
        [ReadOnly]
        public ComponentDataArray<Actor> actors;
        
        public NativeHashMap<int, int>.Concurrent indexMap;
        
        public void Execute(int index)
        {
            if (Actor.Type.Human != actors[index].Value)
                return;

            var key = entities[index].GetHashCode();
            indexMap.TryAdd(key, index);
        }
    }

    [BurstCompile]
    private struct ComputeSpacialMap : IJobParallelFor
    {
        public float scale;
        public double3 layout;

        [ReadOnly]
        public ComponentDataArray<Actor> actors;
        [ReadOnly]
        public ComponentDataArray<Transform> transforms;
        
        public NativeMultiHashMap<uint, int>.Concurrent spacialMap;
        
        public void Execute(int index)
        {
            if (Actor.Type.Human != actors[index].Value)
                return;

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
        
        public ComponentDataArray<Target> targets;
        
        [ReadOnly]
        public EntityArray entities;
        
        [ReadOnly]
        public NativeHashMap<int, int> indexMap;
        [ReadOnly]
        public NativeMultiHashMap<uint, int> spacialMap;
        
        [ReadOnly]
        public ComponentDataArray<Actor> actors;
        [ReadOnly]
        public ComponentDataArray<Transform> transforms;
    
        public void Execute(int index)
        {
            if (Actor.Type.Zombie != actors[index].Value)
                return;

            var target = targets[index];
    
            // Note: Temp zombie disable
            if (target.Entity == -2)
                return;
            
            if (target.Entity != -1)
            {
                if (indexMap.TryGetValue(target.Entity, out var i))
                {
                    target.Position = transforms[i].Position;
                    targets[index] = target;
                    return;
                }
            }

            var t = transforms[index];
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
                targets[index] = target;
                return;
            }

            target.Entity = entities[currIndex].GetHashCode();
            target.Position = transforms[currIndex].Position;
            targets[index] = target;
        }

        private int FindTarget(uint key, int index, float2 position, float inDistance, out float outDistance)
        {
            if (!spacialMap.TryGetFirstValue(key, out var idx, out var itr))
            { outDistance = inDistance; return index; }

            var distSquared = math.distance(position, transforms[idx].Position);
            if (!(distSquared < inDistance)) { outDistance = inDistance; return index; }
            outDistance = distSquared;
            index = idx;
            
            while (spacialMap.TryGetNextValue(out idx, ref itr))
            {
                distSquared = math.distance(position, transforms[idx].Position);
                if (!(distSquared < outDistance)) continue;
                outDistance = distSquared;
                index = idx;
            }

            return index;
        }
    }
}
