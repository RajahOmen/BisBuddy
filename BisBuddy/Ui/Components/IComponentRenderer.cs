using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Ui.Components
{
    /// <summary>
    /// Draws a component with ImGui.
    /// </summary>
    public interface IComponentRenderer<T>
    {
        /// <summary>
        /// Initializes the renderer with a renderable component to draw when <see cref="Draw"/> is called.
        /// Subsequent calls to Initialize will be ignored
        /// </summary>
        /// <param name="renderableComponent">The component to render with this renderer</param>
        public void Initialize(T renderableComponent);

        /// <summary>
        /// Draws the component using ImGui.
        /// </summary>
        public void Draw();
    }
}
