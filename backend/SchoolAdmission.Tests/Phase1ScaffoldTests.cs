using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Tests;

public partial class Phase1ScaffoldTests
{
    [GeneratedRegex("^c_[0-9a-f]{8}$")]
    private static partial Regex ClassIdRegex();

    [GeneratedRegex("^h_[0-9a-f]{8}$")]
    private static partial Regex HoldIdRegex();

    [Fact]
    public void ClassEntity_Defaults_MatchExpectedFormatAndUtc()
    {
        var entity = new ClassEntity
        {
            Name = "Grade 1A",
            TotalSeats = 30
        };

        Assert.Matches(ClassIdRegex(), entity.Id);
        Assert.Equal(DateTimeKind.Utc, entity.CreatedAt.Kind);
        Assert.NotNull(entity.Holds);
        Assert.Empty(entity.Holds);
    }

    [Fact]
    public void HoldEntity_Defaults_MatchExpectedFormatStatusAndUtc()
    {
        var entity = new HoldEntity
        {
            ClassId = "c_1234abcd",
            StudentId = "STU-2026-00000001",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };

        Assert.Matches(HoldIdRegex(), entity.Id);
        Assert.Equal(HoldStatuses.Held, entity.Status);
        Assert.Equal(DateTimeKind.Utc, entity.CreatedAt.Kind);
        Assert.Equal(17, entity.StudentId.Length);
        Assert.Equal(
            new[] { "HELD", "CONFIRMED", "EXPIRED", "CANCELLED" },
            HoldStatuses.All);
    }

    [Fact]
    public void AppDbContext_ModelConfiguration_ConfiguresEntitiesForeignKeyAndConstraints()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(Program.DefaultConnectionString)
            .Options;

        using var dbContext = new AppDbContext(options);

        var designTimeModel = dbContext.GetService<IDesignTimeModel>().Model;
        var classEntityType = designTimeModel.FindEntityType(typeof(ClassEntity));
        var holdEntityType = designTimeModel.FindEntityType(typeof(HoldEntity));

        Assert.NotNull(classEntityType);
        Assert.NotNull(holdEntityType);

        var classPk = classEntityType!.FindPrimaryKey();
        Assert.NotNull(classPk);
        Assert.Single(classPk!.Properties);
        Assert.Equal(nameof(ClassEntity.Id), classPk.Properties[0].Name);

        var studentIdProperty = holdEntityType!.FindProperty(nameof(HoldEntity.StudentId));
        Assert.NotNull(studentIdProperty);
        Assert.Equal(17, studentIdProperty!.GetMaxLength());
        Assert.False(studentIdProperty.IsNullable);

        var foreignKeys = holdEntityType.GetForeignKeys().ToList();
        Assert.Single(foreignKeys);
        Assert.Equal(classEntityType, foreignKeys[0].PrincipalEntityType);
        Assert.Equal(nameof(HoldEntity.ClassId), foreignKeys[0].Properties[0].Name);
        Assert.Equal(DeleteBehavior.Cascade, foreignKeys[0].DeleteBehavior);

        var checkConstraints = holdEntityType.GetCheckConstraints().ToList();
        Assert.Contains(checkConstraints, c => c.Name == "CK_Holds_Status");
    }

    [Fact]
    public void ResolveConnectionString_UsesDefault_WhenUnconfigured()
    {
        var originalEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
            var configuration = new ConfigurationBuilder().Build();

            var resolved = Program.ResolveConnectionString(configuration);

            Assert.Equal(
                "Host=localhost;Port=5432;Database=admission_db;Username=postgres;Password=postgrespassword",
                resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", originalEnv);
        }
    }

    [Fact]
    public void ResolveConnectionString_PrefersEnvironmentVariable()
    {
        const string expected = "Host=db;Port=5432;Database=custom_db;Username=app;Password=secret";
        var originalEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", expected);
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var resolved = Program.ResolveConnectionString(configuration);

            Assert.Equal(expected, resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", originalEnv);
        }
    }
}

