using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;

class ActorNavigationSystem : JobComponentSystem
{
    private long headingSeed;

    private ComponentGroup actorDataGroup;

    protected override void OnStartRunning()
    {
        headingSeed = (long)(long.MaxValue * UnityEngine.Random.value);
        actorDataGroup = GetComponentGroup(typeof(Actor), typeof(Target), typeof(Heading), typeof(Timeout), typeof(Velocity), typeof(Transform));
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var x = headingSeed;
        // Randomize the seed value every frame.
        x ^= x << 13; x ^= x >> 7; x ^= x << 17;
        headingSeed = x;

        var actors = actorDataGroup.GetComponentDataArray<Actor>();
        var targets = actorDataGroup.GetComponentDataArray<Target>();
        var timeouts = actorDataGroup.GetComponentDataArray<Timeout>();
        var headings = actorDataGroup.GetComponentDataArray<Heading>();
        var velocities = actorDataGroup.GetComponentDataArray<Velocity>();
        var transforms = actorDataGroup.GetComponentDataArray<Transform>();

        var navigationJob = new ActorNavigationJob{
            seed = headingSeed,
            deltaTime = Time.deltaTime,
            humanSpeed = ZombieSimulatorBootstrap.Settings.HumanSpeed,
            zombieSpeed = ZombieSimulatorBootstrap.Settings.ZombieSpeed,
            actors = actors,
            targets = targets,
            headings = headings,
            timeouts = timeouts,
            velocities = velocities,
            transforms = transforms
        };
        
        return navigationJob.Schedule(
            headings.Length, 64, inputDeps
        );
    }

    [BurstCompile]
    private struct ActorNavigationJob : IJobParallelFor
    {
        public long seed;
        public float deltaTime;
        public float humanSpeed;
        public float zombieSpeed;

        public ComponentDataArray<Heading> headings;
        public ComponentDataArray<Timeout> timeouts;
        public ComponentDataArray<Velocity> velocities;

        [ReadOnly]
        public ComponentDataArray<Actor> actors;
        [ReadOnly]
        public ComponentDataArray<Target> targets;
        [ReadOnly]
        public ComponentDataArray<Transform> transforms;

        public void Execute(int index)
        {
            // Note: This kinda breaks DoD principles
            // as it creates none linear memory access.
            switch (actors[index].Value)
            {
                case Actor.Type.Zombie:
                    ExecuteZombie(index);
                    break;
                case Actor.Type.Human:
                    ExecuteHuman(index);
                    break;
                default:
                    ExecuteNone(index);
                    break;
            }
        }

        private void ExecuteNone(int index)
        {
            var heading = headings[index];
            heading.Value = 0;
            headings[index] = heading;
        }

        private void ExecuteHuman(int index)
        {
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
            velocity.Value = humanSpeed;
            velocities[index] = velocity;
        }

        private void ExecuteZombie(int index)
        {
            var target = targets[index];
            var velocity = velocities[index];

            if (target.Entity < -0)
            {
                velocity.Value = 0;
                velocities[index] = velocity;
                return;
            }

            velocity.Value = zombieSpeed;
            velocities[index] = velocity;   

            var from = transforms[index].Position;
            var direction = math.normalize(target.Position - from);

            var heading = headings[index];
            heading.Angle = math.atan2(direction.x, direction.y);
            heading.Value = direction;
            headings[index] = heading;
        }

        private const double CMi = 2147483646.0;
        private const double CMui = 4294967293.0;
    }
}
