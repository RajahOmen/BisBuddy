using Autofac;
using System.Collections.Generic;

namespace BisBuddy.Ui.Renderers
{
    public class CachingRendererFactory(
           ILifetimeScope rootScope
           ) : IRendererFactory
    {
        private readonly ILifetimeScope rootScope = rootScope;
        private readonly Dictionary<(object, RendererType), ILifetimeScope> scopeCache = [];

        public IRenderer<T> GetRenderer<T>(
            T itemToRender,
            RendererType rendererType
            ) where T : notnull
        {
            if (scopeCache.TryGetValue((itemToRender, rendererType), out var cachedScope))
                return cachedScope.ResolveKeyed<IRenderer<T>>(rendererType);

            var newScope = rootScope.BeginLifetimeScope();
            scopeCache[(itemToRender, rendererType)] = newScope;

            var renderer = newScope.ResolveKeyed<IRenderer<T>>(rendererType);
            renderer.Initialize(itemToRender);

            return renderer;
        }
    }
}
