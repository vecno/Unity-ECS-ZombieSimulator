using Unity.Jobs;
using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

// Note: This System handles position and rotation of all entities,
// it is wise to keep it as close to the end of the stack as possible.
// Reason: The unity render system runs after all custom systems are
// updated and the first thing it does is update the entity transforms.
// This makes sure that all rotation and position data is in cpu cache.

//[UpdateAfter(typeof(HumanNavigationSystem))]
//[UpdateAfter(typeof(ZombieTargetingSystem))]
public class EntityMovementSystem : JobComponentSystem
{
    private ComponentGroup baseDataGroup;
    private ComponentGroup humanDataGroup;
    private ComponentGroup zombieDataGroup;

    private NativeHashMap<int, MapData> transformMap;

    protected override void OnStartRunning()
    {
        transformMap = new NativeHashMap<int, MapData>(
            ZombieSimulatorBootstrap.Settings.EntityCount, Allocator.Persistent
        );
        baseDataGroup = GetComponentGroup(typeof(Active), typeof(Heading), typeof(Velocity), typeof(Renderer), typeof(Transform));
        humanDataGroup = GetComponentGroup(typeof(Human), typeof(Active), typeof(Owner), typeof(Position), typeof(Rotation));
        zombieDataGroup = GetComponentGroup(typeof(Zombie), typeof(Active), typeof(Owner), typeof(Position), typeof(Rotation));
    }

    protected override void OnStopRunning()
    {
        transformMap.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var keys = baseDataGroup.GetEntityArray();
        var headings = baseDataGroup.GetComponentDataArray<Heading>();
        var renderers = baseDataGroup.GetComponentDataArray<Renderer>();
        var velocities = baseDataGroup.GetComponentDataArray<Velocity>();
        var transforms = baseDataGroup.GetComponentDataArray<Transform>();
        var computeTransformMap = new ComputeTransformMap{
            dt = Time.deltaTime,
            keys = keys,
            headings = headings,
            renderers = renderers,
            velocities = velocities,
            transforms = transforms,
            transformMap = transformMap.ToConcurrent()
        };
        inputDeps = computeTransformMap.Schedule(
            keys.Length, 64, inputDeps
        );
        
        var owners = humanDataGroup.GetComponentDataArray<Owner>();
        var positions = humanDataGroup.GetComponentDataArray<Position>();
        var rotations = humanDataGroup.GetComponentDataArray<Rotation>();
        var copyHumanTransformMap = new CopyHumanTransformMap{
            owners = owners,
            positions = positions,
            rotations = rotations,
            transformMap = transformMap
        };
        inputDeps = copyHumanTransformMap.Schedule(
            owners.Length, 64, inputDeps
        );
        owners = zombieDataGroup.GetComponentDataArray<Owner>();
        positions = zombieDataGroup.GetComponentDataArray<Position>();
        rotations = zombieDataGroup.GetComponentDataArray<Rotation>();
        var copyZombieTransformMap = new CopyZombieTransformMap{
            owners = owners,
            positions = positions,
            rotations = rotations,
            transformMap = transformMap
        };
        inputDeps = copyZombieTransformMap.Schedule(
            owners.Length, 64, inputDeps
        );

        var clrTransformMap = new ClrTransformMap{
            transformMap = transformMap
        };
        return clrTransformMap.Schedule(
            inputDeps
        );
    }

    private struct MapData
    {
        public float2 Renderer;
        public float2 Position;
        public float2 Rotation;
    }
    
    [BurstCompile]
    private struct ClrTransformMap : IJob
    {
        public NativeHashMap<int, MapData> transformMap;
        
        public void Execute()
        {
            transformMap.Clear();
        }
    }

    [BurstCompile]
    private struct CopyHumanTransformMap : IJobParallelFor
    {
        public ComponentDataArray<Position> positions;
        public ComponentDataArray<Rotation> rotations;

        [ReadOnly]
        public ComponentDataArray<Owner> owners;
        [ReadOnly]
        public NativeHashMap<int, MapData> transformMap;

        public void Execute(int index)
        {
            var key = owners[index].Entity.GetHashCode();
            if (!transformMap.TryGetValue(key, out var data))
                return;

            var position = positions[index];
            position.Value.x = data.Position.x;
            position.Value.y = data.Renderer.x;
            position.Value.z = data.Position.y;
            positions[index] = position;

            var rotation = rotations[index];
            rotation.Value.value.y = data.Rotation.x;
            rotation.Value.value.w = data.Rotation.y;
            rotations[index] = rotation;
        }
    }
    
    [BurstCompile]
    private struct CopyZombieTransformMap : IJobParallelFor
    {
        public ComponentDataArray<Position> positions;
        public ComponentDataArray<Rotation> rotations;

        [ReadOnly]
        public ComponentDataArray<Owner> owners;
        [ReadOnly]
        public NativeHashMap<int, MapData> transformMap;

        public void Execute(int index)
        {
            var key = owners[index].Entity.GetHashCode();
            if (!transformMap.TryGetValue(key, out var data))
                return;

            var position = positions[index];
            position.Value.x = data.Position.x;
            position.Value.y = data.Renderer.y;
            position.Value.z = data.Position.y;
            positions[index] = position;

            var rotation = rotations[index];
            rotation.Value.value.y = data.Rotation.x;
            rotation.Value.value.w = data.Rotation.y;
            rotations[index] = rotation;
        }
    }

    [BurstCompile]
    private struct ComputeTransformMap : IJobParallelFor
    {
        public float dt;
        
        [ReadOnly]
        public EntityArray keys;
        [ReadOnly]
        public ComponentDataArray<Heading> headings;
        [ReadOnly]
        public ComponentDataArray<Renderer> renderers;
        [ReadOnly]
        public ComponentDataArray<Velocity> velocities;
        
        public ComponentDataArray<Transform> transforms;
            
        public NativeHashMap<int, MapData>.Concurrent transformMap;
        
        public void Execute(int index)
        {
            var heading = headings[index];
            var velocity = velocities[index];
            
            var transform = transforms[index];
            transform.Position.x += heading.Value.x * velocity.Value * dt;
            transform.Position.y += heading.Value.y * velocity.Value * dt;
            transform.Rotation.x = math.sin(heading.Angle * .5f);
            transform.Rotation.y = math.cos(heading.Angle * .5f);
            transforms[index] = transform;
            
            transformMap.TryAdd(keys[index].GetHashCode(), new MapData{
                Renderer = renderers[index].Value,
                Position = transform.Position,
                Rotation = transform.Rotation
            });
        }
    }
}
