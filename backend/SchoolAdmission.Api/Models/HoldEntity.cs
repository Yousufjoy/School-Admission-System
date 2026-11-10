using System.ComponentModel.DataAnnotations;

namespace SchoolAdmission.Api.Models;

/// <summary>
/// Represents a temporary or finalized seat hold for a student in a class.
/// </summary>
public class HoldEntity
{
    [Key]
    [MaxLength(32)]
    public string Id { get; set; } = IdGenerator.NewPrefixedId("h");

    [Required]
    [MaxLength(32)]
    public string ClassId { get; set; } = string.Empty;

    [Required]
    [StringLength(17, MinimumLength = 17)]
    public string StudentId { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string Status { get; set; } = HoldStatuses.Held;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ClassEntity? Class { get; set; }
}

