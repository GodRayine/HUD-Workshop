namespace HudWorkshop;

internal static class TargetMode
{
    internal static bool Includes(string id, bool split) => id switch
    {
        "_TargetInfo" => !split,
        "_TargetInfoMainTarget" or "_TargetInfoCastBar" or "_TargetInfoBuffDebuff" => split,
        _ => true,
    };
}
