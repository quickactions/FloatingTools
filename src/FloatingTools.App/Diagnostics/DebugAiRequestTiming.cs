using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace FloatingTools.App.Diagnostics;

internal sealed class DebugAiRequestTiming : IDisposable
{
    private static readonly DebugAiRequestTiming Disabled = new();

    private readonly bool _enabled;
    private readonly string _tool = string.Empty;
    private readonly string _operation = string.Empty;
    private readonly Func<long> _getTimestamp = static () => 0;
    private readonly Action<string> _writeLine = static _ => { };
    private readonly long _frequency = 1;
    private readonly long _started;
    private readonly List<Phase> _phases = [];
    private string? _model;
    private bool? _hasImages;
    private bool _completed;

    private DebugAiRequestTiming()
    {
    }

    private DebugAiRequestTiming(
        string tool,
        string operation,
        Func<long> getTimestamp,
        Action<string> writeLine,
        long frequency,
        bool enabled)
    {
        _enabled = enabled;
        _tool = Sanitize(tool);
        _operation = Sanitize(operation);
        _getTimestamp = getTimestamp;
        _writeLine = writeLine;
        _frequency = frequency > 0 ? frequency : 1;
        _started = enabled ? getTimestamp() : 0;
    }

    public static DebugAiRequestTiming Start(string tool, string operation)
    {
#if DEBUG
        return new DebugAiRequestTiming(
            tool,
            operation,
            Stopwatch.GetTimestamp,
            static line => Debug.WriteLine(line),
            Stopwatch.Frequency,
            enabled: true);
#else
        return Disabled;
#endif
    }

    internal static DebugAiRequestTiming CreateForTest(
        string tool,
        string operation,
        Func<long> getTimestamp,
        Action<string> writeLine,
        long frequency = 1_000,
        bool enabled = true) =>
        new(tool, operation, getTimestamp, writeLine, frequency, enabled);

    [Conditional("DEBUG")]
    public void SetModel(string model)
    {
        if (_enabled && !_completed)
        {
            _model = Sanitize(model);
        }
    }

    [Conditional("DEBUG")]
    public void SetHasImages(bool hasImages)
    {
        if (_enabled && !_completed)
        {
            _hasImages = hasImages;
        }
    }

    [Conditional("DEBUG")]
    public void Mark(string phase)
    {
        if (_enabled && !_completed)
        {
            _phases.Add(new Phase(Sanitize(phase), _getTimestamp()));
        }
    }

    [Conditional("DEBUG")]
    public void Complete(string outcome)
    {
        if (!_enabled || _completed)
        {
            return;
        }

        _completed = true;
        var completedAt = _getTimestamp();
        var output = new StringBuilder("FloatingTools AI timing:")
            .Append(" tool=").Append(_tool)
            .Append("; operation=").Append(_operation)
            .Append("; outcome=").Append(Sanitize(outcome));
        if (_model is not null)
        {
            output.Append("; model=").Append(_model);
        }

        if (_hasImages is not null)
        {
            output.Append("; hasImages=")
                .Append(_hasImages.Value ? "true" : "false");
        }

        output.Append("; totalMs=").Append(FormatMilliseconds(completedAt - _started));
        output.Append("; phases=");
        var previous = _started;
        for (var index = 0; index < _phases.Count; index++)
        {
            var phase = _phases[index];
            if (index > 0)
            {
                output.Append(',');
            }

            output.Append(phase.Name)
                .Append('@').Append(FormatMilliseconds(phase.Timestamp - _started))
                .Append("(+").Append(FormatMilliseconds(phase.Timestamp - previous))
                .Append(')');
            previous = phase.Timestamp;
        }

        _writeLine(output.ToString());
    }

    public void Dispose()
    {
#if DEBUG
        Complete("incomplete");
#endif
    }

    private string FormatMilliseconds(long timestampDelta) =>
        ((double)timestampDelta * 1_000 / _frequency)
        .ToString("F1", CultureInfo.InvariantCulture);

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var output = new StringBuilder(Math.Min(value.Length, 64));
        foreach (var character in value)
        {
            if (output.Length == 64)
            {
                break;
            }

            output.Append(char.IsAsciiLetterOrDigit(character)
                          || character is '.' or '_' or '-'
                ? character
                : '_');
        }

        return output.ToString();
    }

    private sealed record Phase(string Name, long Timestamp);
}
