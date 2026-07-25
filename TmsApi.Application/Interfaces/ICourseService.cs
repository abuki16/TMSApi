using TmsApi.Domain.Entities;
using TmsApi.Application.DTOs;
using System.Threading;
using System.Threading.Tasks;
using TmsApi.Application.Common;

namespace TmsApi.Application.Interfaces;

public interface ICourseService
{
    Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct);
    
    // 🟢 ADD THIS LINE FOR THE ENROLL HANDLER
    Task<CourseResponseDto?> GetByCodeAsync(string courseCode, CancellationToken ct);

    Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(PagedRequest request, CancellationToken ct);
    Task UpdateAsync(int id, UpdateCourseRequest request, CancellationToken ct);
    Task<List<CourseResponseDto>> GetAllAsync(CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);

    
} 