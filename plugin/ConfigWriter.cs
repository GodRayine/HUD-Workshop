using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace HudWorkshop;

public sealed partial class Plugin
{
    private static unsafe Span<AddonConfigEntry> EntriesFor(AddonConfig* config,int layout,string id) => IsChat(id)
        ? config->ActiveDataSet->ConfigEntries : config->ActiveDataSet->HudLayoutConfigEntries.Slice(layout*112,112);

    private unsafe void RestoreSaved(Snapshot baseline, HashSet<string> changedIds)
    {
        if (typeof(Dalamud.Plugin.IDalamudPlugin).Assembly.GetName().Version?.ToString() != "15.0.3.4" || baseline.LayoutIndex is < 0 or > 3)
            throw new InvalidOperationException("Несовместимая версия или раскладка HUD.");
        var config = AddonConfig.Instance();
        if (PlayerState.ContentId != editorOwner || config == null || !config->IsLoaded || config->ActiveDataSet == null || config->ActiveDataSet->CurrentHudLayout != baseline.LayoutIndex) throw new InvalidOperationException("Персонаж или раскладка изменились; восстановление остановлено.");
        foreach (var bar in baseline.Bars.Where(b => b.Saved != null && changedIds.Contains(b.Id)))
            foreach (ref var entry in EntriesFor(config,baseline.LayoutIndex,bar.Id))
                if (entry.HasValue && entry.AddonNameHash == Crc32(bar.Id + "_a")) { entry.X = bar.Saved!.X; entry.Y = bar.Saved.Y; entry.Scale = bar.Saved.Scale; entry.ElementFlags=bar.Saved.Flags; entry.Width=bar.Saved.Width; entry.Height=bar.Saved.Height; entry.ByteValue2=bar.Saved.Byte2; break; }
        if (config->ActiveDataSet->CurrentHudLayout == baseline.LayoutIndex) config->ApplyHudLayout();
        foreach(var chat in baseline.Bars.Where(b=>IsChat(b.Id) && b.Ready && b.ModeApplicable)){MoveNative(chat.Id,chat.X,chat.Y,false);EnableNative(chat.Id,chat.Enabled,chat.Visible);}
        config->HasChanges = true;
        config->SaveFile(true);
    }

