using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static Dalamud.Interface.Windowing.Window;

namespace BisBuddy.Ui.Main.Tabs
{
    /// <summary>
    /// Handles drawing of a particular tab type
    /// </summary>
    public interface TabRenderer
    {
        /// <summary>
        /// If set, further restrict window tab constraints to that of the tab.
        /// Will ignore constraints that are more permissive than the ones set at
        /// the window level.
        /// </summary>
        public WindowSizeConstraints? TabSizeConstraints { get; }

        /// <summary>
        /// Do any tasks needed before drawing
        /// </summary>
        public void PreDraw();

        /// <summary>
        /// Draw the tab using ImGui
        /// </summary>
        public void Draw();

        /// <summary>
        /// Set the next tab state to draw/render
        /// </summary>
        /// <param name="state">The state to set the tab to</param>
        /// <exception cref="ArgumentException">If the TabState type is not correct</exception>
        public void SetTabState(TabState state);
    }
}
