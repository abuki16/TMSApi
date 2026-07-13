using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Dtos;
using TmsApi.Entities;

namespace TmsApi.Services;

public interface IAssessmentService
{
    Task<Assessment> CreateAssessmentAsync(Assessment assessment);
    Task<Assessment?> GetByIdAsync(int id);
    Task<Assessment?> UpdateScoreAsync(int id, decimal newScore);
    Task<IReadOnlyList<AssessmentResponseDto>> GetByCourseAsync(int courseId);
    Task<bool> DeleteAssessmentAsync(int id);
}

public class AssessmentService(TmsDbContext dbContext) : IAssessmentService
{
    // 🌟 MAKE SURE THIS IS INSIDE THE BRACKETS HERE:
    public async Task<IReadOnlyList<AssessmentResponseDto>> GetByCourseAsync(int courseId)
    {
        return await dbContext.Assessments
            .Where(a => a.CourseId == courseId)
            .Select(a => new AssessmentResponseDto(
                a.Id,
                a.Title,
                a.MaxScore,
                a.ScoreObtained,
                a.Weight,
                a.CourseId,
                a.StudentId
            ))
            .ToListAsync();
    }

    public async Task<Assessment> CreateAssessmentAsync(Assessment assessment)
    {
        // ... rest of your CreateAssessmentAsync code ...
        var studentExists = await dbContext.Students.AnyAsync(s => s.Id == assessment.StudentId);
        // (Keep all your existing validation logic here)
        
        await dbContext.Assessments.AddAsync(assessment);
        await dbContext.SaveChangesAsync();
        return assessment;
    }

    public async Task<Assessment?> GetByIdAsync(int id)
    {
        return await dbContext.Assessments
            .Include(a => a.Course)
            .Include(a => a.Student)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Assessment?> UpdateScoreAsync(int id, decimal newScore)
    {
        var assessment = await dbContext.Assessments.FirstOrDefaultAsync(a => a.Id == id);
        if (assessment == null) return null;

        if (newScore > assessment.MaxScore)
        {
            throw new ArgumentException($"Validation Failed: Score obtained ({newScore}) cannot exceed max score ({assessment.MaxScore}).");
        }

        assessment.ScoreObtained = newScore;
        await dbContext.SaveChangesAsync();
        return assessment;
    }

    public async Task<bool> DeleteAssessmentAsync(int id)
    {
        var assessment = await dbContext.Assessments.FindAsync(id);
        if (assessment is null) return false;

        dbContext.Assessments.Remove(assessment);
        await dbContext.SaveChangesAsync();
        return true;
    }
}