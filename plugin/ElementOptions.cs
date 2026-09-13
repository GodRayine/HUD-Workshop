using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
namespace HudWorkshop;
public sealed partial class Plugin
{
    private readonly Dictionary<string,uint> previewOptions = new();
    private static readonly string[] Shapes = ["12 × 1", "6 × 2", "4 × 3", "3 × 4", "2 × 6", "1 × 12"];
    private static uint OriginalOptions(Bar b) => b.HotbarLayout is { } shape ? (b.Saved!.Flags & ~0xFF00u) | ((uint)(shape+1)<<8)
        : b.SimpleGauge is { } simple ? (b.Saved!.Flags & ~1u) | (simple?1u:0u) : b.Saved!.Flags;
    private static bool OptionsMatch(Bar b,uint flags) => b.HotbarLayout is { } shape ? shape==(int)((flags>>8)&255)-1
        : b.SimpleGauge is { } simple && simple==((flags&1)!=0);
    private void SetOptionsPreview(string id,uint flags)
    {
        var before=Read().Bars.First(b=>b.Id==id);
        if(before.Offline){previewOptions[id]=flags;return;}
        previewOptions[id]=OriginalOptions(before);
        OptionsNative(id,flags);
        var after=Read().Bars.First(b=>b.Id==id);
        previewOptions[id]=OriginalOptions(after);
        if(!OptionsMatch(after,flags))throw new InvalidOperationException("Игра не применила настройку элемента.");
    }
    private void DrawOptionControls(Bar bar)
    {
        uint? flags=null;
        if(bar.HotbarLayout is >=0 and <6)
        {
            if(Dalamud.Bindings.ImGui.ImGui.BeginCombo("Форма панели",Shapes[bar.HotbarLayout.Value]))
            {
                for(var i=0;i<Shapes.Length;i++) if(Dalamud.Bindings.ImGui.ImGui.Selectable(Shapes[i],i==bar.HotbarLayout))
                    flags=(OriginalOptions(bar)&~0xFF00u)|((uint)(i+1)<<8);
                Dalamud.Bindings.ImGui.ImGui.EndCombo();
            }
        }
        if(bar.SimpleGauge is { } simple && Dalamud.Bindings.ImGui.ImGui.Checkbox("Упрощённая шкала",ref simple))
            flags=(OriginalOptions(bar)&~1u)|(simple?1u:0u);
        if(flags is { } value)
        {
            var pos=new System.Numerics.Vector2(bar.X,bar.Y);
            ApplyBatch([new Move(bar.Id,pos,pos,BeforeOptions:OriginalOptions(bar),AfterOptions:value)]);
        }
    }
    private static void OptionsNative(string id,uint flags) => LoadPropertiesNative(id,flags,null);
    private static unsafe void LoadPropertiesNative(string id, uint? flags, byte? enableBits)
    {
        if(typeof(Dalamud.Plugin.IDalamudPlugin).Assembly.GetName().Version?.ToString() != "15.0.3.4") throw new NotSupportedException("Версия Dalamud изменилась.");
        if(!Elements.Any(e=>e.Id==id)) throw new ArgumentException("Настройки элемента недоступны.");
        var addon=GameGui.GetAddonByName(id);
        if(addon.IsNull || !addon.IsReady) throw new InvalidOperationException("Элемент недоступен.");
        var config=AddonConfig.Instance();
        if(config==null || !config->IsLoaded || config->ActiveDataSet==null) throw new InvalidOperationException("HUD недоступен.");
        var layout=config->ActiveDataSet->CurrentHudLayout;
        if(layout is <0 or >3) throw new InvalidOperationException("Раскладка недоступна.");
        var general=config->GetConfigEntryByAddonName(id+"_a");
        if(general==null || !general->HasValue) throw new InvalidOperationException("Нет настроек элемента.");
        foreach(ref var entry in EntriesFor(config,layout,id))
        {
            if(!entry.HasValue || entry.AddonNameHash!=Crc32(id+"_a"))continue;
            var oldEntry=entry; var oldGeneral=*general;
            var unit=(AtkUnitBase*)addon.Address; var x=addon.X;var y=addon.Y;var scale=addon.Scale;var alpha=unit->Alpha;
            try
            {
                if(flags is { } value){entry.ElementFlags=value;general->ElementFlags=value;}
                if(enableBits is { } bits){entry.ByteValue2=bits;general->ByteValue2=bits;}
                short width=0,height=0;
                if(!unit->LoadAddonConfig(&width,&height,id,false)) throw new InvalidOperationException("Игра не загрузила настройки элемента.");
                unit->OnConfigLoaded(false);
            }
            finally { entry=oldEntry; *general=oldGeneral; unit->SetScale(scale,false);unit->SetAlpha(alpha);unit->SetPosition(x,y); }
            return;
        }
        throw new InvalidOperationException("Элемент отсутствует в раскладке.");
    }
}

