using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TmsApi.Data;
using TmsApi.Dtos;
using TmsApi.Entities; 
namespace TmsApi.Services;
public class EnrollmentService(TmsDbContext context, ILogger<EnrollmentService> logger) : IEnrollmentService
{
    public Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct) =>
        context.Enrollments
            .AsNoTracking()
            .Where(e => e.Id == id && e.CourseId == courseId)
            .Select(e => new EnrollmentResponseDto(e.Id, e.CourseId, e.StudentId, e.EnrolledAt))
            .FirstOrDefaultAsync(ct);

    public async Task<EnrollmentResponseDto> CreateAsync(int courseId, EnrollStudentRequest request, CancellationToken ct)
    {
        var enrollment = new Enrollment
        {
            CourseId = courseId,
            StudentId = request.StudentId,
            EnrolledAt = DateTime.UtcNow
        };

        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Enrolled student {StudentId} into course {CourseId}", request.StudentId, courseId);

        return (await GetByIdAsync(courseId, enrollment.Id, ct))!;
    }
}

public class TmsDatabaseException : Exception
{
    public TmsDatabaseException(string message) : base(message) { }
}

// public interface IEnrollmentService
// {
//     Task<EnrollmentRecord> EnrollAsync(string studentId, string courseCode);
//     Task<EnrollmentRecord?> GetByIdAsync(string id);
//     Task<IReadOnlyList<EnrollmentRecord>> GetAllAsync();
//     Task<bool> DeleteAsync(string id);
// }

// public class EnrollmentService : IEnrollmentService
// {
//     private readonly TmsDbContext _dbContext;
//     private readonly ILogger<EnrollmentService> _logger;

//     public EnrollmentService(TmsDbContext dbContext, ILogger<EnrollmentService> logger)
//     {
//         _dbContext = dbContext;
//         _logger = logger;
//     }

//     public async Task<EnrollmentRecord> EnrollAsync(string studentId, string courseCode)
//     {
//         // 1. Convert string parameters to match your database integer keys
//         int parsedStudentId = int.Parse(studentId);
//         int parsedCourseId = int.Parse(courseCode);

//         // 2. Duplicate check running directly against your real database entity types
//         var existing = await _dbContext.Enrollments.FirstOrDefaultAsync(
//             e => e.StudentId == parsedStudentId &&
//                  e.CourseId == parsedCourseId);

//         if (existing is not null)
//         {
//             _logger.LogWarning(
//                 "Duplicate enrollment attempt {StudentId} already in {CourseCode} (record {EnrollmentId})",
//                 studentId,
//                 courseCode,
//                 existing.Id);

//             return new EnrollmentRecord(existing.Id.ToString(), studentId, courseCode, existing.EnrolledAt);
//         }

//         // 3. Create your database Enrollment entity (Database automatically increments the Int Id)
//         var dbEnrollment = new Enrollment
//         {
//             StudentId = parsedStudentId,
//             CourseId = parsedCourseId,
//             EnrolledAt = DateTime.UtcNow
//         };

//         await _dbContext.Enrollments.AddAsync(dbEnrollment);
//         await _dbContext.SaveChangesAsync();

//         _logger.LogInformation(
//             "Enrolled {StudentId} in {CourseCode} record {EnrollmentId}",
//             studentId,
//             courseCode,
//             dbEnrollment.Id);

//         // 4. Return the tracking shape requested by your interface structure
//         return new EnrollmentRecord(
//             dbEnrollment.Id.ToString(),
//             studentId,
//             courseCode,
//             dbEnrollment.EnrolledAt);
//     }

//     public async Task<EnrollmentRecord?> GetByIdAsync(string id)
//     {
//         int parsedId = int.Parse(id);
//         var record = await _dbContext.Enrollments.FirstOrDefaultAsync(e => e.Id == parsedId);

//         if (record is null)
//         {
//             _logger.LogWarning("Enrollment {EnrollmentId} not found", id);
//             return null;
//         }

//         return new EnrollmentRecord(
//             record.Id.ToString(),
//             record.StudentId.ToString(),
//             record.CourseId.ToString(),
//             record.EnrolledAt);
//     }

//     public async Task<IReadOnlyList<EnrollmentRecord>> GetAllAsync()
//     {
//         // Pull database rows and project them directly into the EnrollmentRecord shapes
//         var list = await _dbContext.Enrollments
//             .Select(e => new EnrollmentRecord(
//                 e.Id.ToString(),
//                 e.StudentId.ToString(),
//                 e.CourseId.ToString(),
//                 e.EnrolledAt))
//             .ToListAsync();

//         return list;
//     }

//     public async Task<bool> DeleteAsync(string id)
//     {
//         int parsedId = int.Parse(id);
//         var record = await _dbContext.Enrollments.FirstOrDefaultAsync(e => e.Id == parsedId);
        
//         if (record is null)
//         {
//             _logger.LogWarning("Delete failed enrollment {EnrollmentId} not found", id);
//             return false;
//         }

//         _dbContext.Enrollments.Remove(record);
//         await _dbContext.SaveChangesAsync();

//         _logger.LogInformation("Deleted enrollment {EnrollmentId}", id);
//         return true;
//     }
// }

// public record EnrollmentRecord(
//     string Id,
//     string StudentId,
//     string CourseCode,
//     DateTime EnrolledAt);

