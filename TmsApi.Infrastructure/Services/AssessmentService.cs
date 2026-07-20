using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;


namespace TmsApi.Infrastructure.Services;

public class AssessmentService(TmsDbContext dbContext) : IAssessmentService
{
    public async Task<IReadOnlyList<AssessmentResponseDto>> GetByCourseAsync(int courseId)
    {
        return await dbContext.Assessments
            .AsNoTracking()
            .Where(a => a.CourseId == courseId)
            .Select(a => new AssessmentResponseDto(
                a.Id,
                a.Title,
                a.MaxScore,
                a.Weight,
                a.CourseId
            ))
            .ToListAsync();
    }

    public async Task<Assessment> CreateAssessmentAsync(Assessment assessment)
    {
        // Validate that the course exists before creating the assessment
        var courseExists = await dbContext.Courses
            .AnyAsync(c => c.Id == assessment.CourseId);

        if (!courseExists)
        {
            throw new ArgumentException(
                $"Validation Failed: Course with ID {assessment.CourseId} does not exist.");
        }

        await dbContext.Assessments.AddAsync(assessment);
        await dbContext.SaveChangesAsync();

        return assessment;
    }

    public async Task<Assessment?> GetByIdAsync(int id)
    {
        return await dbContext.Assessments
            .Include(a => a.Course)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Assessment?> UpdateScoreAsync(int id, decimal newMaxScore)
    {
        var assessment = await dbContext.Assessments
            .FirstOrDefaultAsync(a => a.Id == id);

        if (assessment == null)
            return null;

        if (newMaxScore <= 0)
        {
            throw new ArgumentException(
                "Validation Failed: Maximum score must be greater than zero.");
        }

        assessment.MaxScore = newMaxScore;

        await dbContext.SaveChangesAsync();

        return assessment;
    }

    public async Task<bool> DeleteAssessmentAsync(int id)
    {
        var assessment = await dbContext.Assessments.FindAsync(id);

        if (assessment == null)
            return false;

        dbContext.Assessments.Remove(assessment);

        await dbContext.SaveChangesAsync();

        return true;
    }
}