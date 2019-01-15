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

[UpdateAfter(typeof(HumanNavigationSystem))]
[UpdateAfter(typeof(ZombieNavigationSystem))]
public class EntityMovementSystem : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var movementJob = new MovementJob{dt = Time.deltaTime};
        return movementJob.Schedule(this, inputDeps);
    }

    [BurstCompile]
    private struct MovementJob : IJobProcessComponentData<Heading, Velocity, Position, Rotation>
    {
        public float dt;

        public void Execute([ReadOnly] ref Heading heading, [ReadOnly] ref Velocity velocity, ref Position position, ref Rotation rotation)
        {
            // Note: Keep sin/cos here to mimic extra cpu load. In
            // practice they are cached and smooth rotation is here.
            rotation.Value.value.y = math.sin(heading.Angle * .5f);
            rotation.Value.value.w = math.cos(heading.Angle * .5f);
            
            position.Value.x += heading.Value.x * velocity.Value * dt;
            position.Value.z += heading.Value.y * velocity.Value * dt;
        }
    }
}