using Dalamud.Bindings.ImGui;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace HudWorkshop;
public sealed partial class Plugin
{
    private readonly Dictionary<string,bool> previewEnabled=new();
    private static unsafe void EnableNative(string id,bool enabled,bool? restoreVisible=null)
    {
        if(typeof(Dalamud.Plugin.IDalamudPlugin).Assembly.GetName().Version?.ToString()!="15.0.3.4" || !Names.Contains(id))throw new InvalidOperationException("Несовместимый элемент.");
        var a=GameGui.GetAddonByName(id);if(!NativeReady(id))return;
        var unit=(AtkUnitBase*)a.Address;
        unit->VisibilityFlags=enabled?(ushort)(unit->VisibilityFlags&~0x8001):(ushort)(unit->VisibilityFlags|0x8001);
        if(enabled && restoreVisible!=false)unit->Show(true,0);else unit->Hide(true,false,0);
    }
    private void SetEnabledPreview(string id,bool enabled)
    {
        EnableNative(id,enabled);previewEnabled[id]=enabled;
        status="Есть несохранённые изменения.";
    }
    private void DrawEnableControls(Bar bar)
    {
        var enabled=bar.Enabled;
        if(ImGui.Checkbox("Включён",ref enabled))
        {
            var pos=new Vector2(bar.X,bar.Y);
            ApplyBatch([new Move(bar.Id,pos,pos,BeforeEnabled:bar.Enabled,AfterEnabled:enabled)]);
        }
    }
}
