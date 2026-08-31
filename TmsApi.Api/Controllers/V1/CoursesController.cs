using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers.V1;

[ApiController]
[Route("api/v{version:apiVersion}/courses")]
[ApiVersion("1.0")]
public class CoursesController : ControllerBase
{
    private readonly TmsDbContext _context;

    public CoursesController(TmsDbContext context)
    {
        _context = context;
    }

    [HttpGet]

    public async Task<IActionResult> GetCourses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var baseQuery = _context.Courses.AsNoTracking();
        var totalCount = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderBy(c => c.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                EnrollmentCount = c.Enrollments.Count
            })
            .ToListAsync(ct);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return Ok(new
        {
            items,
            totalCount,
            page,
            pageSize,
            totalPages,
            hasNext = page < totalPages,
            hasPrevious = page > 1
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCourseById(int id, CancellationToken ct)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                EnrollmentCount = c.Enrollments.Count
            })
            .FirstOrDefaultAsync(ct);

        if (course is null) return NotFound();

        return Ok(course);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCourse([FromBody] Course model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var exists = await _context.Courses.AnyAsync(c => c.Code == model.Code, ct);
        if (exists)
        {
            return Conflict(new { message = $"A course with code '{model.Code}' already exists." });
        }

        _context.Courses.Add(model);
        await _context.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetCourseById), new { version = "1.0", id = model.Id }, model);
    }

    [HttpPut("{id:int}")]
public async Task<IActionResult> UpdateCourse(int id, [FromBody] Course model, CancellationToken ct)
{
    var existing = await _context.Courses.FindAsync(new object[] { id }, ct);
    if (existing is null) return NotFound();

    // Check if another course already uses this code
    var codeExists = await _context.Courses
        .AnyAsync(c => c.Code == model.Code && c.Id != id, ct);
        
    if (codeExists)
    {
        return Conflict(new { message = $"A course with code '{model.Code}' already exists." });
    }

    existing.Code = model.Code;
    existing.Title = model.Title;
    existing.MaxCapacity = model.MaxCapacity;

    await _context.SaveChangesAsync(ct);
    return Ok(new { message = "Course updated successfully." });
}
    [HttpDelete("{id:int}")]
public async Task<IActionResult> DeleteCourse(int id, CancellationToken ct)
{
    var existing = await _context.Courses
        .Include(c => c.Enrollments)
        .FirstOrDefaultAsync(c => c.Id == id, ct);

    if (existing is null) return NotFound();

    // Check if there are active enrollments linked to this course
    if (existing.Enrollments.Any())
    {
        return Conflict(new 
        { 
            message = "Cannot delete this course because there are active student enrollments associated with it." 
        });
    }

    _context.Courses.Remove(existing);
    await _context.SaveChangesAsync(ct);
    
    return Ok(new { message = "Course deleted successfully." });
}
}