    private unsafe void SavePositions(Snapshot baseline, Dictionary<string, Vector2> positions, Dictionary<string, float> scales, Dictionary<string,uint> options, Dictionary<string,bool> enabled)
    {
        if (positions.Count == 0) return;
        foreach (var (id, scale) in scales)
            if (!positions.ContainsKey(id) || !float.IsFinite(scale) || scale < .5f || scale > 3f)
                throw new InvalidOperationException("Некорректный масштаб элемента.");
        foreach(var id in options.Keys) if(!positions.ContainsKey(id))throw new InvalidOperationException("Нет позиции изменяемого элемента.");
        foreach(var id in enabled.Keys)if(!positions.ContainsKey(id))throw new InvalidOperationException("Нет позиции переключаемого элемента.");
        var now = Read();
        if (baseline.LayoutIndex is < 0 or > 3 || baseline.ViewportWidth <= 0 || baseline.ViewportHeight <= 0)
            throw new InvalidOperationException("HUD недоступен.");
        if (now.LayoutIndex != baseline.LayoutIndex || now.ViewportWidth != baseline.ViewportWidth || now.ViewportHeight != baseline.ViewportHeight)
            throw new InvalidOperationException("Раскладка или разрешение изменились. Откройте редактор заново.");
        foreach (var original in baseline.Bars)
            if (now.Bars.First(b => b.Id == original.Id).Saved != original.Saved)
                throw new InvalidOperationException("Настройки HUD изменены другим редактором. Сохранение остановлено.");
        foreach (var id in positions.Keys)
            if (baseline.Bars.First(b => b.Id == id).Saved is null) throw new InvalidOperationException("Нет записи панели в HUD.");
        foreach (var (id, target) in positions)
            if (!Names.Contains(id) || !float.IsFinite(target.X) || !float.IsFinite(target.Y)
                || target.X < short.MinValue || target.X > short.MaxValue || target.Y < short.MinValue || target.Y > short.MaxValue)
                throw new InvalidOperationException("Некорректная позиция панели.");
        foreach (var original in baseline.Bars.Where(b => b.Editable))
        {
            var actual = now.Bars.First(b => b.Id == original.Id);
            var expected = positions.GetValueOrDefault(original.Id, new(original.X, original.Y));
            if (!actual.Editable || actual.Enabled!=enabled.GetValueOrDefault(original.Id,original.Enabled) || actual.X != expected.X || actual.Y != expected.Y
                || Math.Abs(actual.Scale - scales.GetValueOrDefault(original.Id, original.Scale)) > .001f
                || (options.TryGetValue(original.Id,out var flags) ? !OptionsMatch(actual,flags) : actual.SimpleGauge != original.SimpleGauge))
                throw new InvalidOperationException("Положение панели изменилось извне. Откройте редактор заново.");
        }
        // Compute every value before the first write: a bad anchor must not leave a partial batch.
        var planned = new Dictionary<string, SavedEntry>();
        foreach (var (id, target) in positions)
        {
            var original = baseline.Bars.First(b => b.Id == id);
            var saved = original.Saved!;
            var live = now.Bars.First(b => b.Id == id);
            var targetScale = scales.GetValueOrDefault(id, original.Scale);
            var width = options.ContainsKey(id) ? live.NativeWidth : saved.Width;
            var height = options.ContainsKey(id) ? live.NativeHeight : saved.Height;
            var compensation = scales.ContainsKey(id) || options.ContainsKey(id)
                ? ScaleGeometry.AnchorCompensation(saved.Byte1, new Vector2(saved.Width, saved.Height) * saved.Scale, new Vector2(width, height) * targetScale)
                : Vector2.Zero;
            var x = saved.X + (target.X - original.X + compensation.X) * 100 / baseline.ViewportWidth;
            var y = saved.Y + (target.Y - original.Y + compensation.Y) * 100 / baseline.ViewportHeight;
            if (!float.IsFinite(x) || !float.IsFinite(y)) throw new InvalidOperationException("Некорректные координаты сохранения.");
            planned[id] = saved with { X = x, Y = y, Scale = targetScale, Width = width, Height = height,
                Flags = options.GetValueOrDefault(id, saved.Flags),
                Byte2 = !IsChat(id) && enabled.TryGetValue(id, out var state) ? OfflineGeometry.EnabledByte(saved.Byte2, state) : saved.Byte2 };
        }
        var changedIds = planned.Keys.ToHashSet();
        SaveTransaction.Run(() =>
        {
            var config = AddonConfig.Instance();
            if (PlayerState.ContentId != editorOwner || config == null || !config->IsLoaded || config->ActiveDataSet == null || config->ActiveDataSet->CurrentHudLayout != baseline.LayoutIndex)
                throw new InvalidOperationException("HUD недоступен для записи.");
            foreach (var (id, saved) in planned)
            {
                var found = false;
                foreach (ref var entry in EntriesFor(config, baseline.LayoutIndex, id))
                {
                    if (!entry.HasValue || entry.AddonNameHash != Crc32(id + "_a")) continue;
                    entry.X = saved.X; entry.Y = saved.Y; entry.Scale = saved.Scale;
                    entry.ElementFlags = saved.Flags; entry.Width = saved.Width; entry.Height = saved.Height; entry.ByteValue2 = saved.Byte2;
                    found = true;
                    break;
                }
                if (!found) throw new InvalidOperationException("Запись элемента исчезла: " + id);
            }
            config->ApplyHudLayout();
            foreach(var (id,state) in enabled)EnableNative(id,state);
            var applied = Read();
            foreach (var original in baseline.Bars.Where(b => b.Editable))
            {
                var expected = positions.GetValueOrDefault(original.Id, new(original.X, original.Y));
                var actual = applied.Bars.First(b => b.Id == original.Id);
                if (!actual.Editable || actual.Enabled!=enabled.GetValueOrDefault(original.Id,original.Enabled) || Math.Abs(actual.X - expected.X) > 1 || Math.Abs(actual.Y - expected.Y) > 1
                    || Math.Abs(actual.Scale - scales.GetValueOrDefault(original.Id, original.Scale)) > .001f
                    || (options.TryGetValue(original.Id,out var flags) && !OptionsMatch(actual,flags)))
                    throw new InvalidOperationException($"Применение {original.Label} не совпало с ожидаемой позицией: {actual.X}/{actual.Y} вместо {expected.X}/{expected.Y}");
            }
        }, () =>
        {
            var config = AddonConfig.Instance();
            if (PlayerState.ContentId != editorOwner || config == null || !config->IsLoaded || config->ActiveDataSet == null || config->ActiveDataSet->CurrentHudLayout != baseline.LayoutIndex)
                throw new InvalidOperationException("HUD недоступен для сохранения.");
            config->HasChanges = true;
            config->SaveFile(true);
            SaveChatVisibility(enabled);
        }, () => RestoreSaved(baseline, changedIds));
        Log.Information("HUD native save verified in memory: layout {Layout}, {Count} panels", baseline.LayoutIndex + 1, positions.Count);
    }
}
