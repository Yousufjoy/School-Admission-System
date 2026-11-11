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
    private readonly InProcessLockRegistry _lockRegistry;

    public HoldsController(AppDbContext dbContext, InProcessLockRegistry lockRegistry)
    {
        _dbContext = dbContext;
        _lockRegistry = lockRegistry;
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

        await using var lockScope = await _dbContext.AcquireHoldForUpdateAsync(
            validHoldId,
            _lockRegistry,
            cancellationToken);

        var (hold, errorResult) = ValidateOwnedHold(lockScope.Entity, validHoldId, studentId);
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
        await lockScope.CommitAsync(cancellationToken);

        return Ok(new ConfirmHoldResponse(hold.Id, HoldStatuses.Confirmed));
    }

    [HttpDelete("{holdId}")]
    public async Task<IActionResult> CancelHold(string holdId, CancellationToken cancellationToken)
    {
        var studentId = AdmissionHelpers.RequireValidStudentId(Request);
        var validHoldId = AdmissionHelpers.ValidateRouteId(holdId, nameof(holdId));

        await using var lockScope = await _dbContext.AcquireHoldForUpdateAsync(
            validHoldId,
            _lockRegistry,
            cancellationToken);

        var (hold, errorResult) = ValidateOwnedHold(lockScope.Entity, validHoldId, studentId);
        if (errorResult is not null)
        {
            return errorResult;
        }

        hold!.Status = HoldStatuses.Cancelled;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await lockScope.CommitAsync(cancellationToken);

        return NoContent();
    }

    private (HoldEntity? Hold, IActionResult? ErrorResult) ValidateOwnedHold(
        HoldEntity? hold,
        string holdId,
        string studentId)
    {
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

