using Microsoft.AspNetCore.Mvc;
using TmsApi.Entities;
using TmsApi.Services;
using Tms.Api.Dtos;
namespace TmsApi.Controllers;

[ApiController]
[Route("api/courses")]
public class CoursesController(ICourseService courseService) : ControllerBase
{
    [HttpGet("{id:int}", Name = nameof(GetCourseById))]
    public async Task<IActionResult> GetCourseById(int id, CancellationToken ct)
    {
        var course = await courseService.GetByIdAsync(id, ct);
        
        if (course is null)
        {
            return NotFound();
        }
        
        return Ok(course);
    }

     [HttpPost]
// public async Task<IActionResult> CreateCourse(CreateCourseRequest request, CancellationToken ct) // Change from 'Course course' to 'CreateCourseRequest request'
// {
//     var result = await courseService.CreateAsync(request, ct); // Pass request here
//     return CreatedAtAction(nameof(GetCourseById), new { id = result.Id }, result);
// }
public async Task<IActionResult> CreateCourse(CreateCourseRequest request, CancellationToken ct)
{
    // Pre-check business rule: Ensure course code uniqueness before inserting
    if (await courseService.CodeExistsAsync(request.Code, ct))
    {
        return Conflict(new ProblemDetails
        {
            Title = "Course code already exists",
            Detail = $"A course with code '{request.Code}' is already registered.",
            Status = StatusCodes.Status409Conflict,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10"
        });
    }

    var result = await courseService.CreateAsync(request, ct);
    return CreatedAtAction(nameof(GetCourseById), new { id = result.Id }, result);
}
}