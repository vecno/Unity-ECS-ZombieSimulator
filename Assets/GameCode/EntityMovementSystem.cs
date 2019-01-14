using Unity.Jobs;
using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateAfter(typeof(HumanNavigationSystem))]
public class EntityMovementSystem : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var headingJob = new HeadingJob();
        var movementJob = new MovementJob{dt = Time.deltaTime};
        var headingHandle = headingJob.Schedule(this, inputDeps);
        return movementJob.Schedule(this, headingHandle);
    }

    [BurstCompile]
    private struct HeadingJob : IJobProcessComponentData<Heading, Position, Rotation>
    {
        public void Execute([ReadOnly] ref Heading heading, [ReadOnly] ref Position position, ref Rotation rotation)
        {
            // Snap the rotation of the object to face the heading direction.
            var dir = (heading.Value + position.Value.xz) - position.Value.xz;
            rotation.Value = Quaternion.AngleAxis(math.atan2(dir.y, dir.x) * Mathf.Rad2Deg, Vector3.up);
        }
    }
    
    [BurstCompile]
    private struct MovementJob : IJobProcessComponentData<Heading, Velocity, Position>
    {
        public float dt;

        public void Execute([ReadOnly] ref Heading heading, [ReadOnly] ref Velocity velocity, ref Position position)
        {
            position.Value.x += heading.Value.x * velocity.Value * dt;
            position.Value.z += heading.Value.y * velocity.Value * dt;
        }
    }
}