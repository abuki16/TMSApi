using System.ComponentModel.DataAnnotations;

namespace TmsApi.Domain.Entities;

public class Course
{
    public int Id { get; set; } // surrogate primary key — internal, used by foreign keys

    [Required(ErrorMessage = "Course code is required.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Course code must be between 2 and 50 characters.")]
    public required string Code { get; set; } // natural key — human-readable

    [Required(ErrorMessage = "Course title is required.")]
    [StringLength(100, ErrorMessage = "Course title cannot exceed 100 characters.")]
    public required string Title { get; set; }

    [Range(1, 1000, ErrorMessage = "Max capacity must be between 1 and 1000.")]
    public int MaxCapacity { get; set; }
    
    public int EnrollmentCount { get; set; }
    
    // Add this property to track course ownership
    public string? InstructorId { get; set; }

    // Navigation property for many-to-many relationship
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}