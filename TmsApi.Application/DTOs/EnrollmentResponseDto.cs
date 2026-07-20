using System;

namespace TmsApi.Application.DTOs;

// 🟢 The clean C# way your learning module expects:
public record EnrollmentResponseDto(
    int Id,
    int CourseId,
    int StudentId,
    DateTime EnrolledAt,
    CourseScheduleInfoDto Course // This allows e.Course.Code and e.Course.Title to work naturally!
);

// A simple DTO representation of the related Course details for queries
public record CourseScheduleInfoDto(string Code, string Title);