using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TmsApi.Application.DTOs;

// 1. Used for POST: api/courses/{courseId}/assessments (Strictly Definition)
public record CreateAssessmentRequest(
    [Required(ErrorMessage = "Assessment title is required.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 100 characters.")]
    string Title,

    [Required]
    [Range(0.01, 1000.00, ErrorMessage = "Max score must be greater than 0 and less than 1000.")]
    decimal MaxScore,

    [Required]
    [Range(0.01, 1.00, ErrorMessage = "Weight must be a fraction between 0.01 (1%) and 1.00 (100%).")]
    decimal Weight
);

// 2. Used for PATCH: api/courses/{courseId}/assessments/{id}/max-score
public record UpdateAssessmentMaxScoreRequest(
    [Required]
    [Range(0.01, 1000.00, ErrorMessage = "Max score must be greater than 0 and less than 1000.")]
    decimal MaxScore
);

// 3. Flat Overview DTO used for listing collections cleanly
public record AssessmentResponseDto(
    int Id,
    string Title,
    decimal MaxScore,
    decimal Weight,
    int CourseId
);

// 4. Detailed HATEOAS resource DTO (Matches CourseDetailDto structural standard)
public record AssessmentDetailDto
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required decimal MaxScore { get; init; }
    public required decimal Weight { get; init; }
    public required int CourseId { get; init; }
    public required IReadOnlyList<LinkDto> Links { get; init; }
}
