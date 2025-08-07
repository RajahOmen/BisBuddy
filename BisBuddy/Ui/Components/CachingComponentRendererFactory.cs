using Autofac;
using BisBuddy.Gear;
using BisBuddy.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BisBuddy.Ui.Components
{
    public class CachingComponentRendererFactory(
        ILifetimeScope rootScope
        ) : IComponentRendererFactory
    {
        private readonly ILifetimeScope rootScope = rootScope;
        private readonly Dictionary<object, ILifetimeScope> scopeCache = [];

        public IComponentRenderer<T> GetComponentRenderer<T>(T componentToRender)
        {
            if (componentToRender is null)
                throw new ArgumentNullException(nameof(componentToRender));

            if (scopeCache.TryGetValue(componentToRender, out var cachedScope))
                return cachedScope.Resolve<IComponentRenderer<T>>();

            var newScope = rootScope.BeginLifetimeScope();
            scopeCache[componentToRender] = newScope;

            var renderer = newScope.Resolve<IComponentRenderer<T>>();
            renderer.Initialize(componentToRender);

            return renderer;
        }
    }
}
