
using Unity.Entities;
using Unity.Mathematics;

public class EntityUtil
{
    public static float3 GetOffScreenLocation()
    {
        return new float3(1000.0f, 1000.0f, 1000.0f);
    }
}

public struct Heading : IComponentData
{
    public float2 Value;
}

public struct Velocity : IComponentData
{
    public float Value;
}
