using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Dtos;
using SchoolAdmission.Api.Helpers;
using SchoolAdmission.Api.Models;
using SchoolAdmission.Api.Options;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("classes")]
public sealed class ClassesController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly HoldOptions _holdOptions;

    public ClassesController(AppDbContext dbContext, HoldOptions holdOptions)
    {
        _dbContext = dbContext;
        _holdOptions = holdOptions;
    }

    [HttpPost]
    public async Task<IActionResult> CreateClass(
        [FromBody] CreateClassRequest request,
        CancellationToken cancellationToken)
    {
        var trimmedName = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedName))
        {
            return BadRequest(new ApiErrorResponse("Class name is required.", StatusCodes.Status400BadRequest));
        }

        var entity = new ClassEntity
        {
            Name = trimmedName,
            TotalSeats = request.TotalSeats!.Value,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Classes.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new CreateClassResponse(entity.Id, entity.Name, entity.TotalSeats);
        return CreatedAtAction(nameof(GetClassById), new { classId = entity.Id }, response);
    }

    [HttpGet]
    public async Task<IActionResult> GetClasses(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var classes = await _dbContext.Classes
            .AsNoTracking()
            .OrderBy(c => c.CreatedAt)
            .ProjectClassSummaries(now)
            .ToListAsync(cancellationToken);

        return Ok(classes);
    }

    [HttpGet("{classId}")]
    public async Task<IActionResult> GetClassById(string classId, CancellationToken cancellationToken)
    {
        var validClassId = AdmissionHelpers.ValidateRouteId(classId, nameof(classId));
        var now = DateTime.UtcNow;

        var summary = await _dbContext.Classes
            .AsNoTracking()
            .Where(c => c.Id == validClassId)
            .ProjectClassSummaries(now)
            .FirstOrDefaultAsync(cancellationToken);

        if (summary is null)
        {
            return NotFound(new ApiErrorResponse($"Class '{validClassId}' was not found.", StatusCodes.Status404NotFound));
        }

        return Ok(summary);
    }

    [HttpPost("{classId}/holds")]
    public async Task<IActionResult> CreateHold(string classId, CancellationToken cancellationToken)
    {
        var studentId = AdmissionHelpers.RequireValidStudentId(Request);
        var validClassId = AdmissionHelpers.ValidateRouteId(classId, nameof(classId));

        await using var transaction = await _dbContext.BeginOptionalTransactionAsync(cancellationToken);

        var targetClass = await _dbContext.FindClassForUpdateAsync(validClassId, cancellationToken);
        if (targetClass is null)
        {
            return NotFound(new ApiErrorResponse($"Class '{validClassId}' was not found.", StatusCodes.Status404NotFound));
        }

        var now = DateTime.UtcNow;

        var studentAlreadyHoldsSeat = await _dbContext.Holds.AnyAsync(
            h => h.ClassId == validClassId &&
                 h.StudentId == studentId &&
                 (h.Status == HoldStatuses.Confirmed ||
                  (h.Status == HoldStatuses.Held && h.ExpiresAt > now)),
            cancellationToken);

        if (studentAlreadyHoldsSeat)
        {
            return Conflict(new ApiErrorResponse(
                "Student already has an active or confirmed hold for this class.",
                StatusCodes.Status409Conflict));
        }

        var occupiedSeats = await _dbContext.Holds.CountAsync(
            h => h.ClassId == validClassId &&
                 (h.Status == HoldStatuses.Confirmed ||
                  (h.Status == HoldStatuses.Held && h.ExpiresAt > now)),
            cancellationToken);

        if (occupiedSeats >= targetClass.TotalSeats)
        {
            return Conflict(new ApiErrorResponse(
                "Class has no available seats remaining.",
                StatusCodes.Status409Conflict));
        }

        var hold = new HoldEntity
        {
            ClassId = targetClass.Id,
            StudentId = studentId,
            Status = HoldStatuses.Held,
            CreatedAt = now,
            ExpiresAt = now.AddSeconds(_holdOptions.HoldDurationSeconds)
        };

        _dbContext.Holds.Add(hold);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        var response = new CreateHoldResponse(
            hold.Id,
            hold.ClassId,
            hold.StudentId,
            hold.Status,
            AdmissionHelpers.EnsureUtc(hold.ExpiresAt));

        return Created($"/holds/{hold.Id}", response);
    }
}

