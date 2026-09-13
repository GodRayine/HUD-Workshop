using System.Numerics;
namespace HudWorkshop;
internal static class OfflineGeometry
{
    internal static Vector2 Origin(Vector2 percentages,Vector2 size,float scale,byte anchor,Vector2 viewport)
    {
        if(anchor>8 || size.X<=0 || size.Y<=0 || scale<=0 || !float.IsFinite(scale)
            || !float.IsFinite(percentages.X) || !float.IsFinite(percentages.Y) || viewport.X<=0 || viewport.Y<=0)
            throw new ArgumentOutOfRangeException(nameof(anchor));
        var p=percentages*viewport/100-size*scale*new Vector2(anchor%3/2f,anchor/3/2f);
        return new(MathF.Round(p.X),MathF.Round(p.Y));
    }
    internal static byte EnabledByte(byte original,bool enabled)=>enabled?(byte)(original|1):(byte)(original&~1);
}
