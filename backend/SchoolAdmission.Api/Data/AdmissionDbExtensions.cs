using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SchoolAdmission.Api.Dtos;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Data;

/// <summary>
/// Represents a transaction and row-lock scope for an entity lookup.
/// </summary>
public sealed class LockedEntityScope<TEntity> : IAsyncDisposable where TEntity : class
{
    private readonly IDbContextTransaction? _transaction;
    private readonly IAsyncDisposable? _inProcessLock;

    public LockedEntityScope(
        TEntity? entity,
        IDbContextTransaction? transaction,
        IAsyncDisposable? inProcessLock)
    {
        Entity = entity;
        _transaction = transaction;
        _inProcessLock = inProcessLock;
    }

    public TEntity? Entity { get; }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            await _transaction.CommitAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
        }

        if (_inProcessLock is not null)
        {
            await _inProcessLock.DisposeAsync();
        }
    }
}

/// <summary>
/// Shared EF Core query and row-locking helpers for PostgreSQL and test providers.
/// </summary>
public static class AdmissionDbExtensions
{
    public static Task<LockedEntityScope<ClassEntity>> AcquireClassForUpdateAsync(
        this AppDbContext dbContext,
        string classId,
        InProcessLockRegistry lockRegistry,
        CancellationToken cancellationToken)
    {
        return AcquireEntityForUpdateAsync(
            dbContext,
            $"class:{classId}",
            lockRegistry,
            () => dbContext.Classes
                .FromSqlRaw(
                    "SELECT * FROM \"Classes\" WHERE \"Id\" = @classId FOR UPDATE",
                    new NpgsqlParameter("@classId", classId))
                .ToListAsync(cancellationToken),
            () => dbContext.Classes.FirstOrDefaultAsync(c => c.Id == classId, cancellationToken),
            cancellationToken);
    }

    public static Task<LockedEntityScope<HoldEntity>> AcquireHoldForUpdateAsync(
        this AppDbContext dbContext,
        string holdId,
        InProcessLockRegistry lockRegistry,
        CancellationToken cancellationToken)
    {
        return AcquireEntityForUpdateAsync(
            dbContext,
            $"hold:{holdId}",
            lockRegistry,
            () => dbContext.Holds
                .FromSqlRaw(
                    "SELECT * FROM \"Holds\" WHERE \"Id\" = @holdId FOR UPDATE",
                    new NpgsqlParameter("@holdId", holdId))
                .ToListAsync(cancellationToken),
            () => dbContext.Holds.FirstOrDefaultAsync(h => h.Id == holdId, cancellationToken),
            cancellationToken);
    }

    private static async Task<LockedEntityScope<TEntity>> AcquireEntityForUpdateAsync<TEntity>(
        AppDbContext dbContext,
        string lockKey,
        InProcessLockRegistry lockRegistry,
        Func<Task<List<TEntity>>> npgsqlForUpdateQuery,
        Func<Task<TEntity?>> fallbackQuery,
        CancellationToken cancellationToken) where TEntity : class
    {
        if (dbContext.Database.IsNpgsql())
        {
            var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var rows = await npgsqlForUpdateQuery();
                return new LockedEntityScope<TEntity>(rows.FirstOrDefault(), transaction, null);
            }
            catch
            {
                await transaction.DisposeAsync();
                throw;
            }
        }

        var lockHandle = await lockRegistry.AcquireAsync(lockKey, cancellationToken);
        IDbContextTransaction? fallbackTransaction = null;
        try
        {
            if (dbContext.Database.IsRelational())
            {
                fallbackTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            }

            var entity = await fallbackQuery();
            return new LockedEntityScope<TEntity>(entity, fallbackTransaction, lockHandle);
        }
        catch
        {
            if (fallbackTransaction is not null)
            {
                await fallbackTransaction.DisposeAsync();
            }

            await lockHandle.DisposeAsync();
            throw;
        }
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
