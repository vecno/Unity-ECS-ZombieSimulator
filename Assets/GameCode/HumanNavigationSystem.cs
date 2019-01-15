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
        // Collect all entities tagged as human and with an heading and timeout component.
        humanDataGroup = GetComponentGroup(typeof(Human), typeof(Heading), typeof(Timeout));
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var x = headingSeed;
        // Randomize the seed value every frame.
        x ^= x << 13; x ^= x >> 7; x ^= x << 17;
        headingSeed = x;

        var timeouts = humanDataGroup.GetComponentDataArray<Timeout>();
        var headings = humanDataGroup.GetComponentDataArray<Heading>();
        
        var navigationJob = new HumanNavigationJob{
            sd = headingSeed,
            dt = Time.deltaTime,
            headings = headings,
            timeouts = timeouts            
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
        
        public ComponentDataArray<Heading> headings;
        public ComponentDataArray<Timeout> timeouts;

        public void Execute(int index)
        {
            var timeout = timeouts[index];
            
            timeout.Value -= dt;
            if (timeout.Value > 0)
            { timeouts[index] = timeout; return; }
            
            var x = ((sd * index) << 15) + sd;
            // Randomize shared seed and round it.
            x ^= x << 13; x ^= x >> 7; x ^= x << 17;
            var val = new double2((int)x, (int)(x >> 32));

            timeout.Value = (float)(7.5 * (val.x+val.y));
            timeouts[index] = timeout;

            var rounded = (((val + CMi) / CMui) - 0.5) * 0.5;
            var direction = math.normalize(rounded);
            
            var heading = headings[index];
            heading.Angle = (float)math.atan2(direction.x, direction.y);
            heading.Value = (float2)rounded;
            headings[index] = heading;
        }
        
        private const double CMi = 2147483646.0;
        private const double CMui = 4294967293.0;
    }
}
