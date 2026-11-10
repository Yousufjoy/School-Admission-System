using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SchoolAdmission.Api.Dtos;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Data;

/// <summary>
/// Shared EF Core query and row-locking helpers for PostgreSQL and test providers.
/// </summary>
public static class AdmissionDbExtensions
{
    public static async Task<IDbContextTransaction?> BeginOptionalTransactionAsync(
        this AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
    }

    public static async Task<ClassEntity?> FindClassForUpdateAsync(
        this AppDbContext dbContext,
        string classId,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsNpgsql())
        {
            var results = await dbContext.Classes
                .FromSqlRaw(
                    "SELECT * FROM \"Classes\" WHERE \"Id\" = @classId FOR UPDATE",
                    new NpgsqlParameter("@classId", classId))
                .ToListAsync(cancellationToken);

            return results.FirstOrDefault();
        }

        return await dbContext.Classes
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);
    }

    public static async Task<HoldEntity?> FindHoldForUpdateAsync(
        this AppDbContext dbContext,
        string holdId,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsNpgsql())
        {
            var results = await dbContext.Holds
                .FromSqlRaw(
                    "SELECT * FROM \"Holds\" WHERE \"Id\" = @holdId FOR UPDATE",
                    new NpgsqlParameter("@holdId", holdId))
                .ToListAsync(cancellationToken);

            return results.FirstOrDefault();
        }

        return await dbContext.Holds
            .FirstOrDefaultAsync(h => h.Id == holdId, cancellationToken);
    }

    public static IQueryable<ClassSummaryResponse> ProjectClassSummaries(
        this IQueryable<ClassEntity> query,
        DateTime utcNow)
    {
        return query.Select(c => new ClassSummaryResponse(
            c.Id,
            c.Name,
            c.TotalSeats,
            c.Holds.Count(h => h.Status == HoldStatuses.Held && h.ExpiresAt > utcNow),
            c.Holds.Count(h => h.Status == HoldStatuses.Confirmed),
            Math.Max(
                0,
                c.TotalSeats
                - c.Holds.Count(h => h.Status == HoldStatuses.Held && h.ExpiresAt > utcNow)
                - c.Holds.Count(h => h.Status == HoldStatuses.Confirmed))));
    }
}
