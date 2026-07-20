using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;

namespace TmsApi.Application.Interfaces;

public interface IAssessmentService
{
    Task<Assessment> CreateAssessmentAsync(Assessment assessment);

    Task<Assessment?> GetByIdAsync(int id);

    Task<Assessment?> UpdateScoreAsync(int id, decimal newMaxScore);

    Task<IReadOnlyList<AssessmentResponseDto>> GetByCourseAsync(int courseId);

    Task<bool> DeleteAssessmentAsync(int id);
}