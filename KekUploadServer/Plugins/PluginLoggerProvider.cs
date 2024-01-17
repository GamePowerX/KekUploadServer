namespace KekUploadServer.Plugins;

public class PluginLoggerProvider : ILoggerProvider
{
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public ILogger CreateLogger(string categoryName)
    {
        var logger = new PluginLogger(categoryName);
        return logger;
    }
}

public class PluginLogger(string categoryName) : ILogger
{
    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        var scope = new PluginLoggerScope<TState>(state);
        return scope;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // format like the console logger
        var formatted = formatter(state, exception);
        var logLevelString = logLevel.ToString().ToUpper();
        var logString = $"[{DateTime.Now:HH:mm:ss}] [{logLevelString}] [{categoryName}] {formatted}";
        PluginLoader.PluginApi.OnConsoleLineWritten(logString);
    }
}

public class PluginLoggerScope<T>(object state) : IDisposable
{
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}