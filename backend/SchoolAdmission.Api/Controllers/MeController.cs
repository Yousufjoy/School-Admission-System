using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Dtos;
using SchoolAdmission.Api.Helpers;

namespace SchoolAdmission.Api.Controllers;

[ApiController]
[Route("me")]
public sealed class MeController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public MeController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("holds")]
    public async Task<IActionResult> GetMyHolds(CancellationToken cancellationToken)
    {
        var studentId = AdmissionHelpers.RequireValidStudentId(Request);
        var now = DateTime.UtcNow;

        var rows = await _dbContext.Holds
            .AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new
            {
                h.Id,
                h.ClassId,
                ClassName = h.Class != null ? h.Class.Name : string.Empty,
                h.Status,
                h.ExpiresAt
            })
            .ToListAsync(cancellationToken);

        var response = rows.Select(row => new StudentHoldResponse(
            row.Id,
            row.ClassId,
            row.ClassName,
            AdmissionHelpers.GetEffectiveStatus(row.Status, row.ExpiresAt, now),
            AdmissionHelpers.EnsureUtc(row.ExpiresAt))).ToList();

        return Ok(response);
    }
}

