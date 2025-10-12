using BisBuddy.Services;
using BisBuddy.Ui.Renderers.ContextMenus;
using Dalamud.Interface;
using System;

namespace BisBuddy.Factories
{
    public class ContextMenuEntryFactory(ITypedLogger<ContextMenuEntry> menuLogger) : IContextMenuEntryFactory
    {
        private readonly ITypedLogger<ContextMenuEntry> menuLogger = menuLogger;

        public ContextMenuEntry Create(
            string entryName,
            FontAwesomeIcon? icon = null,
            Func<bool>? drawFunc = null,
            Func<bool>? shouldDraw = null,
            Action? onClick = null
            )
        {
            return new ContextMenuEntry(
                logger: menuLogger,
                entryName: entryName,
                icon: icon,
                drawFunc: drawFunc,
                shouldDraw: shouldDraw,
                onClick: onClick
                );
        }
    }

    public interface IContextMenuEntryFactory
    {
        public ContextMenuEntry Create(
            string entryName,
            FontAwesomeIcon? icon = null,
            Func<bool>? drawFunc = null,
            Func<bool>? shouldDraw = null,
            Action? onClick = null
            );
    }
}
