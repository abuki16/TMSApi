using System;

namespace TmsApi.Application.DTOs;

public record EnrollmentResponseDto(
    int Id,
    int StudentId,
    string StudentName,
    int CourseId,
    string CourseName,
    string Status,
    DateTime EnrolledAt,
    CourseScheduleInfoDto Course
);

public record CourseScheduleInfoDto(
    string Code, 
    string Title,
    string Name
);