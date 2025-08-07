using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Ui.Components
{
    public interface IComponentRendererFactory
    {
        /// <summary>
        /// Returns a component renderer for the given instance of the component.
        /// </summary>
        /// <param name="componentToRender">The instance of <typeparamref name="T"/> to get a renderer for.</param>
        /// <returns>A new instance of a component renderer for the specified instance.</returns>
        IComponentRenderer<T> GetComponentRenderer<T>(T componentToRender);
    }
}
