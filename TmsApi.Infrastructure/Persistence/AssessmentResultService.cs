using Microsoft.EntityFrameworkCore;
using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TmsApi.Infrastructure.Persistence;

public interface IAssessmentResultService
{
    Task<AssessmentResult?> GetByIdAsync(int id);
    Task<IReadOnlyList<AssessmentResultResponseDto>> GetByAssessmentAsync(int assessmentId);
    Task<AssessmentResult> CreateResultAsync(AssessmentResult result);
    Task<AssessmentResult?> UpdateScoreAsync(int id, decimal newScore);
    Task DeleteResultAsync(int id);
}

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
        // 1. Verify parent definition exists
        var parentAssessment = await dbContext.Assessments.FindAsync(result.AssessmentId);
        if (parentAssessment is null)
        {
            throw new ArgumentException($"Assessment definition with ID {result.AssessmentId} does not exist.");
        }

        // 2. Prevent obtaining a score higher than the definition limits
        if (result.ScoreObtained > parentAssessment.MaxScore)
        {
            throw new ArgumentException($"Score obtained ({result.ScoreObtained}) cannot exceed the maximum assessment ceiling of {parentAssessment.MaxScore}.");
        }

        // 3. Verify student exists
        var studentExists = await dbContext.Students.AnyAsync(s => s.Id == result.StudentId);
        if (!studentExists)
        {
            throw new ArgumentException($"Student with ID {result.StudentId} does not exist.");
        }

        // 4. Verify student is actually enrolled in the course that owns this assessment
        var isEnrolled = await dbContext.Enrollments.AnyAsync(e => 
            e.StudentId == result.StudentId && e.CourseId == parentAssessment.CourseId);

        if (!isEnrolled)
        {
            throw new InvalidOperationException($"Student with ID {result.StudentId} is not enrolled in course ID {parentAssessment.CourseId} associated with this assessment.");
        }

        // 5. Ensure no existing grade record is already submitted for this student/assessment combo
        var alreadyGraded = await dbContext.AssessmentResults.AnyAsync(ar => 
            ar.AssessmentId == result.AssessmentId && ar.StudentId == result.StudentId);

        if (alreadyGraded)
        {
            throw new InvalidOperationException($"Student ID {result.StudentId} has already been graded for this assessment.");
        }

        // ==========================================
        // CRITICAL TRACKING FIX
        // ==========================================
        // Clear navigation objects to prevent EF from attempting to validate or 
        // write empty Assessment/Student graph branches.
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

        if (result is null) return null;

        if (newScore > result.Assessment.MaxScore)
        {
            throw new ArgumentException($"Score obtained ({newScore}) cannot exceed the maximum assessment ceiling of {result.Assessment.MaxScore}.");
        }

        result.ScoreObtained = newScore;
        await dbContext.SaveChangesAsync();
        return result;
    }

    public async Task DeleteResultAsync(int id)
    {
        var result = await dbContext.AssessmentResults.FindAsync(id);
        if (result is not null)
        {
            dbContext.AssessmentResults.Remove(result);
            await dbContext.SaveChangesAsync();
        }
    }
}