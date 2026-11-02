using Microsoft.Extensions.Logging;

namespace RIoT2.Core.Hosting;

/// <summary>
/// The logger factory <see cref="RIoT2Logging.AddRIoT2Logging"/> returns. Until the host's logger factory
/// exists, its loggers write through a factory of their own (the same Serilog pipeline); from then on
/// through the host's, so loggers handed out during start-up end up with the host's providers and log
/// scopes like every other logger.
/// </summary>
internal sealed class StartupLoggerFactory(ILoggerFactory startup) : ILoggerFactory
{
    private ILoggerFactory? _host;

    internal ILoggerFactory Target => Volatile.Read(ref _host) ?? startup;

    internal void Attach(ILoggerFactory host) => Volatile.Write(ref _host, host);

    public ILogger CreateLogger(string categoryName) => new ForwardingLogger(this, categoryName);

    public void AddProvider(ILoggerProvider provider) =>
        throw new NotSupportedException("Add logging providers through the host builder's Logging.");

    // The host disposes its own factory and, with it, the Serilog pipeline.
    public void Dispose() => startup.Dispose();

    private sealed class ForwardingLogger(StartupLoggerFactory owner, string category) : ILogger
    {
        private Binding? _binding;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => Current.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => Current.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Current.Log(logLevel, eventId, state, exception, formatter);

        private ILogger Current
        {
            get
            {
                var target = owner.Target;
                var binding = Volatile.Read(ref _binding);
                if (binding?.Factory != target)
                {
                    binding = new Binding(target, target.CreateLogger(category));
                    Volatile.Write(ref _binding, binding);
                }
                return binding.Logger;
            }
        }

        private sealed record Binding(ILoggerFactory Factory, ILogger Logger);
    }
}
