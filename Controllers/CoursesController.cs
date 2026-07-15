using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Entities;
using TmsApi.Services;
using TmsApi.Dtos;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/courses")]
[Tags("Courses")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class CoursesController(ICourseService courseService, LinkGenerator linkGenerator) : ControllerBase
{
    // Action 1: GET /api/courses (Paginated List)
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CourseResponseDto>), StatusCodes.Status200OK)]
    [EndpointSummary("List courses with pagination")]
    [EndpointDescription("Returns a paginated, optionally filtered list of TMS courses.")]
    public async Task<IActionResult> GetCourses([FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await courseService.GetCoursesAsync(request, ct);
        return Ok(result);
    }

    // Action 2: GET /api/courses/{id}
    [HttpGet("{id:int}", Name = nameof(GetCourseById))]
    [ProducesResponseType(typeof(CourseDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get course by ID")]
    [EndpointDescription("Retrieves a detailed course record complete with conditional hypermedia links.")]
    public async Task<IActionResult> GetCourseById(int id, CancellationToken ct)
    {
        var course = await courseService.GetByIdAsync(id, ct);
        if (course is null) return NotFound();

        // Safe dynamic link creation
        var selfPath = linkGenerator.GetPathByName(HttpContext, nameof(GetCourseById), new { id });
        var enrollmentsPath = linkGenerator.GetPathByName(HttpContext, "ListCourseEnrollments", new { courseId = id });

        var links = new List<LinkDto>
        {
            new(selfPath ?? "", "self", "GET"),
            new(selfPath ?? "", "update", "PUT"),
            new(selfPath ?? "", "delete", "DELETE"),
            new(enrollmentsPath ?? "", "enrollments", "GET")
        };

        // Apply conditional HATEOAS constraint checking course capacity
        if (course.EnrollmentCount < course.MaxCapacity)
        {
            links.Add(new LinkDto(enrollmentsPath ?? "", "enroll", "POST"));
        }

        var detailDto = new CourseDetailDto
        {
            Id = course.Id,
            Code = course.Code,
            Title = course.Title,
            MaxCapacity = course.MaxCapacity,
            EnrollmentCount = course.EnrollmentCount,
            Links = links
        };

        return Ok(detailDto);
    }

    // Action 3: POST /api/courses
    [HttpPost]
    [ProducesResponseType(typeof(CourseResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Create a new course")]
    [EndpointDescription("Creates a new course entry. Fails if the course code already exists.")]
    public async Task<IActionResult> CreateCourse([FromBody] CreateCourseRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (await courseService.CodeExistsAsync(request.Code, ct))
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Course Code Already Exists",
                detail: $"A course with code '{request.Code}' is already registered."
            );
        }

        var result = await courseService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetCourseById), new { id = result.Id }, result);
    }

    // Action 4: PUT /api/courses/{id}
    [HttpPut("{id:int}", Name = nameof(UpdateCourse))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Update an existing course")]
    [EndpointDescription("Updates structural properties of a course. Fails if the new code conflicts with an existing one.")]
    public async Task<IActionResult> UpdateCourse(int id, [FromBody] UpdateCourseRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var existingCourse = await courseService.GetByIdAsync(id, ct);
        if (existingCourse is null) return NotFound();

        if (existingCourse.Code != request.Code && await courseService.CodeExistsAsync(request.Code, ct))
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Course Code Conflict",
                detail: $"Cannot update course. The code '{request.Code}' is already taken."
            );
        }

        await courseService.UpdateAsync(id, request, ct);
        return Ok(new { message = "Course updated successfully." });
    }

    // Action 5: DELETE /api/courses/{id}
    [HttpDelete("{id:int}", Name = nameof(DeleteCourse))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Delete a course")]
    [EndpointDescription("Permanently removes a course record from the persistent store by ID.")]
    public async Task<IActionResult> DeleteCourse(int id, CancellationToken ct)
    {
        var existingCourse = await courseService.GetByIdAsync(id, ct);
        if (existingCourse is null) return NotFound();

        await courseService.DeleteAsync(id, ct);
        return Ok(new { message = "Course deleted successfully." });
    }
}