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

        // Ensure the score is within valid bounds [0, MaxScore]
        if (result.ScoreObtained < 0 || result.ScoreObtained > parentAssessment.MaxScore)
        {
            throw new ArgumentException(
                $"Invalid score: {result.ScoreObtained}. For {parentAssessment.Title}, score must be between 0 and {parentAssessment.MaxScore}.");
        }

        // Verify the student exists
        var studentExists = await dbContext.Students
            .AnyAsync(s => s.Id == result.StudentId);

        if (!studentExists)
        {
            throw new ArgumentException(
                $"Student with ID {result.StudentId} does not exist.");
        }

        // Verify the student is enrolled in the course; if not, automatically enroll as Approved
        var enrollment = await dbContext.Enrollments.FirstOrDefaultAsync(e =>
            e.StudentId == result.StudentId &&
            e.CourseId == parentAssessment.CourseId);

        if (enrollment == null)
        {
            enrollment = new Enrollment
            {
                StudentId = result.StudentId,
                CourseId = parentAssessment.CourseId,
                Status = "Approved",
                EnrolledAt = DateTime.UtcNow
            };
            await dbContext.Enrollments.AddAsync(enrollment);
            await dbContext.SaveChangesAsync();
        }

        var gradePoint = parentAssessment.MaxScore > 0
            ? Math.Round((result.ScoreObtained / parentAssessment.MaxScore) * 4.0m, 2)
            : 3.5m;

        // If student already has a result for this assessment, update it
        var existingResult = await dbContext.AssessmentResults.FirstOrDefaultAsync(ar =>
            ar.AssessmentId == result.AssessmentId &&
            ar.StudentId == result.StudentId);

        if (existingResult != null)
        {
            existingResult.ScoreObtained = result.ScoreObtained;
            existingResult.Title = result.Title;
            existingResult.Weight = result.Weight;

            await dbContext.SaveChangesAsync();
            await UpdateEnrollmentGradeFromAssessmentsAsync(result.StudentId, parentAssessment.CourseId);
            await dbContext.SaveChangesAsync();
            return existingResult;
        }

        // Prevent EF Core from inserting related entities again
        result.Assessment = null!;
        result.Student = null!;

        await dbContext.AssessmentResults.AddAsync(result);
        await dbContext.SaveChangesAsync();

        await UpdateEnrollmentGradeFromAssessmentsAsync(result.StudentId, parentAssessment.CourseId);
        await dbContext.SaveChangesAsync();

        return result;
    }

    private async Task UpdateEnrollmentGradeFromAssessmentsAsync(int studentId, int courseId)
    {
        var enrollment = await dbContext.Enrollments.FirstOrDefaultAsync(e =>
            e.StudentId == studentId && e.CourseId == courseId);

        if (enrollment == null) return;

        var courseAssessmentIds = await dbContext.Assessments
            .Where(a => a.CourseId == courseId)
            .Select(a => a.Id)
            .ToListAsync();

        var studentResults = await dbContext.AssessmentResults
            .Where(ar => ar.StudentId == studentId && courseAssessmentIds.Contains(ar.AssessmentId))
            .ToListAsync();

        if (studentResults.Count > 0)
        {
            var totalObtainedScore = studentResults.Sum(r => r.ScoreObtained);
            // Grade point calculated from institutional grading scale (>=85: 4.00, 80: 3.75, 75: 3.50, 70: 3.00, 60: 2.75, 55: 2.50, 50: 2.00, <50: 0.00)
            enrollment.Grade = TmsApi.Application.Grading.GradingService.ToInstitutionalGradePoint(totalObtainedScore);
        }
    }

    public async Task<AssessmentResult?> UpdateScoreAsync(int id, decimal newScore)
    {
        var result = await dbContext.AssessmentResults
            .Include(ar => ar.Assessment)
            .FirstOrDefaultAsync(ar => ar.Id == id);

        if (result is null)
            return null;

        if (newScore < 0 || newScore > result.Assessment.MaxScore)
        {
            throw new ArgumentException(
                $"Invalid score: {newScore}. For {result.Assessment.Title}, score must be between 0 and {result.Assessment.MaxScore}.");
        }

        result.ScoreObtained = newScore;
        await dbContext.SaveChangesAsync();

        await UpdateEnrollmentGradeFromAssessmentsAsync(result.StudentId, result.Assessment.CourseId);
        await dbContext.SaveChangesAsync();

        return result;
    }

    public async Task DeleteResultAsync(int id)
    {
        var result = await dbContext.AssessmentResults
            .Include(ar => ar.Assessment)
            .FirstOrDefaultAsync(ar => ar.Id == id);

        if (result is null)
            return;

        var studentId = result.StudentId;
        var courseId = result.Assessment?.CourseId ?? 0;

        dbContext.AssessmentResults.Remove(result);
        await dbContext.SaveChangesAsync();

        if (courseId > 0)
        {
            await UpdateEnrollmentGradeFromAssessmentsAsync(studentId, courseId);
            await dbContext.SaveChangesAsync();
        }
    }
}