using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace HudWorkshop;

public sealed partial class Plugin
{
    private static string ElementState(Bar bar) => !bar.Enabled ? " · выключен" : bar.Offline ? " · другой режим" : bar.Placeholder ? " · неактивен" : "";
    private static void SectionTitle(string title)
    {
        ImGui.Spacing();
        ImGui.TextColored(WorkshopTheme.Muted, title);
        ImGui.Spacing();
    }

    // Scoped to this inspector: never modify the game's or another plugin's theme.
    private sealed class WorkshopTheme : IDisposable
    {
        public static readonly Vector4 Accent = new(.36f, .85f, .76f, 1);
        public static readonly Vector4 AccentButton = new(.10f, .37f, .33f, 1);
        public static readonly Vector4 AccentHover = new(.13f, .47f, .41f, 1);
        public static readonly Vector4 AccentActive = new(.09f, .30f, .28f, 1);
        public static readonly Vector4 Text = new(.91f, .94f, .97f, 1);
        public static readonly Vector4 Muted = new(.57f, .64f, .70f, 1);
        public static readonly Vector4 Frame = new(.45f, .58f, .64f, .85f);
        public static readonly Vector4 Warm = new(.95f, .74f, .43f, 1);
        private int colors;
        private int variables;
        private void Color(ImGuiCol slot, Vector4 value) { ImGui.PushStyleColor(slot, value); colors++; }
        private void Scalar(ImGuiStyleVar slot, float value) { ImGui.PushStyleVar(slot, value); variables++; }
        private void Vector(ImGuiStyleVar slot, Vector2 value) { ImGui.PushStyleVar(slot, value); variables++; }
        public WorkshopTheme()
        {
            Color(ImGuiCol.Text, Text);
            Color(ImGuiCol.TextDisabled, Muted);
            Color(ImGuiCol.WindowBg, new(.055f, .068f, .083f, .98f));
            Color(ImGuiCol.ChildBg, Vector4.Zero);
            Color(ImGuiCol.PopupBg, new(.075f, .095f, .115f, .99f));
            Color(ImGuiCol.Border, new(.20f, .26f, .30f, .7f));
            Color(ImGuiCol.TitleBg, new(.07f, .09f, .11f, 1));
            Color(ImGuiCol.TitleBgActive, new(.09f, .13f, .15f, 1));
            Color(ImGuiCol.TitleBgCollapsed, new(.07f, .09f, .11f, .95f));
            Color(ImGuiCol.FrameBg, new(.105f, .135f, .16f, 1));
            Color(ImGuiCol.FrameBgHovered, new(.15f, .20f, .23f, 1));
            Color(ImGuiCol.FrameBgActive, new(.17f, .26f, .28f, 1));
            Color(ImGuiCol.Button, new(.13f, .18f, .21f, 1));
            Color(ImGuiCol.ButtonHovered, new(.19f, .27f, .30f, 1));
            Color(ImGuiCol.ButtonActive, new(.14f, .32f, .30f, 1));
            Color(ImGuiCol.Header, new(.11f, .16f, .19f, 1));
            Color(ImGuiCol.HeaderHovered, new(.16f, .24f, .27f, 1));
            Color(ImGuiCol.HeaderActive, new(.15f, .30f, .28f, 1));
            Color(ImGuiCol.CheckMark, Accent);
            Color(ImGuiCol.SliderGrab, Accent);
            Color(ImGuiCol.SliderGrabActive, AccentHover);
            Color(ImGuiCol.Separator, new(.22f, .29f, .33f, .6f));
            Color(ImGuiCol.ScrollbarBg, new(.06f, .08f, .10f, .6f));
            Color(ImGuiCol.ScrollbarGrab, new(.23f, .31f, .35f, 1));
            Color(ImGuiCol.ScrollbarGrabHovered, new(.30f, .42f, .45f, 1));
            Color(ImGuiCol.ScrollbarGrabActive, AccentButton);
            Color(ImGuiCol.ResizeGrip, new(.36f, .85f, .76f, .15f));
            Color(ImGuiCol.ResizeGripHovered, new(.36f, .85f, .76f, .5f));
            Color(ImGuiCol.ResizeGripActive, Accent);
            Scalar(ImGuiStyleVar.WindowRounding, 10);
            Scalar(ImGuiStyleVar.ChildRounding, 6);
            Scalar(ImGuiStyleVar.FrameRounding, 5);
            Scalar(ImGuiStyleVar.PopupRounding, 6);
            Scalar(ImGuiStyleVar.ScrollbarRounding, 6);
            Scalar(ImGuiStyleVar.WindowBorderSize, 1);
            Scalar(ImGuiStyleVar.FrameBorderSize, 0);
            Vector(ImGuiStyleVar.WindowPadding, new(18, 16));
            Vector(ImGuiStyleVar.FramePadding, new(10, 6));
            Vector(ImGuiStyleVar.ItemSpacing, new(10, 9));
        }
        public void Dispose() { ImGui.PopStyleVar(variables); ImGui.PopStyleColor(colors); }
    }
}
