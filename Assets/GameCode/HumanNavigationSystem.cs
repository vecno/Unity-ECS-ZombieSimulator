using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;

class HumanNavigationSystem : JobComponentSystem
{
    private long headingSeed;

    private ComponentGroup actorDataGroup;

    protected override void OnStartRunning()
    {
        headingSeed = (long)(long.MaxValue * UnityEngine.Random.value);
        actorDataGroup = GetComponentGroup(typeof(Actor), typeof(Heading), typeof(Timeout), typeof(Velocity));
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var x = headingSeed;
        // Randomize the seed value every frame.
        x ^= x << 13; x ^= x >> 7; x ^= x << 17;
        headingSeed = x;

        var actors = actorDataGroup.GetComponentDataArray<Actor>();
        var timeouts = actorDataGroup.GetComponentDataArray<Timeout>();
        var headings = actorDataGroup.GetComponentDataArray<Heading>();
        var velocities = actorDataGroup.GetComponentDataArray<Velocity>();

        var navigationJob = new HumanNavigationJob{
            seed = headingSeed,
            speed = ZombieSimulatorBootstrap.Settings.HumanSpeed,
            deltaTime = Time.deltaTime,
            actors = actors,
            headings = headings,
            timeouts = timeouts,
            velocities = velocities
        };
        
        return navigationJob.Schedule(
            headings.Length, 64, inputDeps
        );
    }

    [BurstCompile]
    private struct HumanNavigationJob : IJobParallelFor
    {
        public long seed;
        public float speed;
        public float deltaTime;

        public ComponentDataArray<Heading> headings;
        public ComponentDataArray<Timeout> timeouts;
        public ComponentDataArray<Velocity> velocities;

        [ReadOnly]
        public ComponentDataArray<Actor> actors;

        public void Execute(int index)
        {
            if (Actor.Type.Human != actors[index].Value)
                return;

            var timeout = timeouts[index];

            timeout.Value -= deltaTime;
            if (timeout.Value > 0)
            { timeouts[index] = timeout; return; }

            var x = ((seed + index) << 15) + seed;
            // Randomize shared seed and normalize.
            x ^= x << 13; x ^= x >> 7; x ^= x << 17;
            var val = new double2((int)x, (int)(x >> 32));
            // The goal here is to get a rand vector with values from -1 to +1.
            var direction = math.normalize(((val + CMi) / CMui - 0.5) * 2.0);

            timeout.Value = 2.5f + (float)math.abs(7.5 * direction.x);
            timeouts[index] = timeout;

            var heading = headings[index];
            heading.Angle = (float)math.atan2(direction.x, direction.y);
            heading.Value = (float2)direction;
            headings[index] = heading;

            var velocity = velocities[index];
            velocity.Value = speed;
            velocities[index] = velocity;
        }

        private const double CMi = 2147483646.0;
        private const double CMui = 4294967293.0;
    }
}
