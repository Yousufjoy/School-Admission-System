using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Data;

/// <summary>
/// Entity Framework Core database context for the school admission system.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ClassEntity> Classes => Set<ClassEntity>();
    public DbSet<HoldEntity> Holds => Set<HoldEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ClassEntity>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasMaxLength(32).IsRequired();
            entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
            entity.Property(c => c.TotalSeats).IsRequired();
            entity.Property(c => c.CreatedAt).IsRequired();
        });

        var allowedStatusesSql = string.Join(", ", HoldStatuses.All.Select(s => $"'{s}'"));

        modelBuilder.Entity<HoldEntity>(entity =>
        {
            entity.ToTable(t =>
                t.HasCheckConstraint("CK_Holds_Status", $"\"Status\" IN ({allowedStatusesSql})"));

            entity.HasKey(h => h.Id);
            entity.Property(h => h.Id).HasMaxLength(32).IsRequired();
            entity.Property(h => h.ClassId).HasMaxLength(32).IsRequired();
            entity.Property(h => h.StudentId).HasMaxLength(17).IsRequired();
            entity.Property(h => h.Status).HasMaxLength(16).IsRequired();
            entity.Property(h => h.ExpiresAt).IsRequired();
            entity.Property(h => h.CreatedAt).IsRequired();

            entity.HasOne(h => h.Class)
                .WithMany(c => c.Holds)
                .HasForeignKey(h => h.ClassId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(h => h.ClassId);
            entity.HasIndex(h => new { h.ClassId, h.Status });
            entity.HasIndex(h => new { h.ClassId, h.StudentId, h.Status });
        });
    }
}

