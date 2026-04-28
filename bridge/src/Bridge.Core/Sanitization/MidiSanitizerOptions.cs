namespace Bridge.Core.Sanitization;

public sealed record MidiSanitizerOptions(
    TimeSpan CoalesceWindow,
    int MaxMessagesPerSecondPerRoute
)
{
    public static MidiSanitizerOptions Default { get; } = new(
        CoalesceWindow: TimeSpan.FromMilliseconds(5),
        MaxMessagesPerSecondPerRoute: 4000
    );
}

