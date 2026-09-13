using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace HudWorkshop;

public sealed partial class Plugin
{
    private string layoutCode = "";
    private string shareStatus = "";
    private SharedLayout? checkedLayout;
    private bool adaptResolution = true;

    private Move[] ImportMoves(SharedLayout layout, Snapshot current, out int skipped)
    {
        LayoutCode.Validate(layout);
        skipped = 0;
        var moves = new List<Move>();
        foreach (var entry in layout.Elements)
        {
            var bar = current.Bars.FirstOrDefault(b => b.Id == entry.Id && b.Editable && b.Saved != null);
            if (bar == null) { skipped++; continue; }
            // Only semantic options are shared. Never accept native flag words from an import.
            if ((entry.Shape != null && bar.HotbarLayout == null) || (entry.Simple != null && bar.SimpleGauge == null)
                || (entry.Scale != null && IsChat(bar.Id)))
                throw new FormatException("Несовместимые свойства: " + bar.Label);
            var x = entry.X * (adaptResolution ? (float)current.ViewportWidth / layout.Width : 1);
            var y = entry.Y * (adaptResolution ? (float)current.ViewportHeight / layout.Height : 1);
            if (!float.IsFinite(x) || !float.IsFinite(y) || x < short.MinValue || x > short.MaxValue || y < short.MinValue || y > short.MaxValue)
                throw new FormatException("Координаты выходят за допустимый диапазон: " + bar.Label);
            uint? before = null, after = null;
            if (entry.Shape is {} shape)
            {
                before = OriginalOptions(bar); after = (before.Value & ~0xff00u) | ((uint)(shape + 1) << 8);
            }
            if (entry.Simple is {} simple)
            {
                before = OriginalOptions(bar); after = (before.Value & ~1u) | (simple ? 1u : 0u);
            }
            var scaleChanged = entry.Scale is {} scale && Math.Abs(scale - bar.Scale) > .001f;
            if (before == after) { before = null; after = null; }
            moves.Add(new Move(bar.Id, new(bar.X, bar.Y), new(x, y),
                scaleChanged ? bar.Scale : null, scaleChanged ? entry.Scale : null, before, after,
                bar.Enabled != entry.Enabled ? bar.Enabled : null, bar.Enabled != entry.Enabled ? entry.Enabled : null));
        }
        return moves.ToArray();
    }

    private void DrawSharing(Snapshot current)
    {
        if (!ImGui.CollapsingHeader("Импорт и экспорт")) return;
        ImGui.TextWrapped("Строка переносит положения, масштаб, форму и включение доступных элементов текущей раскладки, включая предпросмотр. Назначения способностей и содержимое чата в неё не входят.");
        if (ImGui.Button("Экспортировать и скопировать"))
        {
            try
            {
                var layout = new SharedLayout(1, (int)current.ViewportWidth, (int)current.ViewportHeight,
                    current.Bars.Where(b => b.Editable && b.Saved != null).Select(b => new SharedElement(
                        b.Id, b.X, b.Y, IsChat(b.Id) ? null : b.Scale, b.Enabled, b.HotbarLayout, b.SimpleGauge)).ToArray());
                layoutCode = LayoutCode.Encode(layout); checkedLayout = null;
                ImGui.SetClipboardText(layoutCode);
                shareStatus = $"Скопировано элементов: {layout.Elements.Length}. Можно отправить строку другому игроку.";
            }
            catch (Exception ex) { shareStatus = "Не удалось экспортировать: " + ex.Message; }
        }
        if (ImGui.Button("Вставить из буфера"))
        {
            var pasted = ImGui.GetClipboardText();
            checkedLayout = null;
            if (pasted.Length > LayoutCode.MaxText) shareStatus = "Строка слишком длинная.";
            else { layoutCode = pasted; shareStatus = "Нажмите «Проверить строку»."; }
        }
        if (ImGui.InputTextMultiline("##LayoutCode", ref layoutCode, LayoutCode.MaxText + 1, new Vector2(-1, 90)))
        { checkedLayout = null; shareStatus = ""; }
        if (ImGui.Checkbox("Подстроить координаты под разрешение", ref adaptResolution)) checkedLayout = null;
        if (ImGui.Button("Проверить строку"))
        {
            checkedLayout = null;
            try
            {
                var layout = LayoutCode.Decode(layoutCode);
                var moves = ImportMoves(layout, current, out var skipped);
                if (moves.Length == 0) throw new FormatException("Нет доступных совместимых элементов.");
                checkedLayout = layout;
                shareStatus = $"Разрешение: {layout.Width} × {layout.Height}. Доступно: {moves.Length}, пропущено: {skipped}.";
            }
            catch (Exception ex) { shareStatus = "Импорт отклонён: " + ex.Message; }
        }
        ImGui.BeginDisabled(checkedLayout == null);
        if (ImGui.Button("Применить предпросмотр"))
        {
            // Revalidate against the current frame; availability may have changed since checking.
            Move[]? moves = null;
            var skipped = 0;
            try
            {
                moves = ImportMoves(checkedLayout!, current, out skipped);
                if (moves.Length == 0) throw new FormatException("Совместимые элементы больше не доступны.");
            }
            catch (FormatException ex) { shareStatus = "Импорт отклонён: " + ex.Message; moves = null; }
            checkedLayout = null;
            if (moves != null)
            {
                CommitGesture();
                var previousSteps = undo.Count;
                ApplyBatch(moves);
                shareStatus = $"Пропущено недоступных элементов: {skipped}.";
                status = undo.Count == previousSteps ? "Раскладка уже совпадает со строкой. Изменений нет."
                    : "Импорт выполнен. Сохраните HUD или отмените шаг для возврата.";
            }
        }
        ImGui.EndDisabled();
        ImGui.TextWrapped(shareStatus);
        ImGui.TextDisabled("Импорт можно отменить одним шагом.");
        ImGui.TextWrapped("Группы редактора, режим общей/раздельной цели и статусов не переносятся. Недоступные здесь элементы пропускаются.");
    }
}
