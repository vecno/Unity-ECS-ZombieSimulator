using Unity.Entities;
using Unity.Mathematics;

public class EntityUtil
{
    public static float2 GetOffScreenLocation()
    {
        return new float2(100, 100);
    }
}

public struct Position2D : IComponentData
{
    public float2 Value;
}

public struct Heading2D : IComponentData
{
    public float2 Value;
}

public struct MoveSpeed : IComponentData
{
    public float speed;
}