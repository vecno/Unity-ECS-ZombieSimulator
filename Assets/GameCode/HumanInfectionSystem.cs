using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

class HumanInfectionSystem : JobComponentSystem
{
    private ComponentGroup actorDataGroup;

    private NativeHashMap<int, int> indicesMap;
    private NativeHashMap<int, int> transformMap;

    protected override void OnStartRunning()
    {
        indicesMap = new NativeHashMap<int, int>(
            ZombieSettings.Instance.HumanCount, Allocator.Persistent
        );
        transformMap = new NativeHashMap<int, int>(
            ZombieSettings.Instance.HumanCount, Allocator.Persistent
        );

        actorDataGroup = GetComponentGroup(typeof(Actor), typeof(Target), typeof(Renderer), typeof(Transform));
    }

    protected override void OnStopRunning()
    {
        indicesMap.Dispose();
        transformMap.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var entities = actorDataGroup.GetEntityArray();
        var actors = actorDataGroup.GetComponentDataArray<Actor>();
        var targets = actorDataGroup.GetComponentDataArray<Target>();
        var renderers = actorDataGroup.GetComponentDataArray<Renderer>();
        var transforms = actorDataGroup.GetComponentDataArray<Transform>();

        var computeIndexMap = new ComputeIndexMap{
            actors = actors,
            entities = entities,
            indicesMap = indicesMap.ToConcurrent()
        };
        inputDeps = computeIndexMap.Schedule(
            entities.Length, 64, inputDeps
        );

        var infectionJob = new InfectionJob{
            actors = actors,
            targets = targets,
            transforms = transforms,
            indicesMap = indicesMap,
            transformMap = transformMap.ToConcurrent(),
            infectionDistance = ZombieSettings.Instance.InfectionDistance
        };
        inputDeps = infectionJob.Schedule(
            targets.Length, 64, inputDeps
        );

        var transformJob = new TransformJob{
            actors = actors,
            renderers = renderers,
            transformMap = transformMap
        };
        inputDeps = transformJob.Schedule(
            renderers.Length, 64, inputDeps
        );

        var clrIndexMap = new ClrIndexMap{
            indicesMap = indicesMap
        };
        var clrTransformMap = new ClrTransformMap{
            transformMap = transformMap,
        };
        return JobHandle.CombineDependencies(
            clrIndexMap.Schedule(inputDeps),
            clrTransformMap.Schedule(inputDeps)
        );
    }

    [BurstCompile]
    private struct ClrIndexMap : IJob
    {
        public NativeHashMap<int, int> indicesMap;

        public void Execute()
        { indicesMap.Clear(); }
    }

    [BurstCompile]
    private struct ClrTransformMap : IJob
    {
        public NativeHashMap<int, int> transformMap;

        public void Execute()
        { transformMap.Clear(); }
    }

    [BurstCompile]
    private struct ComputeIndexMap : IJobParallelFor
    {
        public NativeHashMap<int, int>.Concurrent indicesMap;

        [ReadOnly]
        public EntityArray entities;
        [ReadOnly]
        public ComponentDataArray<Actor> actors;

        public void Execute(int index)
        {
            if (Actor.Type.Human != actors[index].Value)
                return;

            var key = entities[index].GetHashCode();
            indicesMap.TryAdd(key, index);
        }
    }

    private struct TransformJob : IJobParallelFor
    {
        public ComponentDataArray<Actor> actors;
        public ComponentDataArray<Renderer> renderers;

        [ReadOnly]
        public NativeHashMap<int, int> transformMap;

        public void Execute(int index)
        {
            if (Actor.Type.Zombie == actors[index].Value)
                return;

            if (!transformMap.TryGetValue(index, out _))
                return;

            var actor = actors[index];
            actor.Value = Actor.Type.Zombie;
            actors[index] = actor;

            var renderer = renderers[index];
            renderer.Value.x = -.5f;
            renderer.Value.y =  .5f;
            renderers[index] = renderer;
        }
    }

    private struct InfectionJob : IJobParallelFor
    {
        public float infectionDistance;

        public ComponentDataArray<Actor> actors;
        public ComponentDataArray<Target> targets;

        public NativeHashMap<int, int>.Concurrent transformMap;

        [ReadOnly]
        public NativeHashMap<int, int> indicesMap;
        [ReadOnly]
        public ComponentDataArray<Transform> transforms;

        public void Execute(int index)
        {
            if (Actor.Type.Zombie != actors[index].Value)
                return;

            var target = targets[index];

            if (target.Entity == -1) 
                return;

            if (target.Entity == -2)
            {
                var actor = actors[index];
                actor.Value = Actor.Type.None;
                actors[index] = actor;
                return;
            }
    
            if (!indicesMap.TryGetValue(target.Entity, out var idx))
            {
                target.Entity = -1;
                targets[index] = target;
                return;
            }
    
            var distSquared = math.distance(
                transforms[idx].Position,
                transforms[index].Position
            );
            if (infectionDistance < distSquared) 
                return;

            target.Entity = -1;
            targets[index] = target;
            transformMap.TryAdd(idx, idx);
        }
    }
}
