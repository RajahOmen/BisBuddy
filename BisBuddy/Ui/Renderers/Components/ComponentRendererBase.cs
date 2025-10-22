namespace BisBuddy.Ui.Renderers.Components
{
    public abstract class ComponentRendererBase<T> : ITypedRenderer<T>
    {
        public static RendererType RendererType => RendererType.Component;

        public abstract void Draw();
        public abstract void Initialize(T renderableComponent);
    }
}
