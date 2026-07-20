using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;

namespace TmsApi.Application.Interfaces;

public interface IAssessmentResultService
{
    Task<AssessmentResult?> GetByIdAsync(int id);

    Task<IReadOnlyList<AssessmentResultResponseDto>> GetByAssessmentAsync(int assessmentId);

    Task<AssessmentResult> CreateResultAsync(AssessmentResult result);

    Task<AssessmentResult?> UpdateScoreAsync(int id, decimal newScore);

    Task DeleteResultAsync(int id);
}