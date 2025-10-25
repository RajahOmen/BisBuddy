namespace BisBuddy.Ui.Renderers
{
    public interface ITypedRenderer<T> : IRenderer<T>
    {
        public abstract static RendererType RendererType { get; }
    }
}
