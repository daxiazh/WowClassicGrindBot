using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;

namespace VizAura.Services;

/// <summary>
/// 将日志输出到 UI 的 LoggerProvider
/// </summary>
public sealed class UILoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, UILogger> loggers = new();
    
    /// <summary>
    /// 日志写入事件
    /// </summary>
    public event Action<string, LogLevel, string>? OnLogWritten;

    public ILogger CreateLogger(string categoryName)
    {
        return loggers.GetOrAdd(categoryName, name => new UILogger(name, this));
    }

    public void Dispose()
    {
        loggers.Clear();
    }

    /// <summary>
    /// 触发日志写入事件
    /// </summary>
    /// <param name="categoryName">日志分类名称</param>
    /// <param name="logLevel">日志级别</param>
    /// <param name="message">日志消息</param>
    internal void WriteLog(string categoryName, LogLevel logLevel, string message)
    {
        OnLogWritten?.Invoke(categoryName, logLevel, message);
    }

    /// <summary>
    /// UI Logger 实现
    /// </summary>
    private sealed class UILogger : ILogger
    {
        private readonly string categoryName;
        private readonly UILoggerProvider provider;

        public UILogger(string categoryName, UILoggerProvider provider)
        {
            this.categoryName = categoryName;
            this.provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            string message = formatter(state, exception);
            if (exception != null)
            {
                message += Environment.NewLine + exception;
            }

            provider.WriteLog(categoryName, logLevel, message);
        }
    }
}
