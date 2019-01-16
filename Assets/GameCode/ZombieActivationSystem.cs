//using Unity.Entities;
//using Unity.Jobs;
//using Unity.Transforms;
//using Unity.Burst;
//using Unity.Mathematics;
//
//[UpdateAfter(typeof(HumanToZombieSystem))]
//class ZombieActivationSystem : JobComponentSystem
//{
//    protected override JobHandle OnUpdate(JobHandle inputDeps)
//    {
//        // ToDo Enable this again, it could use the timeout?
//        // It could ping this to check for stragglers, if one
//        // enters detection range, activate some zombies again.
//        return inputDeps;
//    }
//}
//
////[BurstCompile]
////public struct ZombieActivationJob : IJobParallelFor
////{
////    public float zombieSpeed;
////
////    public ComponentDataArray<Zombie> zombieData;
////    
////    public ComponentDataArray<Position> zombiePositions;
////    public ComponentDataArray<Velocity> zombieVelocities;
////    
////    public void Execute(int index)
////    {
////        var zombie = zombieData[index];
////        
////        if (zombie.BecomeActive != 1) 
////            return;
////        if (zombie.FinishedActivation != 0) 
////            return;
////        
////        var position = zombiePositions[index];
////        position.Value = new float3(
////            zombie.BecomeZombiePosition.x,
////            0f, zombie.BecomeZombiePosition.y
////        );
////        zombiePositions[index] = position;
////
////        var velocity = zombieVelocities[index];
////        velocity.Value = 6;
////        zombieVelocities[index] = velocity;
////
////        zombie.FinishedActivation = 1;
////        zombieData[index] = zombie;
////    }
////}
