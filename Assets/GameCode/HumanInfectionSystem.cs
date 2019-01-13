﻿using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Burst;

[UpdateAfter(typeof(HumanToZombieSystem))]
class HumanInfectionSystem : JobComponentSystem
{
    [Inject] private ZombiePositionData zombieTargetData;
    [Inject] private HumanInfectionData humanData;

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var job = new HumanInfectionJob
        {
            zombieTargetData = zombieTargetData,
            humanData = humanData,
            infectionDistance = ZombieSettings.Instance.InfectionDistance
        };

        return job.Schedule(humanData.Length, humanData.Length, inputDeps);
    }
}

[BurstCompile]
struct HumanInfectionJob : IJobParallelFor
{
    public ZombiePositionData zombieTargetData;

    [NativeDisableParallelForRestriction]
    public HumanInfectionData humanData;

    public float infectionDistance;

    public void Execute(int index)
    {
        var zombie = zombieTargetData.Zombies[index];
        if (zombie.HumanTargetIndex == -1 || zombie.BecomeActive != 1)
            return;

        var human = humanData.Humans[zombie.HumanTargetIndex];
        if (human.IsInfected == 1)
            return;

        float2 humanPosition = humanData.Positions[zombie.HumanTargetIndex].Value;
        float2 zombiePosition = zombieTargetData.Positions[index].Value;

        float distSquared = math.distance(zombiePosition, humanPosition);

        if (distSquared < infectionDistance)
        {
            human.IsInfected = 1;
            humanData.Humans[zombie.HumanTargetIndex] = human;
        }
    }
}

public struct HumanInfectionData
{
    public readonly int Length;
    [ReadOnly] public ComponentDataArray<Position2D> Positions;
    public ComponentDataArray<Human> Humans;
}

public struct ZombiePositionData
{
    public readonly int Length;
    [ReadOnly] public ComponentDataArray<Position2D> Positions;
    [ReadOnly] public ComponentDataArray<Zombie> Zombies;
}