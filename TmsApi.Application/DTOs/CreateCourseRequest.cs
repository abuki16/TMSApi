using System.ComponentModel.DataAnnotations;

namespace TmsApi.Application.DTOs;
public record CreateCourseRequest
{
    [Required]
    [RegularExpression(@"^[A-Z]{2,4}-\d{3}$", ErrorMessage = "Code must follow the pattern (e.g., AI-101, CSE-101).")]
    public required string Code { get; init; }

    [Required]
    [MaxLength(200)]
    public required string Title { get; init; }

    [Range(1, 200)]
    public int MaxCapacity { get; init; }
}