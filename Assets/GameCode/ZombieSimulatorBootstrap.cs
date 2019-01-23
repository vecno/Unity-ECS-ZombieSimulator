using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using Random = UnityEngine.Random;

public class ZombieSimulatorBootstrap
{
    public static ZombieSettings Settings { get; private set; }

    public static RenderMesh HumanLook { get; private set; }
    public static EntityArchetype HumanArchetype { get; private set; }

    public static RenderMesh ZombieLook { get; private set; }
    
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

        Settings = settingsGo.GetComponent<ZombieSettings>();
        if (!Settings) return;

        HumanLook = GetLookFromPrototype("HumanRenderPrototype");
        ZombieLook = GetLookFromPrototype("ZombieRenderPrototype");

        NewGame();
    }

    private static void NewGame()
    {
        var entityManager = World.Active.GetOrCreateManager<EntityManager>();
        CreateZombies(entityManager);
        CreateHumans(entityManager);
    }
    
    private static void CreateHumans(EntityManager entityManager)
    {
        var humans = new NativeArray<Entity>(Settings.HumanCount, Allocator.Temp);
        entityManager.CreateEntity(HumanArchetype, humans);

        foreach (var human in humans)
        { SetupHumanoidEntity(entityManager, human, HumanLook); }
        
        humans.Dispose();
    }
    
    private static void CreateZombies(EntityManager entityManager)
    {
        var zombies = new NativeArray<Entity>(Settings.ZombieCount, Allocator.Temp);
        entityManager.CreateEntity(ZombieArchetype, zombies);
        
        foreach (var zombie in zombies)
        { SetupHumanoidEntity(entityManager, zombie, ZombieLook); }
        
        zombies.Dispose();
    }

    private static void SetupHumanoidEntity(EntityManager entityManager, Entity entity, RenderMesh renderer)
    {
        var heading = math.normalize(new float2(
            (Random.value - 0.5f) * 2.0f, (Random.value - 0.5f) * 2.0f
        ));
        var angle = math.atan2(heading.x, heading.y);
        var rotation = Quaternion.AngleAxis(
            angle * Mathf.Rad2Deg, Vector3.up
        );
        entityManager.SetComponentData(entity, new Heading{
            Angle = angle,
            Value = heading
        });
        entityManager.SetComponentData(entity, new Timeout{
            Value = 15.0f * Random.value
        });
        entityManager.SetComponentData(entity, new Position{
            Value = ComputeSpawnLocation()
        });
        entityManager.SetComponentData(entity, new Rotation{
            Value = rotation
        });
        entityManager.SetComponentData(entity, new Velocity{
            Value = 6
        });
        entityManager.SetSharedComponentData(entity, renderer);        
    }

    private static float3 ComputeSpawnLocation()
    {
        var settings = ZombieSettings.Instance;

        var r = Random.value;
        var x0 = settings.Playfield.xMin;
        var x1 = settings.Playfield.xMax;
        var x = x0 + (x1 - x0) * r;

        var r2 = Random.value;
        var y0 = settings.Playfield.yMin;
        var y1 = settings.Playfield.yMax;
        var y = y0 + (y1 - y0) * r2;

        return new float3(x, 0, y);
    }

    private static void DefineArchetypes(EntityManager entityManager)
    {   
        HumanArchetype = entityManager.CreateArchetype(
            typeof(Human),
            typeof(Active),
            typeof(Target),
            typeof(Heading),
            typeof(Timeout),
            typeof(Position),
            typeof(Rotation),
            typeof(Velocity),
            typeof(RenderMesh)
        );

        ZombieArchetype = entityManager.CreateArchetype(
            typeof(Zombie),
            typeof(Active),
            typeof(Target),
            typeof(Heading),
            typeof(Timeout),
            typeof(Position),
            typeof(Rotation),
            typeof(Velocity),
            typeof(RenderMesh)
        );
    }

    private static RenderMesh GetLookFromPrototype(string protoName)
    {
        var proto = GameObject.Find(protoName);
        var result = proto.GetComponent<RenderMeshComponent>().Value;
        UnityEngine.Object.Destroy(proto);
        return result;
    }

}
