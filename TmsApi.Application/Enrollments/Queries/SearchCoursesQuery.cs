using MediatR;
using TmsApi.Application.DTOs;

namespace TmsApi.Application.Courses.Queries;

public record SearchCoursesQuery(string? term) : IRequest<List<CourseResponseDto>>;