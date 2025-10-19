using System;
using Dalamud.Bindings.ImGui;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
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
