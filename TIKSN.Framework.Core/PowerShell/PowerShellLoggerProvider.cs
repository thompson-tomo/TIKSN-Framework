using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TIKSN.PowerShell;

public class PowerShellLoggerProvider : ILoggerProvider
{
    private readonly ICurrentCommandProvider currentCommandProvider;
    private readonly ConcurrentDictionary<string, PowerShellLogger> loggers = new(StringComparer.Ordinal);
    private readonly IOptions<PowerShellLoggerOptions> options;
    private bool disposedValue;

    public PowerShellLoggerProvider(
        ICurrentCommandProvider currentCommandProvider,
        IOptions<PowerShellLoggerOptions> options)
    {
        this.options = options;
        this.currentCommandProvider = currentCommandProvider ??
                                       throw new ArgumentNullException(nameof(currentCommandProvider));
    }

    public ILogger CreateLogger(string categoryName) =>
        this.loggers.GetOrAdd(categoryName, this.CreateLoggerImplementation);

    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                this.loggers.Clear();
            }

            this.disposedValue = true;
        }
    }

    private PowerShellLogger CreateLoggerImplementation(string name)
        => new(this.currentCommandProvider, this.options, name);
}
