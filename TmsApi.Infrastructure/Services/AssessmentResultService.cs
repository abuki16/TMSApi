using Microsoft.EntityFrameworkCore;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;


namespace TmsApi.Infrastructure.Services;

public class AssessmentResultService(TmsDbContext dbContext) : IAssessmentResultService
{
    public async Task<AssessmentResult?> GetByIdAsync(int id)
    {
        return await dbContext.AssessmentResults
            .Include(ar => ar.Assessment)
            .Include(ar => ar.Student)
            .FirstOrDefaultAsync(ar => ar.Id == id);
    }

    public async Task<IReadOnlyList<AssessmentResultResponseDto>> GetByAssessmentAsync(int assessmentId)
    {
        return await dbContext.AssessmentResults
            .AsNoTracking()
            .Where(ar => ar.AssessmentId == assessmentId)
            .Select(ar => new AssessmentResultResponseDto(
                ar.Id,
                ar.Title,
                ar.ScoreObtained,
                ar.Weight,
                ar.AssessmentId,
                ar.StudentId,
                ar.Student.Name
            ))
            .ToListAsync();
    }

    public async Task<AssessmentResult> CreateResultAsync(AssessmentResult result)
    {
        // Verify the assessment exists
        var parentAssessment = await dbContext.Assessments.FindAsync(result.AssessmentId);

        if (parentAssessment is null)
        {
            throw new ArgumentException(
                $"Assessment definition with ID {result.AssessmentId} does not exist.");
        }

        // Ensure the score does not exceed the assessment's maximum score
        if (result.ScoreObtained > parentAssessment.MaxScore)
        {
            throw new ArgumentException(
                $"Score obtained ({result.ScoreObtained}) cannot exceed the maximum assessment ceiling of {parentAssessment.MaxScore}.");
        }

        // Verify the student exists
        var studentExists = await dbContext.Students
            .AnyAsync(s => s.Id == result.StudentId);

        if (!studentExists)
        {
            throw new ArgumentException(
                $"Student with ID {result.StudentId} does not exist.");
        }

        // Verify the student is enrolled in the course
        var isEnrolled = await dbContext.Enrollments.AnyAsync(e =>
            e.StudentId == result.StudentId &&
            e.CourseId == parentAssessment.CourseId);

        if (!isEnrolled)
        {
            throw new InvalidOperationException(
                $"Student with ID {result.StudentId} is not enrolled in course ID {parentAssessment.CourseId}.");
        }

        // Prevent duplicate results
        var alreadyGraded = await dbContext.AssessmentResults.AnyAsync(ar =>
            ar.AssessmentId == result.AssessmentId &&
            ar.StudentId == result.StudentId);

        if (alreadyGraded)
        {
            throw new InvalidOperationException(
                $"Student ID {result.StudentId} has already been graded for this assessment.");
        }

        // Prevent EF Core from inserting related entities again
        result.Assessment = null!;
        result.Student = null!;

        await dbContext.AssessmentResults.AddAsync(result);
        await dbContext.SaveChangesAsync();

        return result;
    }

    public async Task<AssessmentResult?> UpdateScoreAsync(int id, decimal newScore)
    {
        var result = await dbContext.AssessmentResults
            .Include(ar => ar.Assessment)
            .FirstOrDefaultAsync(ar => ar.Id == id);

        if (result is null)
            return null;

        if (newScore > result.Assessment.MaxScore)
        {
            throw new ArgumentException(
                $"Score obtained ({newScore}) cannot exceed the maximum assessment ceiling of {result.Assessment.MaxScore}.");
        }

        result.ScoreObtained = newScore;

        await dbContext.SaveChangesAsync();

        return result;
    }

    public async Task DeleteResultAsync(int id)
    {
        var result = await dbContext.AssessmentResults.FindAsync(id);

        if (result is null)
            return;

        dbContext.AssessmentResults.Remove(result);

        await dbContext.SaveChangesAsync();
    }
}