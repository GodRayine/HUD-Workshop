namespace HudWorkshop;
public sealed partial class Plugin
{
    // Keep the last observed family while the game temporarily hides all status widgets.
    // Key by the game preference and character, so different configurations do not share observations.
    private readonly Dictionary<(ulong,uint),bool> statusModeObservations=new();
    private List<Bar> FilterStatusModes(List<Bar> bars)
    {
        GameConfig.TryGet(Dalamud.Game.Config.UiConfigOption.BuffDispType,out uint preference);
        var key=(PlayerState.ContentId,preference);
        var combined=bars.Any(b=>b.Id=="_Status" && b.Visible && !previewEnabled.ContainsKey(b.Id));
        var separated=bars.Any(b=>b.Id.StartsWith("_StatusCustom",StringComparison.Ordinal) && b.Visible && !previewEnabled.ContainsKey(b.Id));
        var split=statusModeObservations.GetValueOrDefault(key,true);
        if(combined!=separated)split=separated;
        statusModeObservations[key]=split;
        return bars.Select(b=>b.Id=="_Status"?b with{ModeApplicable=!split}
            :b.Id.StartsWith("_StatusCustom",StringComparison.Ordinal)?b with{ModeApplicable=split}:b).ToList();
    }
}
