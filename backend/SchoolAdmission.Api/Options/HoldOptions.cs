namespace SchoolAdmission.Api.Options;

/// <summary>
/// Configuration options for seat hold behavior.
/// </summary>
public sealed class HoldOptions
{
    public const int DefaultHoldDurationSeconds = 600;

    public int HoldDurationSeconds { get; init; } = DefaultHoldDurationSeconds;

    /// <summary>
    /// Resolves hold duration in seconds from configuration or environment variables, defaulting to 600.
    /// </summary>
    public static int ResolveHoldDurationSeconds(IConfiguration configuration)
    {
        var raw = configuration["HOLD_DURATION_SECONDS"]
            ?? Environment.GetEnvironmentVariable("HOLD_DURATION_SECONDS");

        return int.TryParse(raw, out var seconds) && seconds > 0
            ? seconds
            : DefaultHoldDurationSeconds;
    }
}

