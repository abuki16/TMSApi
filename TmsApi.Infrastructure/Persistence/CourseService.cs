using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;
using   Microsoft.Extensions.Logging;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Design;

namespace TmsApi.Infrastructure.Persistence;

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
                c.Enrollments.Count)) // EF translates this directly into a high-performance SQL COUNT(*) subquery
            .FirstOrDefaultAsync(ct);

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

        // Re-query through GetByIdAsync to enforce the exact same flat projection layer across all read/write bounds
        return (await GetByIdAsync(course.Id, ct))!;
    }
    // public async Task<IEnumerable<Course>> GetAllAsync(CancellationToken ct)
    // {
    //     return await context.Courses.ToListAsync(ct);
    // }
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
        .Skip((request.Page - 1) *  request.PageSize)
        .Take(request.PageSize)
        .Select(c => new CourseResponseDto( c.Id,
                                            c.Code,
                                            c.Title,
                                            c.MaxCapacity,
                                            c.Enrollments.Count))
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

        // Map the updated properties across
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