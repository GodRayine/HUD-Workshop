using System.Numerics;
namespace HudWorkshop;

public readonly record struct PanelRect(string Id, Vector2 Position, Vector2 Offset, Vector2 Size)
{
    public Vector2 Start => Position + Offset;
    public Vector2 End => Start + Size;
}
public static class LayoutGeometry
{
    public static (Vector2 Start, Vector2 Size) Bounds(IReadOnlyList<PanelRect> panels)
    {
        if (panels.Count == 0) throw new ArgumentException("Empty selection", nameof(panels));
        var min = panels[0].Start; var max = panels[0].End;
        foreach (var p in panels) { min = Vector2.Min(min, p.Start); max = Vector2.Max(max, p.End); }
        return (min, max - min);
    }
    public static Dictionary<string, Vector2> Translate(IReadOnlyList<PanelRect> panels, Vector2 delta)
    {
        delta = new(MathF.Round(delta.X), MathF.Round(delta.Y));
        return panels.ToDictionary(p => p.Id, p => p.Position + delta);
    }
    public static Dictionary<string, Vector2> Align(IReadOnlyList<PanelRect> panels, int mode)
    {
        if (mode is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(mode));
        var bounds = Bounds(panels);
        return panels.ToDictionary(p => p.Id, p => mode < 3
            ? new Vector2(MathF.Round(bounds.Start.X + (bounds.Size.X - p.Size.X) * (mode / 2f) - p.Offset.X), p.Position.Y)
            : new Vector2(p.Position.X, MathF.Round(bounds.Start.Y + (bounds.Size.Y - p.Size.Y) * ((mode - 3) / 2f) - p.Offset.Y)));
    }
    public static Dictionary<string, Vector2> Distribute(IReadOnlyList<PanelRect> panels, bool horizontal)
    {
        if (panels.Count < 3) return new();
        float Start(PanelRect p) => horizontal ? p.Start.X : p.Start.Y;
        float Size(PanelRect p) => horizontal ? p.Size.X : p.Size.Y;
        var ordered = panels.OrderBy(Start).ToArray();
        var gap = (Start(ordered[^1]) + Size(ordered[^1]) - Start(ordered[0]) - ordered.Sum(Size)) / (ordered.Length - 1);
        var cursor = Start(ordered[0]); var result = new Dictionary<string, Vector2>();
        for (var i = 0; i < ordered.Length; i++)
        {
            var p = ordered[i];
            if (i > 0 && i < ordered.Length - 1)
                result[p.Id] = horizontal ? new(MathF.Round(cursor - p.Offset.X), p.Position.Y) : new(p.Position.X, MathF.Round(cursor - p.Offset.Y));
            cursor += Size(p) + gap;
        }
        return result;
    }
}
