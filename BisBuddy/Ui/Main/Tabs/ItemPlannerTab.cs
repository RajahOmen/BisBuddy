using System;
using ImGuiNET;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Interface.Windowing;
using static Dalamud.Interface.Windowing.Window;

namespace BisBuddy.Ui.Main.Tabs
{
    public class ItemPlannerTab : TabRenderer
    {
        public WindowSizeConstraints? TabSizeConstraints => null;

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
