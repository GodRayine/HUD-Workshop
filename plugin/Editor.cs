using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;

namespace HudWorkshop;

public sealed partial class Plugin
{
    private bool editorOpen;
    private bool cancellationPending;
    private Snapshot? baseline;
    private ulong editorOwner;
    private string selected = "_ActionBar";
    private string status = "Выберите панель мышью и перетащите её. Свойства появятся здесь.";
    private bool showGrid = true, snapGrid = true, snapPanels = true, showOffline;
    private string elementSearch="";
    private bool Drawn(Bar b)=>b.Editable&&(!b.Offline||showOffline||selection.Contains(b.Id));
    private int gridStep = 8, gap;
    private readonly Dictionary<string, Vector2> preview = new();
    private readonly Stack<Move[]> undo = new(), redo = new();
    private Move? gesture;
    private Vector2 dragMouse;
    private float? guideX, guideY;
    private sealed record Move(string Id, Vector2 Before, Vector2 After, float? BeforeScale = null, float? AfterScale = null, uint? BeforeOptions = null, uint? AfterOptions = null, bool? BeforeEnabled = null, bool? AfterEnabled = null);

    private bool Available => ClientState.IsLoggedIn && !Conditions[ConditionFlag.InCombat]
        && !Conditions[ConditionFlag.BetweenAreas] && !Conditions[ConditionFlag.BetweenAreas51];

    private void OpenEditor()
    {
        editorOpen = true;
        if (baseline != null || !Available) return;
        try
        {
            baseline = Read();
            if (baseline.LayoutIndex is < 0 or > 3) throw new InvalidOperationException("Раскладка HUD недоступна.");
            editorOwner = PlayerState.ContentId;
            preview.Clear(); previewScales.Clear(); previewOptions.Clear(); previewEnabled.Clear(); lastReady.Clear(); undo.Clear(); redo.Clear(); gesture = null;
            selection.Clear();
            LoadGroups();
            selected = baseline.Bars.FirstOrDefault(b => b.Editable)?.Id ?? "_ActionBar";
            selection.Add(selected);
            status = "Перетащите рамку элемента. Alt временно отключает привязки.";
        }
        catch (Exception ex) { baseline = null; status = ex.Message; Log.Error(ex, "Cannot open editor"); }
    }

    private void CloseEditor()
    {
        if (!CancelPreview()) { editorOpen = true; editorWindow.IsOpen = true; return; }
        baseline = null; editorOpen = false; editorWindow.IsOpen = false; gesture = null;
    }

    private bool CancelPreview()
    {
        if (baseline == null) return true;
        var errors = new List<Exception>();
        var recovered = new List<string>();
        try
        {
            var now = Read();
            if (PlayerState.ContentId == editorOwner && now.LayoutIndex == baseline.LayoutIndex)
            {
                foreach (var (id, expected) in preview)
                {
                    var actual = now.Bars.First(b => b.Id == id);
                    var original = baseline.Bars.First(b => b.Id == id);
                    var actions = new List<Action>();
                    // Offline previews are local; loaded fields are restored only while still owned.
                    if (actual.Ready)
                    {
                        if (previewEnabled.TryGetValue(id, out var expectedEnabled) && actual.Enabled == expectedEnabled)
                            actions.Add(() => { EnableNative(id, original.Enabled, original.Visible); previewEnabled.Remove(id); });
                        if (previewOptions.TryGetValue(id, out var expectedOptions) && OptionsMatch(actual, expectedOptions))
                            actions.Add(() => { OptionsNative(id, OriginalOptions(original)); previewOptions.Remove(id); });
                        if (previewScales.TryGetValue(id, out var expectedScale) && Math.Abs(actual.Scale - expectedScale) < .001f)
                            actions.Add(() => { ScaleNative(id, original.Scale, false); previewScales.Remove(id); });
                        if (actual.X == expected.X && actual.Y == expected.Y)
                            actions.Add(() => MoveNative(id, original.X, original.Y, false));
                    }
                    var failures = RecoveryBatch.AttemptAll(actions);
                    if (failures.Count == 0) recovered.Add(id);
                    else errors.AddRange(failures);
                }
            }
            else recovered.AddRange(preview.Keys); // Never write a previous character's session into another.
        }
        catch (Exception ex) { errors.Add(ex); }
        foreach (var id in recovered)
        {
            preview.Remove(id); previewScales.Remove(id); previewOptions.Remove(id); previewEnabled.Remove(id);
        }
        undo.Clear(); redo.Clear(); gestureStart.Clear(); gesture = null; guideX = guideY = null;
        if (errors.Count > 0)
        {
            cancellationPending = true;
            Log.Error(new AggregateException(errors), "Preview cancellation incomplete; retaining failed entries");
            status = "Отмена не завершена. Данные сохранены для повторной попытки: нажмите «Отменить всё».";
            return false;
        }
        lastReady.Clear();
        cancellationPending = false;
        return true;
    }

