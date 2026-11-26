using System.Collections.Concurrent;
using System.Text;
using Serilog.Core;
using Serilog.Events;

namespace Coworking.IntegrationTests.Fixtures;

/// <summary>
/// A Serilog sink that keeps every log event in memory, so a test can search the logs.
/// Program.cs configures Serilog with <c>ReadFrom.Services(...)</c>, which picks up any
/// <see cref="ILogEventSink"/> registered in DI – a test just registers an instance of this.
/// </summary>
public sealed class CapturingLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyCollection<LogEvent> Events => _events;

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    /// <summary>Everything that could end up in a log file: messages, property values and exceptions.</summary>
    public string AllText()
    {
        var text = new StringBuilder();
        foreach (var logEvent in _events)
        {
            text.AppendLine(logEvent.RenderMessage());
            foreach (var property in logEvent.Properties)
            {
                text.Append(property.Key).Append('=').AppendLine(property.Value.ToString());
            }

            if (logEvent.Exception is not null)
            {
                text.AppendLine(logEvent.Exception.ToString());
            }
        }

        return text.ToString();
    }
}
