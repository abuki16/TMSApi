using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class EnrollmentService(TmsDbContext context, ILogger<EnrollmentService> logger) : IEnrollmentService
{
    public Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct) =>
        context.Enrollments
            .AsNoTracking()
            .Where(e => e.Id == id && e.CourseId == courseId)
            .Select(e => new EnrollmentResponseDto(
                e.Id, 
                e.StudentId, 
                e.Student.Name,
                e.CourseId, 
                e.Course.Title,
                e.Status,
                e.EnrolledAt,
                new CourseScheduleInfoDto(e.Course.Code, e.Course.Title, e.Course.Title)))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<EnrollmentResponseDto>> GetByCourseAsync(int courseId, CancellationToken ct)
    {
        return await context.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == courseId)
            .Select(e => new EnrollmentResponseDto(
                e.Id, 
                e.StudentId, 
                e.Student.Name,
                e.CourseId, 
                e.Course.Title,
                e.Status,
                e.EnrolledAt,
                new CourseScheduleInfoDto(e.Course.Code, e.Course.Title, e.Course.Title)))
            .ToListAsync(ct);
    }

    public async Task<List<EnrollmentResponseDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.Enrollments
            .AsNoTracking()
            .Select(e => new EnrollmentResponseDto(
                e.Id, 
                e.StudentId, 
                e.Student.Name,
                e.CourseId, 
                e.Course.Title,
                e.Status,
                e.EnrolledAt,
                new CourseScheduleInfoDto(e.Course.Code, e.Course.Title, e.Course.Title)))
            .ToListAsync(ct);
    }

    public async Task<EnrollmentResponseDto> CreateAsync(int courseId, EnrollStudentRequest request, CancellationToken ct)
    {
        var enrollment = new Enrollment
        {
            CourseId = courseId,
            StudentId = request.StudentId,
            EnrolledAt = DateTime.UtcNow,
            Status = "Pending"
        };

        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Enrolled student {StudentId} into course {CourseId}", request.StudentId, courseId);

        var createdEnrollment = await GetByIdAsync(courseId, enrollment.Id, ct);

        if (createdEnrollment == null)
        {
            throw new InvalidOperationException($"Failed to retrieve enrollment with ID {enrollment.Id} after creation.");
        }

        return createdEnrollment;
    }

    public async Task<IEnumerable<EnrollmentResponseDto>> GetByStudentIdAsync(int studentId, CancellationToken ct)
    {
        return await context.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId)
            .Select(e => new EnrollmentResponseDto(
                e.Id, 
                e.StudentId, 
                e.Student.Name,
                e.CourseId, 
                e.Course.Title,
                e.Status,
                e.EnrolledAt,
                new CourseScheduleInfoDto(e.Course.Code, e.Course.Title, e.Course.Title)))
            .ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(int studentId, string courseCode, CancellationToken ct)
    {
        return await context.Enrollments
            .AsNoTracking()
            .AnyAsync(e => e.StudentId == studentId && e.Course.Code == courseCode, ct);
    }

    public async Task AddAsync(Enrollment enrollment, CancellationToken ct)
    {
        context.Enrollments.Add(enrollment);
        
        var course = await context.Courses.FirstOrDefaultAsync(c => c.Id == enrollment.CourseId, ct);
        if (course is null)
        {
            logger.LogWarning("Enrollment failed. Course ID {CourseId} does not exist.", enrollment.CourseId);
            throw new TmsDatabaseException($"Course with ID '{enrollment.CourseId}' was not found.");
        }
        
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Successfully added Student {StudentId} to CourseId {CourseId}", enrollment.StudentId, enrollment.CourseId);
    }
    public async Task<Enrollment?> GetEntityByIdAsync(int id, CancellationToken ct)
    {
        return await context.Enrollments.FindAsync(new object[] { id }, ct);
    }

    public async Task UpdateAsync(Enrollment enrollment, CancellationToken ct)
    {
        context.Enrollments.Update(enrollment);
        await context.SaveChangesAsync(ct);
        
        logger.LogInformation("Successfully updated enrollment ID {EnrollmentId} status to {Status}", enrollment.Id, enrollment.Status);
    }
    
}

public class TmsDatabaseException : Exception
{
    public TmsDatabaseException(string message) : base(message) { }
}