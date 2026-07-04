using Microsoft.AspNetCore.Mvc;
using TmsApi.Entities;
using TmsApi.Services;
using TmsApi.Dtos;
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

// [HttpGet]
// public async Task<IActionResult> GetAllCourses(CancellationToken ct)
// {
//     var courses = await courseService.GetAllAsync(ct);
//     return Ok(courses); // Returns a 200 OK status with the array of courses
// }

 [HttpGet]
public async Task<IActionResult> GetCourses([FromQuery] PagedRequest request,CancellationToken ct)
 {
      var result = await courseService.GetCoursesAsync(request, ct);
       return Ok(result);
 }

[HttpPut("{id:int}")]
public async Task<IActionResult> UpdateCourse(int id, UpdateCourseRequest request, CancellationToken ct)
{
    var existingCourse = await courseService.GetByIdAsync(id, ct);
    if (existingCourse is null)
    {
        return NotFound();
    }

    if (existingCourse.Code != request.Code && await courseService.CodeExistsAsync(request.Code, ct))
    {
        return Conflict(new ProblemDetails
        {
            Title = "Course code conflict",
            Detail = $"Cannot update course. The code '{request.Code}' is already taken.",
            Status = StatusCodes.Status409Conflict,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10"
        });
    }

    await courseService.UpdateAsync(id, request, ct);
    return Ok(new { message = "Course updated successfully" });
}

[HttpDelete("{id:int}")]
public async Task<IActionResult> DeleteCourse(int id, CancellationToken ct)
{
    var existingCourse = await courseService.GetByIdAsync(id, ct);
    if (existingCourse is null)
    {
        return NotFound();
    }

    await courseService.DeleteAsync(id, ct);
    return Ok(new { message = "Course deleted successfully" });
}
}