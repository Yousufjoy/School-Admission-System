namespace SchoolAdmission.Api.Models;

/// <summary>
/// Defines allowed status values for <see cref="HoldEntity"/>.
/// </summary>
public static class HoldStatuses
{
    public const string Held = "HELD";
    public const string Confirmed = "CONFIRMED";
    public const string Expired = "EXPIRED";
    public const string Cancelled = "CANCELLED";

    public static readonly IReadOnlyList<string> All =
    [
        Held,
        Confirmed,
        Expired,
        Cancelled
    ];
}

