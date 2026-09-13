using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace HudWorkshop;

public sealed partial class Plugin : IDalamudPlugin
{
    [PluginService] private static IDalamudPluginInterface PluginInterface { get; set; } = null!;
    [PluginService] private static ICommandManager Commands { get; set; } = null!;
    [PluginService] private static IGameGui GameGui { get; set; } = null!;
    [PluginService] private static IClientState ClientState { get; set; } = null!;
    [PluginService] private static IPlayerState PlayerState { get; set; } = null!;
    [PluginService] private static ICondition Conditions { get; set; } = null!;
    [PluginService] private static IFramework Framework { get; set; } = null!;
    [PluginService] private static IPluginLog Log { get; set; } = null!;
    [PluginService] private static IGameConfig GameConfig { get; set; } = null!;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public Plugin()
    {
        // Check before registering any frame callbacks, including chat visibility writes.
        var version = typeof(IDalamudPlugin).Assembly.GetName().Version?.ToString();
        if (version != "15.0.3.4")
            throw new NotSupportedException($"HUD Workshop требует Dalamud 15.0.3.4; установлена {version}. Нужна совместимая сборка плагина.");
        editorWindow = new InspectorWindow(this);
        windowSystem.AddWindow(editorWindow);
        Commands.AddHandler("/hudworkshop", new CommandInfo(OnCommand) { HelpMessage = "Открыть редактор элементов HUD Workshop." });
        PluginInterface.UiBuilder.Draw += ApplyChatVisibility;
        PluginInterface.UiBuilder.Draw += DrawEditor;
        PluginInterface.UiBuilder.OpenMainUi += OpenEditor;
        PluginInterface.UiBuilder.OpenConfigUi += OpenEditor;
        Log.Information("HUD Workshop 1.1.1 loaded. Open with /hudworkshop.");
    }
    private void OnCommand(string command, string args) { if (editorOpen) CloseEditor(); else OpenEditor(); }
    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= ApplyChatVisibility;
        PluginInterface.UiBuilder.Draw -= DrawEditor;
        PluginInterface.UiBuilder.OpenMainUi -= OpenEditor;
        PluginInterface.UiBuilder.OpenConfigUi -= OpenEditor;
        Commands.RemoveHandler("/hudworkshop");
        windowSystem.RemoveAllWindows();
        Framework.RunOnFrameworkThread(() =>
        {
            if(baseline != null)CloseEditor();
            foreach(var id in disabledChat.Where(id=>IsChat(id)&&Names.Contains(id)))EnableNative(id,true);
        }).GetAwaiter().GetResult();
    }
    private unsafe Snapshot Read()
    {
        // Fail closed before touching client structures on a different Dalamud build.
        var version = typeof(IDalamudPlugin).Assembly.GetName().Version?.ToString();
        if (version != "15.0.3.4") throw new NotSupportedException($"Expected Dalamud 15.0.3.4, found {version}.");
        if (registeredHudNames == null)
        {
            registeredHudNames = new();
            foreach (ref readonly var item in HudLayoutAddon.GetSpan()) registeredHudNames.Add(item.AddonName.ToString());
        }
        var started = Stopwatch.GetTimestamp();
        var config = AddonConfig.Instance();
        var layout = config != null && config->IsLoaded && config->ActiveDataSet != null ? config->ActiveDataSet->CurrentHudLayout : -1;
        var savedEntries = new Dictionary<uint, SavedEntry>();
        if (layout is >= 0 and < 4)
        {
            // The 448/4 layout is from the exact ClientStructs shipped with this Dalamud build.
            var entries = config->ActiveDataSet->HudLayoutConfigEntries.Slice(layout * 112, 112);
            foreach (ref readonly var entry in entries)
            {
                if (!entry.HasValue) continue;
                savedEntries.TryAdd(entry.AddonNameHash, new SavedEntry(entry.X, entry.Y, entry.Scale, entry.ElementFlags,
                    entry.Width, entry.Height, entry.ByteValue1, entry.ByteValue2, entry.ByteValue3, entry.Alpha));
            }
        }
        if(config != null && config->IsLoaded && config->ActiveDataSet != null)
            foreach(ref readonly var entry in config->ActiveDataSet->ConfigEntries)
                if(entry.HasValue && ChatHashes.Contains(entry.AddonNameHash))
                    savedEntries[entry.AddonNameHash]=new SavedEntry(entry.X,entry.Y,entry.Scale,entry.ElementFlags,entry.Width,entry.Height,entry.ByteValue1,entry.ByteValue2,entry.ByteValue3,entry.Alpha);
        // _TargetInfo's saved bit 0 selects the three-part target display, even without a target.
        var splitTarget = savedEntries.TryGetValue(Crc32("_TargetInfo_a"),out var targetSettings)
            ? (bool?)((targetSettings.Flags & 1u) != 0) : null;
        var viewport = ImGui.GetMainViewport();
        var bars = new List<Bar>();
        foreach (var element in Elements)
        {
            savedEntries.TryGetValue(Crc32(element.Id + "_a"), out var saved);
            var addon = GameGui.GetAddonByName(element.Id);
            var ready = (registeredHudNames.Contains(element.Id) || IsChat(element.Id)) && saved != null && !addon.IsNull && addon.IsReady;
            var visible = ready && addon.IsVisible;
            bool? simpleGauge = null;
            var canPlaceholder = ready;
            if (ready && element.Id.StartsWith("JobHud", StringComparison.Ordinal))
            {
                var job = (FFXIVClientStructs.FFXIV.Client.UI.AddonJobHud*)addon.Address;
                canPlaceholder &= job->IsGaugeInitialized && job->IsConfigLoaded;
                visible &= canPlaceholder;
                ready &= canPlaceholder;
                simpleGauge = job->UseSimpleGauge;
            }
            (float X, float Y, float Width, float Height) frame = default;
            if (visible)
            {
                if (element.Hotbar) frame = ReadHotbarFrame((AtkUnitBase*)addon.Address);
                else if (element.Id == "_ParameterWidget") frame = ReadParameterFrame((AtkUnitBase*)addon.Address);
                else if (element.Id.StartsWith("JobHud", StringComparison.Ordinal)) frame = ReadJobFrame((AtkUnitBase*)addon.Address);
                else
                {
                    FFXIVClientStructs.FFXIV.Common.Math.Bounds bounds;
                    ((AtkUnitBase*)addon.Address)->GetWindowBounds(&bounds);
                    frame = (bounds.Pos1.X - 6, bounds.Pos1.Y - 6, bounds.Width + 12, bounds.Height + 12);
                    visible = bounds.Width > 0 && bounds.Height > 0;
                }
            }
            var placeholder = canPlaceholder && !visible && saved!.Width > 0 && saved.Height > 0;
            if(placeholder)
            {
                // Hidden nodes may retain stale screen transforms. Use the addon origin and layout extent.
                var unit=(AtkUnitBase*)addon.Address;
                var width=unit->RootNode!=null && unit->RootNode->Width>0?unit->RootNode->Width:saved!.Width;
                var height=unit->RootNode!=null && unit->RootNode->Height>0?unit->RootNode->Height:saved!.Height;
                frame=(addon.X-6,addon.Y-6,width*addon.Scale+12,height*addon.Scale+12);
            }
            if((visible || placeholder) && element.Id.StartsWith("ChatLogPanel_")) frame=(addon.X-6,addon.Y-6,((AtkUnitBase*)addon.Address)->RootNode->Width*addon.Scale+12,((AtkUnitBase*)addon.Address)->RootNode->Height*addon.Scale+12);
            if((visible || placeholder) && IsSplitTarget(element.Id)) frame=ReadSplitTargetFrame(element.Id,(AtkUnitBase*)addon.Address);
            var bar = new Bar(element.Id, element.Label, ready, visible,
                ready ? addon.X : 0, ready ? addon.Y : 0, frame.Width,
                frame.Height, ready ? addon.Scale : 0, saved, visible || placeholder ? frame.X - addon.X : 0, visible || placeholder ? frame.Y - addon.Y : 0, simpleGauge,
                ready && element.Hotbar ? (int)((FFXIVClientStructs.FFXIV.Client.UI.AddonActionBarX*)addon.Address)->ActionBarLayout : null,
                ready && ((AtkUnitBase*)addon.Address)->RootNode != null ? ((AtkUnitBase*)addon.Address)->RootNode->Width : (ushort)0,
                ready && ((AtkUnitBase*)addon.Address)->RootNode != null ? ((AtkUnitBase*)addon.Address)->RootNode->Height : (ushort)0, placeholder,
                splitTarget is { } split ? TargetMode.Includes(element.Id,split) : !element.Id.StartsWith("_TargetInfo",StringComparison.Ordinal));
            bar=ProjectOffline(bar,viewport.Size);
            if(element.Id.StartsWith("ChatLogPanel_")) bar=bar with {ModeApplicable=saved!=null&&(saved.Flags&1)!=0};
            if(bar.Offline && IsChat(element.Id))bar=bar with{Enabled=previewEnabled.GetValueOrDefault(element.Id,!disabledChat.Contains(element.Id))};
            if(ready) bar=bar with {Enabled=(((AtkUnitBase*)addon.Address)->VisibilityFlags&1)==0};
            bars.Add(bar);
        }
        bars=FilterStatusModes(bars);
        return new Snapshot(1, DateTime.UtcNow, version!, layout, viewport.Size.X, viewport.Size.Y,
            viewport.Pos.X, viewport.Pos.Y, Stopwatch.GetElapsedTime(started).TotalMilliseconds, bars);
    }

    private static unsafe (float X, float Y, float Width, float Height) ReadHotbarFrame(AtkUnitBase* unit)
    {
        // The ordinary hotbar's twelve DragDrop components contain the actual 44x44 icon nodes.
        // Read their transformed bounds; the addon root also contains number/lock controls and padding.
        var left = int.MaxValue; var top = int.MaxValue;
        var right = int.MinValue; var bottom = int.MinValue; var count = 0;
        for (var i = 0; i < unit->UldManager.NodeListCount; i++)
        {
            var node = unit->UldManager.NodeList[i];
            if (node == null || node->NodeId < 8 || node->NodeId > 19 || (int)node->Type != 1005) continue;
            var component = ((AtkComponentNode*)node)->Component;
            if (component == null) continue;
            for (var j = 0; j < component->UldManager.NodeListCount; j++)
            {
                var icon = component->UldManager.NodeList[j];
                if (icon == null || icon->NodeId != 3 || (int)icon->Type != 1002) continue;
                FFXIVClientStructs.FFXIV.Common.Math.Bounds bounds;
                icon->GetBounds(&bounds);
                if (bounds.Width <= 0 || bounds.Height <= 0) continue;
                left = Math.Min(left, bounds.Pos1.X); top = Math.Min(top, bounds.Pos1.Y);
                right = Math.Max(right, bounds.Pos2.X); bottom = Math.Max(bottom, bounds.Pos2.Y); count++;
            }
        }
        if (count != 12) throw new InvalidOperationException("Не удалось определить границы 12 кнопок панели.");
        // Screen-pixel padding shared by drawing, hit testing and snapping, independent of HUD scale.
        const int padding = 6;
        return (left - padding, top - padding, right - left + padding * 2, bottom - top + padding * 2);
    }

    private static unsafe (float X, float Y, float Width, float Height) ReadParameterFrame(AtkUnitBase* unit)
    {
        // Verified on the pinned client: node 2 is the class label; 3/4 are HP/MP.
        // The root reserves substantial empty space to the left and right of these nodes.
        var left = int.MaxValue; var top = int.MaxValue;
        var right = int.MinValue; var bottom = int.MinValue; var count = 0;
        for (var i = 0; i < unit->UldManager.NodeListCount; i++)
        {
            var node = unit->UldManager.NodeList[i];
            if (node == null || !node->IsVisible()) continue;
            if (!(node->NodeId == 2 && (int)node->Type == 3) &&
                !((node->NodeId == 3 || node->NodeId == 4) && (int)node->Type == 1001)) continue;
            FFXIVClientStructs.FFXIV.Common.Math.Bounds bounds;
            node->GetBounds(&bounds);
            if (bounds.Width <= 0 || bounds.Height <= 0) continue;
            left = Math.Min(left, bounds.Pos1.X); top = Math.Min(top, bounds.Pos1.Y);
            right = Math.Max(right, bounds.Pos2.X); bottom = Math.Max(bottom, bounds.Pos2.Y); count++;
        }
        if (count == 0) throw new InvalidOperationException("Не удалось определить границы параметров персонажа.");
        const int padding = 6;
        return (left - padding, top - padding, right - left + padding * 2, bottom - top + padding * 2);
    }

    private static uint Crc32(string value)
    {
        uint crc = uint.MaxValue;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(value))
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0);
        }
        return ~crc;
    }

    private static unsafe void MoveNative(string id, float x, float y, bool requireVisible = true)
    {
        if (typeof(IDalamudPlugin).Assembly.GetName().Version?.ToString() != "15.0.3.4") throw new NotSupportedException("Версия Dalamud изменилась.");
        if (!float.IsFinite(x) || !float.IsFinite(y) || x < short.MinValue || x > short.MaxValue || y < short.MinValue || y > short.MaxValue) throw new ArgumentOutOfRangeException(nameof(x));
        var addon = GameGui.GetAddonByName(id);
        if (!Names.Contains(id) || addon.IsNull || !addon.IsReady || (requireVisible && !addon.IsVisible)) throw new InvalidOperationException("Элемент недоступен.");
        ((AtkUnitBase*)addon.Address)->SetPosition((short)MathF.Round(x), (short)MathF.Round(y));
    }

}

public sealed record SavedEntry(float X, float Y, float Scale, uint Flags, ushort Width, ushort Height, byte Byte1, byte Byte2, byte Byte3, byte Alpha);
public sealed record Bar(string Id, string Label, bool Ready, bool Visible, float X, float Y, float Width, float Height, float Scale, SavedEntry? Saved, float OffsetX = 0, float OffsetY = 0, bool? SimpleGauge = null, int? HotbarLayout = null, ushort NativeWidth = 0, ushort NativeHeight = 0, bool Placeholder = false, bool ModeApplicable = true, bool Offline = false, bool Enabled = true)
{
    public bool Editable => ModeApplicable && (Ready || Offline) && (Visible || Placeholder);
}
public sealed record Snapshot(int SchemaVersion, DateTime CapturedUtc, string DalamudVersion, int LayoutIndex,
    float ViewportWidth, float ViewportHeight, float ViewportX, float ViewportY, double ReadMilliseconds, List<Bar> Bars);

