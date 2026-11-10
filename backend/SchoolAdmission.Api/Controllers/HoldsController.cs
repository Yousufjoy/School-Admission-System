using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Dtos;
using SchoolAdmission.Api.Helpers;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("holds")]
public sealed class HoldsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public HoldsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("{holdId}")]
    public async Task<IActionResult> GetHold(string holdId, CancellationToken cancellationToken)
    {
        var validHoldId = AdmissionHelpers.ValidateRouteId(holdId, nameof(holdId));

        var hold = await _dbContext.Holds
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == validHoldId, cancellationToken);

        if (hold is null)
        {
            return NotFound(new ApiErrorResponse($"Hold '{validHoldId}' was not found.", StatusCodes.Status404NotFound));
        }

        var effectiveStatus = AdmissionHelpers.GetEffectiveStatus(hold.Status, hold.ExpiresAt, DateTime.UtcNow);

        return Ok(new HoldDetailResponse(
            hold.Id,
            hold.ClassId,
            effectiveStatus,
            AdmissionHelpers.EnsureUtc(hold.ExpiresAt)));
    }

    [HttpPost("{holdId}/confirm")]
    public async Task<IActionResult> ConfirmHold(string holdId, CancellationToken cancellationToken)
    {
        var studentId = AdmissionHelpers.RequireValidStudentId(Request);
        var validHoldId = AdmissionHelpers.ValidateRouteId(holdId, nameof(holdId));

        await using var transaction = await _dbContext.BeginOptionalTransactionAsync(cancellationToken);

        var (hold, errorResult) = await LoadOwnedHoldForUpdateAsync(validHoldId, studentId, cancellationToken);
        if (errorResult is not null)
        {
            return errorResult;
        }

        var effectiveStatus = AdmissionHelpers.GetEffectiveStatus(hold!.Status, hold.ExpiresAt, DateTime.UtcNow);

        if (effectiveStatus == HoldStatuses.Confirmed)
        {
            return Ok(new ConfirmHoldResponse(hold.Id, HoldStatuses.Confirmed));
        }

        if (effectiveStatus is HoldStatuses.Expired or HoldStatuses.Cancelled)
        {
            return Conflict(new ApiErrorResponse(
                $"Hold cannot be confirmed because it is {effectiveStatus}.",
                StatusCodes.Status409Conflict));
        }

        hold.Status = HoldStatuses.Confirmed;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return Ok(new ConfirmHoldResponse(hold.Id, HoldStatuses.Confirmed));
    }

    [HttpDelete("{holdId}")]
    public async Task<IActionResult> CancelHold(string holdId, CancellationToken cancellationToken)
    {
        var studentId = AdmissionHelpers.RequireValidStudentId(Request);
        var validHoldId = AdmissionHelpers.ValidateRouteId(holdId, nameof(holdId));

        await using var transaction = await _dbContext.BeginOptionalTransactionAsync(cancellationToken);

        var (hold, errorResult) = await LoadOwnedHoldForUpdateAsync(validHoldId, studentId, cancellationToken);
        if (errorResult is not null)
        {
            return errorResult;
        }

        hold!.Status = HoldStatuses.Cancelled;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return NoContent();
    }

    private async Task<(HoldEntity? Hold, IActionResult? ErrorResult)> LoadOwnedHoldForUpdateAsync(
        string holdId,
        string studentId,
        CancellationToken cancellationToken)
    {
        var hold = await _dbContext.FindHoldForUpdateAsync(holdId, cancellationToken);
        if (hold is null)
        {
            return (null, NotFound(new ApiErrorResponse($"Hold '{holdId}' was not found.", StatusCodes.Status404NotFound)));
        }

        if (!string.Equals(hold.StudentId, studentId, StringComparison.Ordinal))
        {
            return (null, StatusCode(
                StatusCodes.Status403Forbidden,
                new ApiErrorResponse("You do not own this hold.", StatusCodes.Status403Forbidden)));
        }

        return (hold, null);
    }
}

