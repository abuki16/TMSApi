using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Entities;

namespace TmsApi.Services;

public interface IAssessmentService
{
    Task<Assessment> CreateAssessmentAsync(Assessment assessment);
    Task<Assessment?> GetByIdAsync(int id);
    Task<Assessment?> UpdateScoreAsync(int id, decimal newScore);
}

public class AssessmentService(TmsDbContext dbContext) : IAssessmentService
{
    public async Task<Assessment> CreateAssessmentAsync(Assessment assessment)
    {
        // 1. Validation: Does the student exist?
        var studentExists = await dbContext.Students.AnyAsync(s => s.Id == assessment.StudentId);
        if (!studentExists)
        {
            throw new ArgumentException($"Validation Failed: Student with ID {assessment.StudentId} does not exist.");
        }

        // 2. Validation: Does the course exist?
        var courseExists = await dbContext.Courses.AnyAsync(c => c.Id == assessment.CourseId);
        if (!courseExists)
        {
            throw new ArgumentException($"Validation Failed: Course with ID {assessment.CourseId} does not exist.");
        }

        // 3. Academic Check: Is the student actually enrolled in this course?
        var isEnrolled = await dbContext.Enrollments.AnyAsync(e => e.StudentId == assessment.StudentId && e.CourseId == assessment.CourseId);
        if (!isEnrolled)
        {
            throw new InvalidOperationException($"Academic Constraint: Student {assessment.StudentId} cannot receive an assessment for Course {assessment.CourseId} because they are not enrolled.");
        }

        // 4. Data Rule Check: Score obtained cannot exceed max score
        if (assessment.ScoreObtained > assessment.MaxScore)
        {
            throw new ArgumentException($"Validation Failed: Score obtained ({assessment.ScoreObtained}) cannot be higher than the maximum possible score ({assessment.MaxScore}).");
        }

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
}