    private void SetPreview(string id, Vector2 position)
    {
        var addon = GameGui.GetAddonByName(id);
        if(NativeReady(id)){MoveNative(id, position.X, position.Y, false); preview[id]=new(addon.X,addon.Y);}
        else preview[id]=new(MathF.Round(position.X),MathF.Round(position.Y));
        status = "Есть несохранённые изменения.";
    }

    private void CommitGesture()
    {
        if (gesture is not { } move) return;
        var changes = gestureStart.Select(b => new Move(b.Id, new(b.X, b.Y), preview.GetValueOrDefault(b.Id, new(b.X, b.Y))))
            .Where(m => m.Before != m.After).ToArray();
        if (changes.Length > 0)
        {
            undo.Push(changes); redo.Clear();
            foreach (var change in changes) Log.Information("HUD preview moved {Id}: {Before} -> {After}", change.Id, change.Before, change.After);
        }
        gestureStart.Clear();
        gesture = null; guideX = guideY = null;
    }

    private void EditPosition(Bar bar, Vector2 position)
    {
        var current = Read();
        var delta = position - new Vector2(bar.X, bar.Y);
        ApplyBatch(current.Bars.Where(b => b.Editable && selection.Contains(b.Id))
            .Select(b => new Move(b.Id, new(b.X, b.Y), new Vector2(b.X, b.Y) + delta)).ToArray());
    }

    private void DrawEditor()
    {
        if (!editorOpen) return;
        try
        {
            if (cancellationPending)
            {
                inspectorSnapshot = null;
                editorWindow.IsOpen = true;
                using (var recoveryTheme = new WorkshopTheme()) windowSystem.Draw();
                if (!editorWindow.IsOpen) CloseEditor();
                return;
            }
            if (!Available)
            {
                if (!CancelPreview()) return;
                baseline = null;
                status = "Редактирование приостановлено: бой, переход или персонаж не загружен.";
            }
            if (baseline == null && Available) OpenEditor();
            Snapshot? current = baseline == null ? null : Read();
            if (current != null && baseline != null && (PlayerState.ContentId != editorOwner || current.LayoutIndex != baseline.LayoutIndex
                || current.ViewportWidth != baseline.ViewportWidth || current.ViewportHeight != baseline.ViewportHeight))
            {
                if (!CancelPreview()) return;
                baseline = null; current = null;
                status = "Раскладка или размер экрана изменились. Сессия завершена.";
            }
            if (current != null && baseline != null)
            {
                current=ReconcileLoaded(current);
                if (current.Bars.Any(b => preview.ContainsKey(b.Id) && (!b.Editable
                    || (previewOptions.TryGetValue(b.Id,out var options) ? !OptionsMatch(b,options) : b.SimpleGauge != baseline.Bars.First(o => o.Id == b.Id).SimpleGauge))))
                {
                    if (!CancelPreview()) return;
                baseline = null; current = null;
                    status = "Редактируемый элемент исчез. Несохранённые изменения отменены.";
                }
                else
                {
                    // A target/cast/party widget can appear after the session starts. Capture its real origin before editing.
                    baseline = baseline with { Bars = baseline.Bars.Select(original =>
                    {
                        var live = current.Bars.First(b => b.Id == original.Id);
                        return !preview.ContainsKey(original.Id) && (original.Ready != live.Ready || original.Visible != live.Visible || original.SimpleGauge != live.SimpleGauge || original.ModeApplicable != live.ModeApplicable)
                            ? live : original;
                    }).ToList() };
                    selection.RemoveWhere(id => !current.Bars.Any(b => b.Id == id && b.Editable));
                    if (!selection.Contains(selected)) selected = selection.FirstOrDefault() ?? "";
                }
            }
            var wasDragging = gesture != null;
            if (current != null) DrawHandles(current);
            inspectorSnapshot = current;
            editorWindow.IsOpen = editorOpen;
            // Escape cancels an active drag first; otherwise the native closing order applies.
            editorWindow.RespectCloseHotkey = !wasDragging;
            using (var theme = new WorkshopTheme()) windowSystem.Draw();
            if (!editorWindow.IsOpen) CloseEditor();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Editor operation failed");
            if (CancelPreview()) { baseline = null; status = "Операция отменена: " + ex.Message; }
        }
    }

