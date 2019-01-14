using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Burst;

[UpdateAfter(typeof(HumanToZombieSystem))]
class HumanNavigationSystem : JobComponentSystem
{
    private long headingSeed;
    
    private ComponentGroup humanDataGroup;
    
    protected override void OnStartRunning()
    {
        headingSeed = (long)(long.MaxValue * UnityEngine.Random.value);
        humanDataGroup = GetComponentGroup(typeof(Human), typeof(Heading));
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var x = headingSeed;
        // Randomize the seed value every frame.
        x ^= x << 13; x ^= x >> 7; x ^= x << 17;
        headingSeed = x;

        var humans = humanDataGroup.GetComponentDataArray<Human>();
        var headings = humanDataGroup.GetComponentDataArray<Heading>();
        
        var directions = new NativeArray<float2>(
            headings.Length, Allocator.TempJob
        );
        
        var directionsJob = new RandDirectionsJob{
            seed = headingSeed,
            directions = directions
        };
        var navigationJob = new HumanNavigationJob{
            dt = Time.deltaTime,
            humans = humans,
            headings = headings,
            directions = directions
            
        };
        
        var jobHandle = directionsJob.Schedule(
            directions.Length, 64, inputDeps
        );
        return navigationJob.Schedule(
            headings.Length, 64, jobHandle
        );
    }

    [BurstCompile]
    private struct RandDirectionsJob : IJobParallelFor
    {
        [ReadOnly]
        public long seed;
    
        public NativeArray<float2> directions;

        public void Execute(int index)
        {
            var x = (seed * index) + seed;
            // Randomize shared seed and round it.
            x ^= x << 13; x ^= x >> 7; x ^= x << 17;
            var val = new double2((int)x, (int)(x >> 32));
            directions[index] = (float2) ((val + CMi) / CMui);
        }
    
        private const double CMi = 2147483646.0;
        private const double CMui = 4294967293.0;
    }

    [BurstCompile]
    private struct HumanNavigationJob : IJobParallelFor
    {
        public float dt;

        [DeallocateOnJobCompletionAttribute]
        public NativeArray<float2> directions;
        
        public ComponentDataArray<Human> humans;
        public ComponentDataArray<Heading> headings;

        public void Execute(int index)
        {
            // Note: It seems this split is not needed,
            // the goal is to pack related data in memory.
            // Why is the countdown timer in an other array?
            // The reason Heading and old movement was removed?
            
            var human = humans[index];
            human.TimeTillNextDirectionChange -= dt;
            
            if (human.TimeTillNextDirectionChange > 0)
            { humans[index] = human; return; }
            
            human.TimeTillNextDirectionChange = 5;
            humans[index] = human;
            
            var heading = headings[index];
            heading.Value = (directions[index] - 0.5f) * 0.5f;
            headings[index] = heading;
        }
    }
}
