using Unity.Entities;

public struct Actor : IComponentData
{
    public Type Value;
    
    public enum Type : byte
    {
        None = 0,
        Human = 1,
        Zombie = 2
    }
}

public struct Human : IComponentData {}
public struct Zombie : IComponentData {}
