using TmsApi.Application.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TmsApi.Domain.Entities;

namespace TmsApi.Application.Interfaces;

public interface IEnrollmentService
{
    Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct);
    Task<EnrollmentResponseDto> CreateAsync(int courseId, EnrollStudentRequest request, CancellationToken ct);
    Task<IReadOnlyList<EnrollmentResponseDto>> GetByCourseAsync(int courseId, CancellationToken ct);

    // FOR YOUR CQRS HANDLERS
    Task<IEnumerable<EnrollmentResponseDto>> GetByStudentIdAsync(int studentId, CancellationToken ct);
    Task<bool> ExistsAsync(int studentId, string courseCode, CancellationToken ct);
   
   // Accepts the concrete entity mapping from the CQRS workflow
    Task AddAsync(Enrollment enrollment, CancellationToken ct);
}