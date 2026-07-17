using TmsApi.Domain.Entities;
using TmsApi.Application.DTOs;
namespace TmsApi.Application.Interfaces;

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
//Task<IEnumerable<Course>> GetAllAsync(CancellationToken ct);
Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(PagedRequest request, CancellationToken ct);

    Task UpdateAsync(int id, UpdateCourseRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}