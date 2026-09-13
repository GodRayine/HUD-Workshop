using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;

namespace HudWorkshop;

public sealed partial class Plugin
{
    private readonly HashSet<string> selection = new();
    private List<Bar> gestureStart = new();
    private List<string[]> groups = new();
    private Vector2? marqueeStart;
    private HashSet<string> marqueeBase = new();
    private string GroupPath => Path.Combine(PluginInterface.GetPluginConfigDirectory(), $"groups-{editorOwner:X}-{baseline?.LayoutIndex ?? 0}.json");

    private void LoadGroups()
    {
        try { groups = File.Exists(GroupPath) ? JsonSerializer.Deserialize<List<string[]>>(File.ReadAllText(GroupPath)) ?? new() : new(); }
        catch (Exception ex) { groups = new(); Log.Warning(ex, "Cannot read groups"); }
        groups = groups.Where(g => g != null).Select(g => g.Where(Names.Contains).Distinct().ToArray()).Where(g => g.Length > 1).ToList();
        marqueeStart = null; gestureStart.Clear();
    }
    private void SaveGroups()
    {
        Directory.CreateDirectory(PluginInterface.GetPluginConfigDirectory());
        var path = GroupPath;
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(groups));
        File.Move(path + ".tmp", path, true);
    }
    private IEnumerable<string> Members(string id, Snapshot current) => (groups.FirstOrDefault(g => g.Contains(id)) ?? [id])
        .Where(member => current.Bars.Any(b => b.Id == member && b.Editable));
    private void SelectPanel(string id, bool additive, Snapshot current)
    {
        var members = Members(id, current).ToArray();
        if (additive)
        {
            if (members.All(selection.Contains)) selection.ExceptWith(members); else selection.UnionWith(members);
        }
        else if (!selection.Contains(id)) { selection.Clear(); selection.UnionWith(members); }
        selected = selection.Contains(id) ? id : selection.FirstOrDefault() ?? "";
    }
    private void ApplyMoves(Move[] moves, bool forward)
    {
        // Validate the entire batch before the first native write.
        var available=Read();
        foreach (var move in moves)
        {
            var p = forward ? move.After : move.Before;
            var scale = forward ? move.AfterScale : move.BeforeScale;
            if (scale is { } value && (!float.IsFinite(value) || value < .5f || value > 3f)) throw new InvalidOperationException("Некорректный масштаб.");
            var addon = GameGui.GetAddonByName(move.Id);
            if (!available.Bars.Any(b=>b.Id==move.Id && b.Editable) || !float.IsFinite(p.X) || !float.IsFinite(p.Y)
                || p.X < short.MinValue || p.X > short.MaxValue || p.Y < short.MinValue || p.Y > short.MaxValue)
                throw new InvalidOperationException("Одна из выбранных панелей недоступна или выходит за пределы координат.");
        }
        foreach (var move in moves)
        {
            var scale = forward ? move.AfterScale : move.BeforeScale;
            // Track position first so cancellation also owns a partially applied scale operation.
            SetPreview(move.Id, forward ? move.After : move.Before);
            if (scale is { } value) SetScalePreview(move.Id, value);
            var options=forward?move.AfterOptions:move.BeforeOptions;
            if(options is { } flags) SetOptionsPreview(move.Id,flags);
            var enabled=forward?move.AfterEnabled:move.BeforeEnabled;
            if(enabled is { } state) SetEnabledPreview(move.Id,state);
        }
    }
    private void ApplyBatch(Move[] moves)
    {
        moves = moves.Select(m => m with { After = new(MathF.Round(m.After.X), MathF.Round(m.After.Y)) }).Where(m => m.Before != m.After || m.BeforeScale != m.AfterScale || m.BeforeOptions != m.AfterOptions || m.BeforeEnabled != m.AfterEnabled).ToArray();
        if (moves.Length == 0) return;
        ApplyMoves(moves, true); undo.Push(moves); redo.Clear();
    }
    private static Bar SelectionBounds(List<Bar> bars)
    {
        var bounds = LayoutGeometry.Bounds(Rects(bars));
        return bars[0] with { X = bounds.Start.X, Y = bounds.Start.Y, OffsetX = 0, OffsetY = 0, Width = bounds.Size.X, Height = bounds.Size.Y };
    }
    private static PanelRect[] Rects(List<Bar> bars) => bars.Select(b => new PanelRect(b.Id, new(b.X, b.Y), new(b.OffsetX, b.OffsetY), new(b.Width, b.Height))).ToArray();
    private void ApplyTargets(List<Bar> bars, Dictionary<string, Vector2> targets) => ApplyBatch(bars.Where(b => targets.ContainsKey(b.Id))
        .Select(b => new Move(b.Id, new(b.X, b.Y), targets[b.Id])).ToArray());

    private void DragSelection(Snapshot current)
    {
        if (gestureStart.Count == 0) return;
        var bounds = SelectionBounds(gestureStart);
        var origin = new Vector2(bounds.X, bounds.Y);
        var delta = SnapPosition(bounds, origin + ImGui.GetMousePos() - dragMouse, current) - origin;
        var targets = LayoutGeometry.Translate(Rects(gestureStart), delta);
        ApplyMoves(gestureStart.Select(b => new Move(b.Id, new(b.X, b.Y), targets[b.Id])).ToArray(), true);
    }
    private void AlignSelectionToGrid(Snapshot current)
    {
        var bars = current.Bars.Where(b => b.Editable && selection.Contains(b.Id)).ToList();
        if (bars.Count == 0) return;
        var bounds = SelectionBounds(bars);
        var delta = new Vector2(MathF.Round(bounds.X / gridStep) * gridStep - bounds.X,
            MathF.Round(bounds.Y / gridStep) * gridStep - bounds.Y);
        ApplyTargets(bars, LayoutGeometry.Translate(Rects(bars), delta));
    }
    private void DrawMarquee(Snapshot current, Vector2 viewport, ImDrawListPtr draw)
    {
        var selectedBars = current.Bars.Where(b => b.Editable && selection.Contains(b.Id)).ToList();
        if (selectedBars.Count > 1)
        {
            var bounds = SelectionBounds(selectedBars);
            var pos = viewport + new Vector2(bounds.X, bounds.Y);
            draw.AddRect(pos - new Vector2(3), pos + new Vector2(bounds.Width, bounds.Height) + new Vector2(3), 0xff83cbff, 3);
        }
        if (gesture == null && ImGui.IsWindowHovered() && !ImGui.IsAnyItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            marqueeStart = ImGui.GetMousePos();
            marqueeBase = ImGui.GetIO().KeyShift ? new(selection) : new();
        }
        if (marqueeStart is not { } start) return;
        if (ImGui.IsKeyPressed(ImGuiKey.Escape)) { marqueeStart = null; return; }
        var end = ImGui.GetMousePos(); var min = Vector2.Min(start, end); var max = Vector2.Max(start, end);
        draw.AddRectFilled(min, max, 0x224aa5ff); draw.AddRect(min, max, 0xff83cbff);
        if (!ImGui.IsMouseReleased(ImGuiMouseButton.Left)) return;
        selection.Clear(); selection.UnionWith(marqueeBase);
        if (Vector2.DistanceSquared(start, end) > 9)
            foreach (var b in current.Bars.Where(Drawn))
            {
                var pos = viewport + new Vector2(b.X + b.OffsetX, b.Y + b.OffsetY);
                if (pos.X < max.X && pos.Y < max.Y && pos.X + b.Width > min.X && pos.Y + b.Height > min.Y)
                    selection.UnionWith(Members(b.Id, current));
            }
        selected = selection.FirstOrDefault() ?? ""; marqueeStart = null;
    }
    private void DrawGroupControls(Snapshot current)
    {
        var bars = current.Bars.Where(b => b.Editable && selection.Contains(b.Id)).ToList();
        ImGui.TextUnformatted($"Выбрано элементов: {bars.Count}");
        if (ImGui.Button("Выбрать все"))
        {
            selection.UnionWith(current.Bars.Where(Drawn).Select(b => b.Id));
            selected = selection.FirstOrDefault() ?? "";
        }
        ImGui.SameLine();
        if (ImGui.Button("Снять выделение")) { selection.Clear(); selected = ""; }
        ImGui.BeginDisabled(bars.Count < 2);
        if (ImGui.Button("Сгруппировать"))
        {
            groups.RemoveAll(g => g.Any(selection.Contains)); groups.Add(bars.Select(b => b.Id).ToArray()); SaveGroups();
            status = "Группа создана. Клик по любой её панели выбирает группу.";
        }
        ImGui.EndDisabled(); ImGui.SameLine();
        ImGui.BeginDisabled(!groups.Any(g => g.Any(selection.Contains)));
        if (ImGui.Button("Разгруппировать")) { groups.RemoveAll(g => g.Any(selection.Contains)); SaveGroups(); status = "Группа разобрана."; }
        ImGui.EndDisabled();
        ImGui.BeginDisabled(bars.Count < 2);
        if (ImGui.BeginCombo("Выравнивание", "Выберите действие"))
        {
            var labels = new[] { "Левые края", "Центры по горизонтали", "Правые края", "Верхние края", "Центры по вертикали", "Нижние края" };
            for (var i = 0; i < labels.Length; i++) if (ImGui.Selectable(labels[i])) AlignSelection(bars, i);
            ImGui.EndCombo();
        }
        ImGui.EndDisabled();
        ImGui.BeginDisabled(bars.Count < 3);
        if (ImGui.Button("Равные интервалы X")) DistributeSelection(bars, true);
        ImGui.SameLine();
        if (ImGui.Button("Равные интервалы Y")) DistributeSelection(bars, false);
        ImGui.EndDisabled();
    }
    private void AlignSelection(List<Bar> bars, int mode)
    {
        if (bars.Count >= 2) ApplyTargets(bars, LayoutGeometry.Align(Rects(bars), mode));
    }
    private void DistributeSelection(List<Bar> bars, bool horizontal) => ApplyTargets(bars, LayoutGeometry.Distribute(Rects(bars), horizontal));
}
