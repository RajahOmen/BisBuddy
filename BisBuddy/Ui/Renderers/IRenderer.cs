using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Ui.Renderers
{
    /// <summary>
    /// Something that can be rendered in the ImGui UI.
    /// </summary>
    /// <typeparam name="T">What kind of thing to render</typeparam>
    public interface IRenderer<T>
    {
        /// <summary>
        /// Initializes the renderer with a renderable component to draw when <see cref="Draw"/> is called.
        /// Subsequent calls to Initialize will be ignored
        /// </summary>
        /// <param name="renderableComponent">The component to render with this renderer</param>
        public void Initialize(T renderableComponent);

        /// <summary>
        /// Draws the item using ImGui.
        /// </summary>
        public void Draw();
    }
}
