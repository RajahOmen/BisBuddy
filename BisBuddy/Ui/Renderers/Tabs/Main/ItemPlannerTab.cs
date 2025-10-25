using Dalamud.Bindings.ImGui;
using System;
using static Dalamud.Interface.Windowing.Window;

namespace BisBuddy.Ui.Renderers.Tabs.Main
{
    public class ItemPlannerTab : TabRenderer<MainWindowTab>
    {
        public WindowSizeConstraints? TabSizeConstraints => null;

        public bool ShouldDraw => true;

        public void PreDraw() { }

        public void Draw()
        {
            ImGui.Text("Item Planner coming soon...");
        }

        public void SetTabState(TabState state)
        {
            throw new NotImplementedException();
        }
    }
}
