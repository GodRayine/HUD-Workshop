using System.Numerics;
namespace HudWorkshop;
public sealed partial class Plugin
{
    private readonly Dictionary<string,bool> lastReady=new();
    private static unsafe bool NativeReady(string id)
    {
        var addon=GameGui.GetAddonByName(id);
        if(addon.IsNull || !addon.IsReady)return false;
        if(!id.StartsWith("JobHud"))return true;
        var job=(FFXIVClientStructs.FFXIV.Client.UI.AddonJobHud*)addon.Address;
        return job->IsGaugeInitialized && job->IsConfigLoaded;
    }
    private Bar ProjectOffline(Bar bar,Vector2 viewport)
    {
        if(bar.Ready || bar.Saved is not { } saved || saved.Width==0 || saved.Height==0 || saved.Byte1>8 || saved.Scale<=0 || !float.IsFinite(saved.Scale) || !float.IsFinite(saved.X) || !float.IsFinite(saved.Y))return bar;
        var origin=OfflineGeometry.Origin(new(saved.X,saved.Y),new(saved.Width,saved.Height),saved.Scale,saved.Byte1,viewport);
        var position=preview.GetValueOrDefault(bar.Id,origin);
        var scale=previewScales.GetValueOrDefault(bar.Id,saved.Scale);
        var flags=previewOptions.GetValueOrDefault(bar.Id,saved.Flags);
        return bar with { Offline=true,Placeholder=true,X=position.X,Y=position.Y,Scale=scale,
            Width=saved.Width*scale+12,Height=saved.Height*scale+12,OffsetX=-6,OffsetY=-6,
            NativeWidth=saved.Width,NativeHeight=saved.Height,
            SimpleGauge=bar.Id.StartsWith("JobHud")?(flags&1)!=0:null,
            Enabled=previewEnabled.GetValueOrDefault(bar.Id,(saved.Byte2&1)!=0) };
    }

    private Snapshot ReconcileLoaded(Snapshot current)
    {
        var changed=false;
        foreach(var bar in current.Bars)
        {
            var wasReady=lastReady.GetValueOrDefault(bar.Id,baseline!.Bars.First(b=>b.Id==bar.Id).Ready);
            lastReady[bar.Id]=bar.Ready;
            if(!bar.Ready || wasReady || !preview.TryGetValue(bar.Id,out var pos))continue;
            if(bar.Saved!=baseline!.Bars.First(b=>b.Id==bar.Id).Saved)throw new InvalidOperationException("Настройки появившегося элемента изменились извне.");
            MoveNative(bar.Id,pos.X,pos.Y,false);
            if(previewScales.TryGetValue(bar.Id,out var scale))ScaleNative(bar.Id,scale,false);
            if(previewOptions.TryGetValue(bar.Id,out var flags))OptionsNative(bar.Id,flags);
            if(previewEnabled.TryGetValue(bar.Id,out var enabled))EnableNative(bar.Id,enabled);
            changed=true;
        }
        return changed?Read():current;
    }
}
