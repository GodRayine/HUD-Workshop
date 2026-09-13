using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace HudWorkshop;

public sealed partial class Plugin
{
    private readonly Dictionary<string, float> previewScales = new();
    private static readonly int[] ScalePercentages = [60, 80, 90, 100, 110, 120, 140, 160, 180, 200];

    private static unsafe void ScaleNative(string id, float scale, bool requireVisible = true)
    {
        if (typeof(IDalamudPlugin).Assembly.GetName().Version?.ToString() != "15.0.3.4") throw new NotSupportedException("Версия Dalamud изменилась.");
        if (!float.IsFinite(scale) || scale < .5f || scale > 3f) throw new ArgumentOutOfRangeException(nameof(scale));
        var addon = GameGui.GetAddonByName(id);
        if (!Names.Contains(id) || addon.IsNull || !addon.IsReady || (requireVisible && !addon.IsVisible)) throw new InvalidOperationException("Элемент недоступен.");
        ((AtkUnitBase*)addon.Address)->SetScale(scale, false);
    }

    private void SetScalePreview(string id, float scale)
    {
        var ready=GameGui.GetAddonByName(id);
        if(!NativeReady(id)){previewScales[id]=scale;return;}
        ScaleNative(id, scale, false);
        var actual = GameGui.GetAddonByName(id);
        previewScales[id] = actual.Scale;
        if (Math.Abs(actual.Scale - scale) > .001f) throw new InvalidOperationException("Игра не приняла масштаб элемента.");
        status = "Есть несохранённые изменения.";
    }

    private void DrawScaleControls(Bar bar)
    {
        ImGui.SetNextItemWidth(180);
        if (!ImGui.BeginCombo("Масштаб элемента", $"{bar.Scale * 100:0}%")) return;
        try
        {
            foreach (var percent in ScalePercentages)
                if (ImGui.Selectable($"{percent}%", Math.Abs(bar.Scale * 100 - percent) < .1f))
                {
                    var position = new Vector2(bar.X, bar.Y);
                    ApplyBatch([new Move(bar.Id, position, position, bar.Scale, percent / 100f)]);
                }
        }
        finally { ImGui.EndCombo(); }
    }
}
