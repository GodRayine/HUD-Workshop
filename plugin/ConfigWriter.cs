using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace HudWorkshop;

public sealed partial class Plugin
{
    private static unsafe Span<AddonConfigEntry> EntriesFor(AddonConfig* config,int layout,string id) => IsChat(id)
        ? config->ActiveDataSet->ConfigEntries : config->ActiveDataSet->HudLayoutConfigEntries.Slice(layout*112,112);

    private unsafe void RestoreSaved(Snapshot baseline)
    {
        if (typeof(Dalamud.Plugin.IDalamudPlugin).Assembly.GetName().Version?.ToString() != "15.0.3.4" || baseline.LayoutIndex is < 0 or > 3)
            throw new InvalidOperationException("Несовместимая версия или раскладка HUD.");
        var config = AddonConfig.Instance();
        if (config == null || !config->IsLoaded || config->ActiveDataSet == null) throw new InvalidOperationException("HUD недоступен");
        foreach (var bar in baseline.Bars.Where(b => b.Saved != null))
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
        var directory = Path.Combine(PluginInterface.GetPluginConfigDirectory(), "diagnostics");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"before-write-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json"), System.Text.Json.JsonSerializer.Serialize(baseline, JsonOptions));
        var config = AddonConfig.Instance();
        try
        {
            foreach (var (id, target) in positions)
            {
                var original = baseline.Bars.First(b => b.Id == id);
                foreach (ref var entry in EntriesFor(config,baseline.LayoutIndex,id))
                    if (entry.HasValue && entry.AddonNameHash == Crc32(id + "_a"))
                    {
                        var saved = original.Saved!;
                        var live=now.Bars.First(b=>b.Id==id);
                        var targetScale=scales.GetValueOrDefault(id,original.Scale);
                        var width=options.ContainsKey(id)?live.NativeWidth:saved.Width;
                        var height=options.ContainsKey(id)?live.NativeHeight:saved.Height;
                        var compensation=Vector2.Zero;
                        if(scales.ContainsKey(id)||options.ContainsKey(id))
                        {
                            compensation=ScaleGeometry.AnchorCompensation(saved.Byte1,
                                new Vector2(saved.Width,saved.Height)*saved.Scale,new Vector2(width,height)*targetScale);
                        }
                        entry.X = saved.X + (target.X - original.X + compensation.X) * 100 / baseline.ViewportWidth;
                        entry.Y = saved.Y + (target.Y - original.Y + compensation.Y) * 100 / baseline.ViewportHeight;
                        if (scales.TryGetValue(id, out var scale)) entry.Scale = scale;
                        if(options.TryGetValue(id,out var flags)){entry.ElementFlags=flags;entry.Width=width;entry.Height=height;}
                        if(!IsChat(id) && enabled.TryGetValue(id,out var state))entry.ByteValue2=OfflineGeometry.EnabledByte(entry.ByteValue2,state);
                        break;
                    }
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
            config->HasChanges = true;
            config->SaveFile(true);
            SaveChatVisibility(enabled);
            Log.Information("HUD native save verified in memory: layout {Layout}, {Count} panels", baseline.LayoutIndex + 1, positions.Count);
        }
        catch { RestoreSaved(baseline); throw; }
    }
}
