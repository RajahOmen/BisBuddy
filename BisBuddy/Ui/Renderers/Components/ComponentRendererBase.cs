using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Ui.Renderers.Components
{
    public abstract class ComponentRendererBase<T> : ITypedRenderer<T>
    {
        public static RendererType RendererType => RendererType.Component;

        public abstract void Draw();
        public abstract void Initialize(T renderableComponent);
    }
}
