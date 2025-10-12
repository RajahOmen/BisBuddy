using BisBuddy.Services;
using BisBuddy.Ui.Renderers.ContextMenus;
using Dalamud.Interface;
using System;
using System.Numerics;

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
            Action? onClick = null,
            Func<Vector4>? textColor = null,
            Func<Vector4>? backgroundColor = null
            )
        {
            return new ContextMenuEntry(
                logger: menuLogger,
                entryName: entryName,
                icon: icon,
                drawFunc: drawFunc,
                shouldDraw: shouldDraw,
                onClick: onClick,
                textColor: textColor,
                backgroundColor: backgroundColor
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
            Action? onClick = null,
            Func<Vector4>? textColor = null,
            Func<Vector4>? backgroundColor = null
            );
    }
}