    private void DrawHandles(Snapshot current)
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.Pos);
        ImGui.SetNextWindowSize(viewport.Size);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        ImGui.Begin("###HudWorkshopCanvas", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoBringToFrontOnFocus);
        try
        {
        var draw = ImGui.GetWindowDrawList();
        if (showGrid)
        {
            var step = Math.Max(8, gridStep);
            for (var x = 0; x < viewport.Size.X; x += step) draw.AddLine(viewport.Pos + new Vector2(x, 0), viewport.Pos + new Vector2(x, viewport.Size.Y), 0x187f9dab);
            for (var y = 0; y < viewport.Size.Y; y += step) draw.AddLine(viewport.Pos + new Vector2(0, y), viewport.Pos + new Vector2(viewport.Size.X, y), 0x187f9dab);
        }
        string? hovered = null;
        foreach (var bar in current.Bars.Where(Drawn).OrderByDescending(b => selection.Contains(b.Id)))
        {
            var pos = viewport.Pos + new Vector2(bar.X + bar.OffsetX, bar.Y + bar.OffsetY);
            var size = new Vector2(bar.Width, bar.Height);
            ImGui.SetCursorScreenPos(pos);
            ImGui.InvisibleButton("handle" + bar.Id, size);
            if (ImGui.IsItemHovered()) hovered = bar.Id;
            if (ImGui.IsItemActivated())
            {
                SelectPanel(bar.Id, ImGui.GetIO().KeyShift, current);
                if (selection.Contains(bar.Id))
                {
                gestureStart = current.Bars.Where(b => b.Editable && selection.Contains(b.Id)).ToList();
                gesture = new(bar.Id, new(bar.X, bar.Y), new(bar.X, bar.Y));
                dragMouse = ImGui.GetMousePos();
                }
            }
            if (ImGui.IsItemActive() && gesture?.Id == bar.Id && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 2))
            {
                DragSelection(current);
            }

            if (ImGui.IsItemDeactivated() && gesture?.Id == bar.Id)
            {
                // Include the release position even if the last mouse motion and release share a frame.
                var delta = ImGui.GetMousePos() - dragMouse;
                if (delta.LengthSquared() > 4)
                    DragSelection(current);
                CommitGesture();
            }
        }
        // Selected handles receive input first and are painted last when inactive frames overlap.
        foreach(var bar in current.Bars.Where(Drawn).OrderBy(b=>selection.Contains(b.Id)))
        {
            var pos=viewport.Pos+new Vector2(bar.X+bar.OffsetX,bar.Y+bar.OffsetY);
            var size=new Vector2(bar.Width,bar.Height);
            var isSelected = selection.Contains(bar.Id);
            var isHovered = hovered == bar.Id;
            var tint = isSelected ? WorkshopTheme.Accent : !bar.Enabled ? WorkshopTheme.Muted
                : isHovered ? WorkshopTheme.Text : bar.Offline ? WorkshopTheme.Warm : WorkshopTheme.Frame;
            var color = ImGui.ColorConvertFloat4ToU32(tint);
            draw.AddRectFilled(pos, pos + size, ImGui.ColorConvertFloat4ToU32(new Vector4(tint.X, tint.Y, tint.Z, isSelected ? .12f : isHovered ? .08f : .025f)), 3);
            draw.AddRect(pos, pos + size, color, 3, ImDrawFlags.None, isSelected ? 2 : 1);
            var label = bar.Label + ElementState(bar);
            var labelHeight = Math.Min(ImGui.GetTextLineHeight() + 6, size.Y);
            var labelWidth = Math.Min(size.X, ImGui.CalcTextSize(label).X + 16);
            draw.AddRectFilled(pos, pos + new Vector2(labelWidth, labelHeight), 0xf5221b17u, 3);
            draw.PushClipRect(pos, pos + size, true);
            draw.AddText(pos + new Vector2(8, 3), color, label);
            draw.PopClipRect();
        }
        DrawMarquee(current, viewport.Pos, draw);
        if (guideX is { } gx) draw.AddLine(viewport.Pos + new Vector2(gx, 0), viewport.Pos + new Vector2(gx, viewport.Size.Y), ImGui.ColorConvertFloat4ToU32(WorkshopTheme.Accent), 1);
        if (guideY is { } gy) draw.AddLine(viewport.Pos + new Vector2(0, gy), viewport.Pos + new Vector2(viewport.Size.X, gy), ImGui.ColorConvertFloat4ToU32(WorkshopTheme.Accent), 1);
        if (ImGui.IsKeyPressed(ImGuiKey.Escape) && gesture is { } active)
        {
            foreach (var b in gestureStart) SetPreview(b.Id, new(b.X, b.Y));
            gestureStart.Clear(); gesture = null; guideX = guideY = null;
        }
        }
        finally { ImGui.End(); ImGui.PopStyleVar(2); }
    }

    private Vector2 SnapPosition(Bar moving, Vector2 raw, Snapshot current)
    {
        guideX = guideY = null;
        if (ImGui.GetIO().KeyAlt) return raw;
        var offset = new Vector2(moving.OffsetX, moving.OffsetY);
        raw += offset;
        var x = snapGrid ? MathF.Round(raw.X / gridStep) * gridStep : raw.X;
        var y = snapGrid ? MathF.Round(raw.Y / gridStep) * gridStep : raw.Y;
        var bestX = 9f; var bestY = 9f;
        if (snapPanels)
            foreach (var originalTarget in current.Bars.Where(b => Drawn(b) && b.Id != moving.Id && !selection.Contains(b.Id)))
            {
                var target = originalTarget with { X = originalTarget.X + originalTarget.OffsetX, Y = originalTarget.Y + originalTarget.OffsetY };
                var xs = new List<float> { target.X, target.X + target.Width - moving.Width, target.X + (target.Width - moving.Width) / 2 };
                var ys = new List<float> { target.Y, target.Y + target.Height - moving.Height, target.Y + (target.Height - moving.Height) / 2 };
                if (raw.Y < target.Y + target.Height + 8 && raw.Y + moving.Height > target.Y - 8)
                    xs.AddRange([target.X + target.Width + gap, target.X - moving.Width - gap]);
                if (raw.X < target.X + target.Width + 8 && raw.X + moving.Width > target.X - 8)
                    ys.AddRange([target.Y + target.Height + gap, target.Y - moving.Height - gap]);
                foreach (var value in xs) if (Math.Abs(value - raw.X) < bestX) { bestX = Math.Abs(value - raw.X); x = value; guideX = value; }
                foreach (var value in ys) if (Math.Abs(value - raw.Y) < bestY) { bestY = Math.Abs(value - raw.Y); y = value; guideY = value; }
            }
        return new(MathF.Round(x - offset.X), MathF.Round(y - offset.Y));
    }

    private void DrawInspector(Snapshot? current)
    {
        if (cancellationPending)
        {
            ImGui.TextWrapped(status);
            if (ImGui.Button("Повторить отмену") && CancelPreview())
            {
                baseline = null;
                status = "Отмена завершена.";
            }
            return;
        }
            ImGui.TextColored(WorkshopTheme.Accent, "HUD WORKSHOP");
            ImGui.SameLine();
            ImGui.TextDisabled(current == null ? "Приостановлено" : $"Раскладка {current.LayoutIndex + 1}");
            ImGui.TextColored(preview.Count > 0 ? WorkshopTheme.Warm : WorkshopTheme.Muted,
                preview.Count > 0 ? "Есть несохранённые изменения" : "Можно редактировать");
            ImGui.Spacing();
            // Keep commit and history controls visible while the inspector scrolls.
            var footerHeight = ImGui.GetFrameHeightWithSpacing() * 3 + ImGui.GetTextLineHeightWithSpacing() * 3;
            ImGui.BeginChild("InspectorContent", new Vector2(0, -footerHeight));
            try
            {
                if (current != null)
                {
                    SectionTitle("ЭЛЕМЕНТ");
                    ImGui.SetNextItemWidth(-1);
                    ImGui.InputTextWithHint("##ElementSearch", "Найти элемент…", ref elementSearch, 100);
                    var bar = current.Bars.FirstOrDefault(b => b.Id == selected && selection.Contains(b.Id));
                    ImGui.SetNextItemWidth(-1);
                    if (ImGui.BeginCombo("##ElementPicker", bar?.Label ?? "Выберите элемент на экране"))
                    {
                        foreach (var item in current.Bars.Where(b => b.Editable && b.Label.Contains(elementSearch, StringComparison.OrdinalIgnoreCase)))
                            if (ImGui.Selectable(item.Label + ElementState(item), selection.Contains(item.Id)))
                                SelectPanel(item.Id, ImGui.GetIO().KeyShift, current);
                        ImGui.EndCombo();
                    }
                    ImGui.Spacing();
                    if (bar != null)
                    {
                        DrawEnableControls(bar);
                        ImGui.SameLine(); ImGui.TextDisabled($"{bar.Width:0.#} × {bar.Height:0.#} px");
                        if (selection.Count > 1) ImGui.TextWrapped("Свойства ниже относятся к основному выбранному элементу. Групповые действия — в секции «Выделение».");
                        if (bar.Offline) ImGui.TextWrapped("Элемент другого режима. Рамка построена по сохранённой раскладке.");
                        else if (bar.Placeholder && bar.Enabled) ImGui.TextDisabled("Неактивен • рамка показывает место появления");
                        SectionTitle("ПОЛОЖЕНИЕ И ВИД");
                        var x = (int)bar.X; var y = (int)bar.Y;
                        ImGui.SetNextItemWidth(145);
                        if (ImGui.InputInt("X", ref x)) EditPosition(bar, new(x, bar.Y));
                        ImGui.SameLine(); ImGui.SetNextItemWidth(145);
                        if (ImGui.InputInt("Y", ref y)) EditPosition(bar, new(bar.X, y));
                        if (!IsChat(bar.Id)) DrawScaleControls(bar);
                        ImGui.SetNextItemWidth(180);
                        DrawOptionControls(bar);
                        if (ImGui.Button("Выровнять по сетке", new Vector2(-1, 0))) AlignSelectionToGrid(current);
                    }
                    else ImGui.TextWrapped("Нажмите на рамку в игре или выберите элемент из списка. Его свойства появятся здесь.");
                    ImGui.Spacing();
                    if (ImGui.CollapsingHeader($"Выделение · {selection.Count}###SelectionSection", ImGuiTreeNodeFlags.DefaultOpen))
                        DrawGroupControls(current);
                }
                ImGui.Spacing();
                if (ImGui.CollapsingHeader("Сетка и привязки", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.Checkbox("Показывать сетку", ref showGrid);
                    ImGui.Checkbox("Привязка к сетке", ref snapGrid);
                    ImGui.SameLine(); ImGui.SetNextItemWidth(90);
                    ImGui.InputInt("Шаг", ref gridStep); gridStep = Math.Clamp(gridStep, 1, 128);
                    ImGui.Checkbox("Приклеивать панели", ref snapPanels);
                    ImGui.SameLine(); ImGui.SetNextItemWidth(90);
                    ImGui.InputInt("Зазор", ref gap); gap = Math.Clamp(gap, -64, 128);
                }
                if (ImGui.CollapsingHeader("Отображение элементов"))
                {
                    ImGui.Checkbox("Элементы других режимов", ref showOffline);
                    if (current != null)
                    {
                        ImGui.TextDisabled("Без доступных данных:");
                        foreach (var unavailable in current.Bars.Where(b => !b.Editable && b.ModeApplicable))
                            ImGui.TextDisabled(unavailable.Label);
                    }
                }
                if (current != null) DrawSharing(current);
                if (ImGui.CollapsingHeader("Управление"))
                {
                    ImGui.TextWrapped("Shift + клик — добавить или убрать элемент из выделения. Перетаскивание по пустому месту — выделить рамкой. Alt — временно отключить привязки. Esc — отменить текущее перетаскивание.");
                }
            }
            finally { ImGui.EndChild(); }
            ImGui.Separator();
            ImGui.BeginDisabled(current == null);
            try
            {
                var half = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2;
                ImGui.BeginDisabled(undo.Count == 0);
                if (ImGui.Button("Отменить шаг", new Vector2(half, 0))) { var move = undo.Pop(); ApplyMoves(move, false); redo.Push(move); }
                ImGui.EndDisabled(); ImGui.SameLine();
                ImGui.BeginDisabled(redo.Count == 0);
                if (ImGui.Button("Повторить", new Vector2(half, 0))) { var move = redo.Pop(); ApplyMoves(move, true); undo.Push(move); }
                ImGui.EndDisabled();
                ImGui.PushStyleColor(ImGuiCol.Button, WorkshopTheme.AccentButton);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, WorkshopTheme.AccentHover);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, WorkshopTheme.AccentActive);
                bool save;
                try { save = ImGui.Button("Сохранить HUD", new Vector2(half, 36)); }
                finally { ImGui.PopStyleColor(3); }
                if (save && baseline != null)
                {
                    CommitGesture(); SavePositions(baseline, preview, previewScales, previewOptions, previewEnabled);
                    baseline = Read(); preview.Clear(); previewScales.Clear(); previewOptions.Clear(); previewEnabled.Clear(); lastReady.Clear(); undo.Clear(); redo.Clear();
                    status = "HUD сохранён в настройках игры.";
                }
                ImGui.SameLine();
                if (ImGui.Button("Отменить всё", new Vector2(half, 36)) && CancelPreview()) status = "Исходное положение восстановлено.";
            }
            finally { ImGui.EndDisabled(); }
            ImGui.TextWrapped(status);
            ImGui.TextDisabled("Закрытие окна отменяет несохранённые изменения.");
    }
}
