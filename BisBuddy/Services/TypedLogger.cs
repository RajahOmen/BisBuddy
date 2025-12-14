using Dalamud.Plugin.Services;
using Serilog;
using Serilog.Events;
using System;
using System.Linq;

namespace BisBuddy.Services
{
    public class TypedLogger<T>(
        IPluginLog pluginLog
        ) : ITypedLogger<T> where T : class
    {
        private readonly IPluginLog pluginLog = pluginLog;
        private readonly string logPrefix = $"[{typeof(T).Name}]";

        private string prefixedMessage(string message)
            => $"{logPrefix} {message}";

        private object[] getValues(object?[]? values) =>
            values?
            .Where(v => v is not null)
            .Cast<object>()
            .ToArray() ?? [];

        public void Verbose(string message, params object?[]? values) =>
            Verbose(null, message, getValues(values));
        public void Verbose(Exception? exception, string message, params object?[]? values)
            => pluginLog.Verbose(exception, prefixedMessage(message), getValues(values));

        public void Info(string message, params object?[]? values) =>
            Info(null, message, getValues(values));
        public void Info(Exception? exception, string message, params object?[]? values) =>
            pluginLog.Info(exception, prefixedMessage(message), getValues(values));

        public void Debug(string message, params object?[]? values) =>
            Debug(null, message, getValues(values));
        public void Debug(Exception? exception, string message, params object?[]? values) =>
            pluginLog.Debug(exception, prefixedMessage(message), getValues(values));

        public void Warning(string message, params object?[]? values) =>
            Warning(null, message, getValues(values));
        public void Warning(Exception? exception, string message, params object?[]? values) =>
            pluginLog.Warning(exception, prefixedMessage(message), getValues(values));

        public void Error(string message, params object?[]? values) =>
            Error(null, message, getValues(values));
        public void Error(Exception? exception, string message, params object?[]? values) =>
            pluginLog.Error(exception, prefixedMessage(message), getValues(values));

        public void Fatal(string message, params object?[]? values) =>
            Fatal(null, message, getValues(values));
        public void Fatal(Exception? exception, string message, params object?[]? values) =>
            pluginLog.Fatal(exception, prefixedMessage(message), getValues(values));

        public void Write(LogEvent logEvent)
        {
            pluginLog.Write(
                logEvent.Level,
                logEvent.Exception,
                logEvent.MessageTemplate.Text,
                logEvent.Properties.Select(p => p.Value.ToString()).ToArray()
                );
        }
    }

    public interface ITypedLogger<T> : ILogger where T : class
    {
        void Info(string message, params object?[]? values);
        void Info(Exception? exception, string message, params object?[]? values);
    }
}
