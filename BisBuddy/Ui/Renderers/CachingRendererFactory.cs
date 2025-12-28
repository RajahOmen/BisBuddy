using Autofac;
using BisBuddy.Services;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;

namespace BisBuddy.Ui.Renderers
{
    public class CachingRendererFactory : IRendererFactory, IDisposable
    {
        private readonly ITypedLogger<CachingRendererFactory> logger;
        private readonly ILifetimeScope rootScope;
        private readonly IClientState clientState;
        private readonly Dictionary<(object, RendererType), ILifetimeScope> scopeCache = [];

        public CachingRendererFactory(
            ITypedLogger<CachingRendererFactory> logger,
            ILifetimeScope rootScope,
            IClientState clientState
        )
        {
            this.logger = logger;
            this.rootScope = rootScope;
            this.clientState = clientState;
            clientState.Logout += handleOnLogout;
        }

        public void Dispose()
        {
            clientState.Logout -= handleOnLogout;
        }

        public IRenderer<T> GetRenderer<T>(
            T itemToRender,
            RendererType rendererType
            ) where T : notnull
        {
            if (itemToRender is null)
                throw new ArgumentNullException($"This value can't be null!: {nameof(itemToRender)} = '{itemToRender}'");

            if (scopeCache.TryGetValue((itemToRender, rendererType), out var cachedScope))
                return cachedScope.ResolveKeyed<IRenderer<T>>(rendererType);

            var newScope = rootScope.BeginLifetimeScope();
            scopeCache[(itemToRender, rendererType)] = newScope;

            var renderer = newScope.ResolveKeyed<IRenderer<T>>(rendererType);
            renderer.Initialize(itemToRender);

            return renderer;
        }

        private void handleOnLogout(int type, int code)
        {
            logger.Debug($"Purging {scopeCache.Count} renderers");
            scopeCache.Clear();
        }
    }
}
