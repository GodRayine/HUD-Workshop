using System.Numerics;

namespace HudWorkshop;

internal static class ScaleGeometry
{
    // Nine HUD anchors: left/centre/right, then top/middle/bottom.
    internal static Vector2 AnchorCompensation(byte anchor, Vector2 unscaledSize, float before, float after)
    {
        if (anchor > 8 || !float.IsFinite(before) || !float.IsFinite(after) || before <= 0 || after <= 0)
            throw new ArgumentOutOfRangeException(nameof(anchor));
        return AnchorCompensation(anchor, unscaledSize * before, unscaledSize * after);
    }
    internal static Vector2 AnchorCompensation(byte anchor, Vector2 beforeSize, Vector2 afterSize)
    {
        if(anchor>8 || !float.IsFinite(beforeSize.X) || !float.IsFinite(beforeSize.Y)
            || !float.IsFinite(afterSize.X) || !float.IsFinite(afterSize.Y)
            || beforeSize.X<=0 || beforeSize.Y<=0 || afterSize.X<=0 || afterSize.Y<=0)
            throw new ArgumentOutOfRangeException(nameof(anchor));
        return (afterSize-beforeSize)*new Vector2(anchor%3/2f,anchor/3/2f);
    }
}
