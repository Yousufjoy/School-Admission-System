using System.ComponentModel.DataAnnotations;

namespace SchoolAdmission.Api.Models;

/// <summary>
/// Represents an academic class with a defined total seat capacity.
/// </summary>
public class ClassEntity
{
    [Key]
    [MaxLength(32)]
    public string Id { get; set; } = IdGenerator.NewPrefixedId("c");

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public int TotalSeats { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<HoldEntity> Holds { get; set; } = new List<HoldEntity>();
}

