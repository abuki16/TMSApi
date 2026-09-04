using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Application.Common;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class CourseService(TmsDbContext context, ILogger<CourseService> logger) : ICourseService
{
    public Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct) =>
        context.Courses
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CourseResponseDto(
                c.Id, 
                c.Code, 
                c.Title, 
                c.MaxCapacity, 
                c.Enrollments.Count,
                c.Enrollments.Select(e => new EnrollmentItemDto(e.Id, e.StudentId)).ToList(),
                c.InstructorId
            )) 
            .FirstOrDefaultAsync(ct);

    public Task<CourseResponseDto?> GetByCodeAsync(string courseCode, CancellationToken ct) =>
        context.Courses
            .AsNoTracking()
            .Where(c => c.Code == courseCode)
            .Select(c => new CourseResponseDto(
                c.Id, 
                c.Code, 
                c.Title, 
                c.MaxCapacity, 
                c.Enrollments.Count,
                c.Enrollments.Select(e => new EnrollmentItemDto(e.Id, e.StudentId)).ToList(),
                c.InstructorId
            )) 
            .FirstOrDefaultAsync(ct);

    public async Task<List<CourseResponseDto>> GetAllAsync(CancellationToken ct) =>
        await context.Courses
            .AsNoTracking()
            .Select(c => new CourseResponseDto(
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                c.Enrollments.Count,
                c.Enrollments.Select(e => new EnrollmentItemDto(e.Id, e.StudentId)).ToList(),
                c.InstructorId
            ))
            .ToListAsync(ct);

    public async Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct)
    {
        var course = new Course
        {
            Code = request.Code,
            Title = request.Title,
            MaxCapacity = request.MaxCapacity
        };

        context.Courses.Add(course);
        await context.SaveChangesAsync(ct);
        
        logger.LogInformation("Created course {CourseId} ({Code})", course.Id, course.Code);

        return (await GetByIdAsync(course.Id, ct))!;
    }

    public async Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(
        PagedRequest request,
        CancellationToken ct)
    {
        IQueryable<Course> query = context.Courses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(c =>
                EF.Functions.ILike(c.Title, $"%{request.Search}%") ||
                EF.Functions.ILike(c.Code, $"%{request.Search}%"));
        }

        var totalCount = await query.CountAsync(ct);

        IOrderedQueryable<Course> sortedQuery = request.OrderBy switch
        {
            "Code" => request.Descending
                ? query.OrderByDescending(c => c.Code)
                : query.OrderBy(c => c.Code),

            "MaxCapacity" => request.Descending
                ? query.OrderByDescending(c => c.MaxCapacity)
                : query.OrderBy(c => c.MaxCapacity),

            _ => request.Descending
                ? query.OrderByDescending(c => c.Title)
                : query.OrderBy(c => c.Title)
        };

        var items = await sortedQuery
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CourseResponseDto(
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                c.Enrollments.Count,
                c.Enrollments.Select(e => new EnrollmentItemDto(e.Id, e.StudentId)).ToList(),
                c.InstructorId
            ))
            .ToListAsync(ct);

        return new PagedResponse<CourseResponseDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
    
    public async Task UpdateAsync(int id, UpdateCourseRequest request, CancellationToken ct)
    {
        var course = await context.Courses.FindAsync([id], ct);
        if (course is null) return;

        course.Code = request.Code;
        course.Title = request.Title;
        course.MaxCapacity = request.MaxCapacity;

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var course = await context.Courses.FindAsync([id], ct);
        if (course is null) return;

        context.Courses.Remove(course);
        await context.SaveChangesAsync(ct);
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) =>
        context.Courses.AsNoTracking().AnyAsync(c => c.Code == code, ct);
}