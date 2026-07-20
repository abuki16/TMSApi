using System.Collections.Generic;

namespace TmsApi.Application.DTOs;

public record CourseResponseDto(
    int Id,
    string Code,
    string Title,
    int MaxCapacity,
    int EnrollmentCount,
    // a collection here so .Enrollments.Count evaluates naturally
    IReadOnlyCollection<EnrollmentItemDto> Enrollments
);

// A simple nested DTO representing individual enrollment instances inside a course view
public record EnrollmentItemDto(int Id, int StudentId);