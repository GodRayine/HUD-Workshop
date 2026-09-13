using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace HudWorkshop;

public sealed partial class Plugin
{
    private readonly WindowSystem windowSystem = new("HudWorkshop");
    private readonly InspectorWindow editorWindow;
    private Snapshot? inspectorSnapshot;

    private sealed class InspectorWindow : Window
    {
        private readonly Plugin owner;

        public InspectorWindow(Plugin owner) : base("HUD Workshop###HudWorkshopEditor", ImGuiWindowFlags.None, true)
        {
            this.owner = owner;
            Size = new Vector2(470, 760);
            SizeCondition = ImGuiCond.FirstUseEver;
            PositionCondition = ImGuiCond.FirstUseEver;
            SizeConstraints = new WindowSizeConstraints { MinimumSize = new(440, 540), MaximumSize = new(780, 1400) };
            AllowClickthrough = false;
            DisableFadeInFadeOut = true;
        }

        public override void PreDraw()
        {
            Position = ImGui.GetMainViewport().Pos + new Vector2(60, 160);
        }

        public override void Draw()
        {
            // WindowSystem draws the native title controls outside this content scope.
            // Keep their default padding, while retaining the inspector's larger controls.
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 6));
            try { owner.DrawInspector(owner.inspectorSnapshot); }
            finally { ImGui.PopStyleVar(); }
        }
    }
}
