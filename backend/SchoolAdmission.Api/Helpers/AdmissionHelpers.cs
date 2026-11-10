using System.Text.RegularExpressions;
using SchoolAdmission.Api.Middleware;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Helpers;

/// <summary>
/// Shared validation, timestamp normalization, and hold status helpers.
/// </summary>
public static partial class AdmissionHelpers
{
    public const string StudentIdHeaderName = "X-Student-Id";
    private const int MaxRouteIdLength = 32;

    [GeneratedRegex("^[0-9]{17}$")]
    private static partial Regex StudentIdRegex();

    /// <summary>
    /// Extracts and validates the required 17-digit X-Student-Id request header.
    /// </summary>
    public static string RequireValidStudentId(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(StudentIdHeaderName, out var values) || values.Count != 1)
        {
            throw new ApiException(StatusCodes.Status400BadRequest,
                $"Missing or invalid {StudentIdHeaderName} header. Expected a 17-digit numeric string.");
        }

        var studentId = values[0]?.Trim() ?? string.Empty;
        if (!StudentIdRegex().IsMatch(studentId))
        {
            throw new ApiException(StatusCodes.Status400BadRequest,
                $"Invalid {StudentIdHeaderName} header. Must match ^\\d{{17}}$.");
        }

        return studentId;
    }

    /// <summary>
    /// Validates route identifier parameters so malformed identifiers return 400.
    /// </summary>
    public static string ValidateRouteId(string? id, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(id) ||
            id.Length > MaxRouteIdLength ||
            id.Any(char.IsControl) ||
            id.Any(char.IsWhiteSpace))
        {
            throw new ApiException(StatusCodes.Status400BadRequest,
                $"Invalid route parameter '{parameterName}'.");
        }

        return id;
    }

    /// <summary>
    /// Ensures a <see cref="DateTime"/> carries <see cref="DateTimeKind.Utc"/> for ISO-8601 UTC serialization.
    /// </summary>
    public static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    /// <summary>
    /// Computes the effective hold status, mapping expired HELD entries to EXPIRED.
    /// </summary>
    public static string GetEffectiveStatus(string status, DateTime expiresAt, DateTime utcNow) =>
        status == HoldStatuses.Held && EnsureUtc(expiresAt) <= utcNow
            ? HoldStatuses.Expired
            : status;
}

