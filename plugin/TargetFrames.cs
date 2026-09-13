using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace HudWorkshop;
public sealed partial class Plugin
{
    private static bool IsSplitTarget(string id) => id is "_TargetInfoMainTarget" or "_TargetInfoCastBar" or "_TargetInfoBuffDebuff";

    private static unsafe (float X, float Y, float Width, float Height) ReadSplitTargetFrame(string id, AtkUnitBase* unit)
    {
        var min=new Vector2(float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity);var count=0;
        for(var i=0;i<unit->UldManager.NodeListCount;i++)
        {
            var node=unit->UldManager.NodeList[i];if(node==null)continue;
            // Stable content nodes from the pinned client. Do not shrink when the target has no cast/effects.
            var include=id switch
            {
                "_TargetInfoBuffDebuff" => node->NodeId==2 && (int)node->Type==1,
                "_TargetInfoCastBar" => node->NodeId is >=2 and <=7,
                "_TargetInfoMainTarget" => node->NodeId is 10 or 11 or 13
                    || (node->NodeId is 2 or 3 or 12 or 14 or 15 && node->IsVisible()),
                _ => false
            };
            if(!include || node->Width==0 || node->Height==0)continue;
            var lo=new Vector2(float.PositiveInfinity);var hi=new Vector2(float.NegativeInfinity);
            for(var corner=0;corner<4;corner++)
            {
                var point=new Vector2((corner&1)==0?0:node->Width,(corner&2)==0?0:node->Height);
                var parent=node;var depth=0;
                while(parent!=null && parent!=unit->RootNode && depth++<32)
                {
                    var origin=new Vector2(parent->OriginX,parent->OriginY);
                    point=(point-origin)*new Vector2(parent->ScaleX,parent->ScaleY);
                    var sin=MathF.Sin(parent->Rotation);var cos=MathF.Cos(parent->Rotation);
                    point=new Vector2(point.X*cos-point.Y*sin,point.X*sin+point.Y*cos)+origin+new Vector2(parent->X,parent->Y);
                    parent=parent->ParentNode;
                }
                if(parent!=unit->RootNode)throw new InvalidOperationException("Не удалось определить геометрию панели цели.");
                // Local coordinates also work while hidden: no stale screen transforms are consulted.
                point=new Vector2(unit->X,unit->Y)+point*unit->Scale;
                lo=Vector2.Min(lo,point);hi=Vector2.Max(hi,point);
            }
            min=Vector2.Min(min,lo);max=Vector2.Max(max,hi);count++;
        }
        if(count==0)throw new InvalidOperationException("Не найдены узлы раздельной панели цели.");
        return (min.X-6,min.Y-6,max.X-min.X+12,max.Y-min.Y+12);
    }
}
