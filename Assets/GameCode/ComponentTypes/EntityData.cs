using Unity.Entities;
using Unity.Mathematics;

public struct Owner : IComponentData
{
    public int Entity;    
}

public struct Target : IComponentData
{
    public int Entity;
    public float2 Position;
}

public struct Heading : IComponentData
{
    public float Angle;
    public float2 Value;
}

public struct Renderer : IComponentData
{
    public float2 Value;
}

public struct Timeout : IComponentData
{
    public float Value;
}

public struct Velocity : IComponentData
{
    public float Value;
}

public struct Transform : IComponentData
{
    public float2 Position;
    public float2 Rotation;
}