using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

class ZombieNavigationSystem : JobComponentSystem
{
    private ComponentGroup actorDataGroup;

    protected override void OnStartRunning()
    {
        actorDataGroup = GetComponentGroup(typeof(Actor), typeof(Target), typeof(Heading), typeof(Velocity), typeof(Transform));
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var actors = actorDataGroup.GetComponentDataArray<Actor>();
        var targets = actorDataGroup.GetComponentDataArray<Target>();
        var headings = actorDataGroup.GetComponentDataArray<Heading>();
        var velocities = actorDataGroup.GetComponentDataArray<Velocity>();
        var transforms = actorDataGroup.GetComponentDataArray<Transform>();

        var navigationJob = new ZombieNavigationJob{
            speed = ZombieSettings.Instance.ZombieSpeed,
            actors = actors,
            targets = targets,
            headings = headings,
            velocities = velocities,
            transforms = transforms
        };

        return navigationJob.Schedule(
            actors.Length, 64, inputDeps
        );
    }
}

[BurstCompile]
public struct ZombieNavigationJob : IJobParallelFor
{
    public float speed;

    public ComponentDataArray<Heading> headings;
    public ComponentDataArray<Velocity> velocities;

    [ReadOnly]
    public ComponentDataArray<Actor> actors;
    [ReadOnly]
    public ComponentDataArray<Target> targets;
    [ReadOnly]
    public ComponentDataArray<Transform> transforms;

    public void Execute(int index)
    {
        var heading = headings[index];
        if (Actor.Type.None == actors[index].Value)
        {
            heading.Value = 0;
            headings[index] = heading;
            return;
        }

        if (Actor.Type.Zombie != actors[index].Value)
            return;

        var target = targets[index];
        var velocity = velocities[index];

        if (target.Entity < -0)
        {
            velocity.Value = 0;
            velocities[index] = velocity;
            return;
        }

        velocity.Value = speed;
        velocities[index] = velocity;   

        var from = transforms[index].Position;
        var direction = math.normalize(target.Position - from);

        heading.Angle = math.atan2(direction.x, direction.y);
        heading.Value = direction;
        headings[index] = heading;
    }
}
