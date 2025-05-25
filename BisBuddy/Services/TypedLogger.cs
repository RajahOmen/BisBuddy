using Dalamud.Plugin.Services;
using System;

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

        public void Verbose(string message, params object[] values) =>
            Verbose(null, message, values);
        public void Verbose(Exception? exception, string message, params object[] values)
            => pluginLog.Verbose(exception, prefixedMessage(message), values);

        public void Info(string message, params object[] values) =>
            Info(null, message, values);
        public void Info(Exception? exception, string message, params object[] values) =>
            pluginLog.Info(exception, prefixedMessage(message), values);

        public void Debug(string message, params object[] values) =>
            Debug(null, message, values);
        public void Debug(Exception? exception, string message, params object[] values) =>
            pluginLog.Debug(exception, prefixedMessage(message), values);

        public void Warning(string message, params object[] values) =>
            Warning(null, message, values);
        public void Warning(Exception? exception, string message, params object[] values) =>
            pluginLog.Warning(exception, prefixedMessage(message), values);

        public void Error(string message, params object[] values) =>
            Error(null, message, values);
        public void Error(Exception? exception, string message, params object[] values) =>
            pluginLog.Error(exception, prefixedMessage(message), values);

        public void Fatal(string message, params object[] values) =>
            Fatal(null, message, values);
        public void Fatal(Exception? exception, string message, params object[] values) =>
            pluginLog.Fatal(exception, prefixedMessage(message), values);
    }

    public interface ITypedLogger<T> where T : class
    {
        void Verbose(string message, params object[] values);
        void Verbose(Exception? exception, string message, params object[] values);

        void Info(string message, params object[] values);
        void Info(Exception? exception, string message, params object[] values);

        void Debug(string message, params object[] values);
        void Debug(Exception? exception, string message, params object[] values);

        void Warning(string message, params object[] values);
        void Warning(Exception? exception, string message, params object[] values);

        void Error(string message, params object[] values);
        void Error(Exception? exception, string message, params object[] values);

        void Fatal(string message, params object[] values);
        void Fatal(Exception? exception, string message, params object[] values);
    }
}
