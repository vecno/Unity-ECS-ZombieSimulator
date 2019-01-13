using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public class ZombieSimulatorBootstrap
{
    public static ZombieSettings Settings { get; private set; }

    public static MeshInstanceRenderer HumanLook { get; private set; }
    public static EntityArchetype HumanArchetype { get; private set; }

    public static MeshInstanceRenderer ZombieLook { get; private set; }
    public static EntityArchetype ZombieArchetype { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        var entityManager = World.Active.GetOrCreateManager<EntityManager>();
        DefineArchetypes(entityManager);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void InitializeWithScene()
    {
        var settingsGo = GameObject.Find("ZombieSettings");
        if (null == settingsGo) return;
        
        // Note: Doing this on Unity objects is bad practice, it
        // bypasses the lifetime check of the engines object system.
        // Settings = settingsGo?.GetComponent<ZombieSettings>();

        Settings = settingsGo.GetComponent<ZombieSettings>();
        if (!Settings) return;

        HumanLook = GetLookFromPrototype("HumanRenderPrototype");
        ZombieLook = GetLookFromPrototype("ZombieRenderPrototype");

        NewGame();
    }

    private static void NewGame()
    {
        var entityManager = World.Active.GetOrCreateManager<EntityManager>();
        CreateHumans(entityManager);
        CreateZombies(entityManager);
    }

    private static void CreateHumans(EntityManager entityManager)
    {
        var humans = new NativeArray<Entity>(Settings.HumanCount, Allocator.Temp);
        
        entityManager.CreateEntity(HumanArchetype, humans);
        foreach (var human in humans) { SetupHumanoidEntity(
            entityManager, human, Settings.HumanSpeed, HumanLook
        ); }

        humans.Dispose();
    }
    
    private static void CreateZombies(EntityManager entityManager)
    {
        var zombies = new NativeArray<Entity>(Settings.ZombieCount, Allocator.Temp);
        
        entityManager.CreateEntity(ZombieArchetype, zombies);
        foreach (var zombie in zombies) { SetupHumanoidEntity(
            entityManager, zombie, Settings.ZombieSpeed, ZombieLook
        ); }

        zombies.Dispose();
    }

    private static void SetupHumanoidEntity(EntityManager entityManager, Entity entity, float moveSpeed, MeshInstanceRenderer renderer)
    {
        var position = ComputeSpawnLocation();
        entityManager.SetComponentData(entity, new Position{
            Value = new float3(position.x, 0.0f, position.y)
        });
        entityManager.SetComponentData(entity, new Rotation{
            Value = Quaternion.identity
        });
        entityManager.SetComponentData(entity, new Position2D { Value = position });
        entityManager.SetComponentData(entity, new Heading2D { Value = new float2(1.0f, 0.0f) });
        entityManager.SetComponentData(entity, new MoveSpeed { speed = moveSpeed });

        entityManager.AddSharedComponentData(entity, renderer);        
    }

    private static float2 ComputeSpawnLocation()
    {
        var settings = ZombieSettings.Instance;

        var r = UnityEngine.Random.value;
        var x0 = settings.Playfield.xMin;
        var x1 = settings.Playfield.xMax;
        var x = x0 + (x1 - x0) * r;

        var r2 = UnityEngine.Random.value;
        var y0 = settings.Playfield.yMin;
        var y1 = settings.Playfield.yMax;
        var y = y0 + (y1 - y0) * r2;

        return new float2(x, y);
    }

    private static void DefineArchetypes(EntityManager entityManager)
    {
        HumanArchetype = entityManager.CreateArchetype(typeof(Human),
                                                       typeof(Position),
                                                       typeof(Rotation),
                                                       typeof(Heading2D),
                                                       typeof(MoveSpeed),
                                                       typeof(Position2D));

        ZombieArchetype = entityManager.CreateArchetype(typeof(Zombie),
                                                        typeof(Position),
                                                        typeof(Rotation),
                                                        typeof(Heading2D),
                                                        typeof(MoveSpeed),
                                                        typeof(Position2D));
    }

    private static MeshInstanceRenderer GetLookFromPrototype(string protoName)
    {
        var proto = GameObject.Find(protoName);
        var result = proto.GetComponent<MeshInstanceRendererComponent>().Value;
        UnityEngine.Object.Destroy(proto);
        return result;
    }

}
