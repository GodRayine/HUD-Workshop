using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace HudWorkshop;

public sealed partial class Plugin
{
    private static unsafe (float X, float Y, float Width, float Height) ReadJobFrame(AtkUnitBase* unit)
    {
        // Standard and simple gauges coexist in the tree. Only the active visible branch counts.
        var root = ((AddonJobHud*)unit)->JobHudRootNode;
        if (root == null) root = unit->RootNode;
        var left = int.MaxValue; var top = int.MaxValue;
        var right = int.MinValue; var bottom = int.MinValue; var count = 0;
        for (var i = 0; i < unit->UldManager.NodeListCount; i++)
        {
            var node = unit->UldManager.NodeList[i];
            if (node == null || node == root || node->ParentNode != root || !node->IsVisible()) continue;
            FFXIVClientStructs.FFXIV.Common.Math.Bounds bounds;
            node->GetBounds(&bounds);
            if (bounds.Width <= 0 || bounds.Height <= 0) continue;
            left = Math.Min(left, bounds.Pos1.X); top = Math.Min(top, bounds.Pos1.Y);
            right = Math.Max(right, bounds.Pos2.X); bottom = Math.Max(bottom, bounds.Pos2.Y); count++;
        }
        if (count == 0)
        {
            FFXIVClientStructs.FFXIV.Common.Math.Bounds bounds;
            unit->GetWindowBounds(&bounds);
            return (bounds.Pos1.X - 6, bounds.Pos1.Y - 6, bounds.Width + 12, bounds.Height + 12);
        }
        return (left - 6, top - 6, right - left + 12, bottom - top + 12);
    }
}
