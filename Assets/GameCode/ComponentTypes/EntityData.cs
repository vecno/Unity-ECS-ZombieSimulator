using Unity.Entities;
using Unity.Mathematics;

public struct Heading : IComponentData
{
    public float Angle;
    public float2 Value;
}

public struct Target : IComponentData
{
    public int entity;
    public float2 position;
}

public struct Timeout : IComponentData
{
    public float Value;
}

public struct Velocity : IComponentData
{
    public float Value;
}