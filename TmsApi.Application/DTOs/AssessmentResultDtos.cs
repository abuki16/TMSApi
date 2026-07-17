using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TmsApi.Application.DTOs;

// For POST: api/assessments/{assessmentId}/results
public record GradeStudentRequest(
    [Required(ErrorMessage = "Result title is required.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 100 characters.")]
    string Title,

    [Required]
    [Range(0.00, 1000.00, ErrorMessage = "Score obtained cannot be negative or exceed 1000.")]
    decimal ScoreObtained,

    [Required]
    [Range(0.01, 1.00, ErrorMessage = "Weight must be between 0.01 (1%) and 1.00 (100%).")]
    decimal Weight,

    [Required(ErrorMessage = "A valid Student ID must be provided.")]
    int StudentId
);
// For PATCH: api/assessments/{assessmentId}/results/{id}/score
public record UpdateAssessmentResultScoreRequest(
    [Required]
    [Range(0.00, 1000.00, ErrorMessage = "Score obtained cannot be negative.")]
    decimal ScoreObtained
);

// Flat Overview for collection listings
public record AssessmentResultResponseDto(
    int Id,
    string Title,
    decimal ScoreObtained,
    decimal Weight,
    int AssessmentId,
    int StudentId,
    string StudentName
);

// Rich Resource DTO with HATEOAS Links
public record AssessmentResultDetailDto
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required decimal ScoreObtained { get; init; }
    public required decimal Weight { get; init; }
    public required int AssessmentId { get; init; }
    public required int StudentId { get; init; }
    public required IReadOnlyList<LinkDto> Links { get; init; }
}