using Unity.Mathematics;

public class EntityUtil
{
    public static float3 GetOffScreenLocation()
    {
        return new float3(1000.0f, 1000.0f, 1000.0f);
    }
    
    // Maps a position on to the index of a hexagonal tilemap.
    // Ref. https://www.redblobgames.com/grids/hexagons/#pixel-to-hex
    public int2 ToIndex(float2 pnt, float scale)
    {
        var p = pnt / scale;
        var q = (Layout.x * p.x + p.y) * 2.0;
        var r = (Layout.y * p.x + Layout.z * p.y) * 2.0;
        return (int2) math.round(new double2(q, r));
    }

    public static readonly double3 Layout = new double3 {
        x = 2.0 / 3.0,
        y = -1.0 / 3.0,
        z = math.sqrt(3.0) / 3.0
    };
}
