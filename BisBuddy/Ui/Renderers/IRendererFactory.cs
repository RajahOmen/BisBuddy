namespace BisBuddy.Ui.Renderers
{
    public interface IRendererFactory
    {
        /// <summary>
        /// Returns a renderer for the given instance of the item.
        /// </summary>
        /// <param name="itemToRender">The instance of <typeparamref name="T"/> to get a renderer for.</param>
        /// <returns>A new instance of a renderer for the specified instance.</returns>
        IRenderer<T> GetRenderer<T>(T itemToRender, RendererType rendererType) where T : notnull;
    }
}
