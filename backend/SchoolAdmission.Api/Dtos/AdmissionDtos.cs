using System.ComponentModel.DataAnnotations;

namespace SchoolAdmission.Api.Dtos;

public sealed class CreateClassRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(200, ErrorMessage = "Name must not exceed 200 characters.")]
    public string? Name { get; set; }

    [Required(ErrorMessage = "TotalSeats is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "TotalSeats must be at least 1.")]
    public int? TotalSeats { get; set; }
}

public sealed record CreateClassResponse(
    string Id,
    string Name,
    int TotalSeats);

public sealed record ClassSummaryResponse(
    string Id,
    string Name,
    int TotalSeats,
    int HeldSeats,
    int ConfirmedSeats,
    int AvailableSeats);

public sealed record CreateHoldResponse(
    string HoldId,
    string ClassId,
    string StudentId,
    string Status,
    DateTime ExpiresAt);

public sealed record HoldDetailResponse(
    string HoldId,
    string ClassId,
    string Status,
    DateTime ExpiresAt);

public sealed record ConfirmHoldResponse(
    string HoldId,
    string Status);

public sealed record StudentHoldResponse(
    string HoldId,
    string ClassId,
    string ClassName,
    string Status,
    DateTime ExpiresAt);

public sealed record ApiErrorResponse(
    string Error,
    int StatusCode,
    IDictionary<string, string[]>? Errors = null);

