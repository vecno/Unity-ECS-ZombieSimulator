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

    public static MeshInstanceRenderer HumanLook { get; private set; }
    public static MeshInstanceRenderer ZombieLook { get; private set; }

    public static EntityArchetype LogicArchetype { get; private set; }
    public static EntityArchetype HumanRenderArchetype { get; private set; }
    public static EntityArchetype ZombieRenderArchetype { get; private set; }
    
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
        var logic = new NativeArray<Entity>(Settings.HumanCount, Allocator.Temp);
        var humans = new NativeArray<Entity>(Settings.HumanCount, Allocator.Temp);
        var zombies = new NativeArray<Entity>(Settings.HumanCount, Allocator.Temp);

        entityManager.CreateEntity(LogicArchetype, logic);
        entityManager.CreateEntity(HumanRenderArchetype, humans);
        entityManager.CreateEntity(ZombieRenderArchetype, zombies);
        SetupHumanoidEntity(entityManager, logic, humans, zombies, true);

        logic.Dispose();
        humans.Dispose();
        zombies.Dispose();
    }
    
    private static void CreateZombies(EntityManager entityManager)
    {
        var logic = new NativeArray<Entity>(Settings.ZombieCount, Allocator.Temp);
        var humans = new NativeArray<Entity>(Settings.ZombieCount, Allocator.Temp);
        var zombies = new NativeArray<Entity>(Settings.ZombieCount, Allocator.Temp);

        entityManager.CreateEntity(LogicArchetype, logic);
        entityManager.CreateEntity(HumanRenderArchetype, humans);
        entityManager.CreateEntity(ZombieRenderArchetype, zombies);
        SetupHumanoidEntity(entityManager, logic, humans, zombies, false);

        logic.Dispose();
        humans.Dispose();
        zombies.Dispose();
    }

    private static void SetupHumanoidEntity(
        EntityManager entityManager, NativeArray<Entity> logic,
        NativeArray<Entity> humans, NativeArray<Entity> zombies, bool isHuman
    ) {
        var hpos = isHuman ? .5f : -.5f;
        var zpos = isHuman ? -.5f : .5f;
        var type = isHuman ? Actor.Type.Human : Actor.Type.Zombie;
        var sval = isHuman ? Settings.HumanSpeed : Settings.ZombieSpeed;
        for (var i = 0; i < logic.Length; i++)
        {
            var dir = (new float2(Random.value, Random.value) - 0.5f) * 2.0f;
            var nrm = math.normalize(dir);
            var angle = math.atan2(nrm.x, nrm.y);
            var rotation = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.up);
            var position = ComputeSpawnLocation();

            var le = logic[i];
            var he = humans[i];
            var ze = zombies[i];

            entityManager.SetComponentData(le, new Actor{ Value = type });
            entityManager.SetComponentData(le, new Heading{ Angle = angle, Value = dir });
            entityManager.SetComponentData(le, new Timeout{ Value = 15.0f * Random.value });
            entityManager.SetComponentData(le, new Velocity{ Value = sval });
            entityManager.SetComponentData(le, new Renderer{
                Value = { x = hpos, y = zpos }, 
                
            });
            entityManager.SetComponentData(le, new Transform{
                Position = { x = position.x, y = position.z },
                Rotation = { x = rotation.y, y = rotation.w }
            });

            position.y = zpos;
            entityManager.SetComponentData(ze, new Owner{ Entity = le.GetHashCode() });
            entityManager.SetComponentData(ze, new Position{ Value = position });
            entityManager.SetComponentData(ze, new Rotation{ Value = rotation });
            entityManager.SetSharedComponentData(ze, ZombieLook);

            position.y = hpos;
            entityManager.SetComponentData(he, new Owner{ Entity = le.GetHashCode() });
            entityManager.SetComponentData(he, new Position{ Value = position });
            entityManager.SetComponentData(he, new Rotation{ Value = rotation });
            entityManager.SetSharedComponentData(he, HumanLook);
        }
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
        LogicArchetype = entityManager.CreateArchetype(
            ComponentType.Create<Actor>(),
            ComponentType.Create<Target>(),
            ComponentType.Create<Heading>(),
            ComponentType.Create<Timeout>(),
            ComponentType.Create<Renderer>(),
            ComponentType.Create<Velocity>(),
            ComponentType.Create<Transform>()
        );
        HumanRenderArchetype = entityManager.CreateArchetype(
            ComponentType.Create<Owner>(),
            ComponentType.Create<Human>(),
            ComponentType.Create<Position>(),
            ComponentType.Create<Rotation>(),
            ComponentType.Create<MeshInstanceRenderer>()
        );
        ZombieRenderArchetype = entityManager.CreateArchetype(
            ComponentType.Create<Owner>(),
            ComponentType.Create<Zombie>(),
            ComponentType.Create<Position>(),
            ComponentType.Create<Rotation>(),
            ComponentType.Create<MeshInstanceRenderer>()
        );
    }

    private static MeshInstanceRenderer GetLookFromPrototype(string protoName)
    {
        var proto = GameObject.Find(protoName);
        var result = proto.GetComponent<MeshInstanceRendererComponent>().Value;
        UnityEngine.Object.Destroy(proto);
        return result;
    }

}
