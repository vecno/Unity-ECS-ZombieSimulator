using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Burst;

class HumanNavigationSystem : JobComponentSystem
{
    private long headingSeed;
    
    private ComponentGroup humanDataGroup;
    
    protected override void OnStartRunning()
    {
        headingSeed = (long)(long.MaxValue * UnityEngine.Random.value);
        humanDataGroup = GetComponentGroup(typeof(Human), typeof(Heading), typeof(Timeout), typeof(Velocity));
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var x = headingSeed;
        // Randomize the seed value every frame.
        x ^= x << 13; x ^= x >> 7; x ^= x << 17;
        headingSeed = x;

        var timeouts = humanDataGroup.GetComponentDataArray<Timeout>();
        var headings = humanDataGroup.GetComponentDataArray<Heading>();
        var velocities = humanDataGroup.GetComponentDataArray<Velocity>();
        
        var navigationJob = new HumanNavigationJob{
            sp = ZombieSimulatorBootstrap.Settings.HumanSpeed,
            sd = headingSeed,
            dt = Time.deltaTime,
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
        public long sd;
        public float dt;
        public float sp;
        
        public ComponentDataArray<Heading> headings;
        public ComponentDataArray<Timeout> timeouts;
        
        public ComponentDataArray<Velocity> velocities;

        public void Execute(int index)
        {
            var timeout = timeouts[index];
            
            timeout.Value -= dt;
            if (timeout.Value > 0)
            { timeouts[index] = timeout; return; }
            
            var x = ((sd + index) << 15) + sd;
            // Randomize shared seed and round it.
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
            velocity.Value = sp;
            velocities[index] = velocity;
        }
        
        private const double CMi = 2147483646.0;
        private const double CMui = 4294967293.0;
    }
}
