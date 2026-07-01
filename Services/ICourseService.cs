 using TmsApi.Entities;
using Tms.Api.Dtos;
namespace TmsApi.Services;

// public interface ICourseService
// {
//     Task<Course?> GetByIdAsync(int id, CancellationToken ct);
//     Task<Course> CreateAsync(Course course, CancellationToken ct);
// }

public interface ICourseService
{
Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct);
Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct);
Task<bool> CodeExistsAsync(string code, CancellationToken ct);
}