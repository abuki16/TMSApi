using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Dtos;
using TmsApi.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/enrollments")]
[Tags("Enrollments")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class EnrollmentsController(
    ICourseService courseService,
    IEnrollmentService enrollmentService,
    IStudentService studentService) : ControllerBase
{
    // Action 1: GET /api/courses/{courseId}/enrollments (Returns the whole list)
    [HttpGet(Name = "ListCourseEnrollments")]
    [ProducesResponseType(typeof(IReadOnlyList<EnrollmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("List enrolments for a course")]
    public async Task<IActionResult> GetEnrollments(int courseId, CancellationToken ct)
    {
        var course = await courseService.GetByIdAsync(courseId, ct);
        if (course is null) return NotFound();

        var enrollments = await enrollmentService.GetByCourseAsync(courseId, ct);
        return Ok(enrollments);
    }

    // Action 2: GET /api/courses/{courseId}/enrollments/{id} (Returns single enrollment item)
    [HttpGet("{id:int}", Name = nameof(GetEnrollment))]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get one enrolment for a course")]
    public async Task<IActionResult> GetEnrollment(int courseId, int id, CancellationToken ct)
    {
        var enrollment = await enrollmentService.GetByIdAsync(courseId, id, ct);
        return enrollment is not null ? Ok(enrollment) : NotFound();
    }

    // Action 3: POST /api/courses/{courseId}/enrollments (Creates a student enrollment)
    [HttpPost]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Enrol a student in a course")]
    [EndpointDescription("Returns 404 if the course/student does not exist, 409 if full or already enrolled.")]
    public async Task<IActionResult> EnrollStudent(int courseId, EnrollStudentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // 1. Look up the parent course first. If null, return 404 NotFound.
        var course = await courseService.GetByIdAsync(courseId, ct);
        if (course is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Course Not Found",
                detail: $"Course with ID {courseId} does not exist."
            );
        }

        // 2. Look up the student using ToString() to match the string signature. 
        var student = await studentService.GetByIdAsync(request.StudentId.ToString());
        if (student is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Student Not Found",
                detail: $"Student with ID {request.StudentId} does not exist in the system."
            );
        }

        // 3. Prevent duplicate enrollment in the same course
        // Checks the existing enrollments for this course to see if the student is already registered
        var existingEnrollments = await enrollmentService.GetByCourseAsync(courseId, ct);
        
        // This checks if any existing enrollment item shares the incoming StudentId
        bool isAlreadyEnrolled = false;
        foreach (var item in existingEnrollments)
        {
            if (item.StudentId == request.StudentId)
            {
                isAlreadyEnrolled = true;
                break;
            }
        }

        if (isAlreadyEnrolled)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Duplicate Enrollment",
                detail: $"Student with ID {request.StudentId} is already enrolled in this course."
            );
        }

        // 4. Check capacity limits next. If full, return 409 Conflict.
        if (course.EnrollmentCount >= course.MaxCapacity)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Course is full",
                detail: $"Course '{course.Title}' has reached its maximum capacity of {course.MaxCapacity}."
            );
        }

        // 5. Otherwise, safely proceed with creation
        try
        {
            var enrollment = await enrollmentService.CreateAsync(courseId, request, ct);
            
            return CreatedAtAction(
                nameof(GetEnrollment),
                new { courseId, id = enrollment.Id },
                enrollment);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Enrollment Failure",
                detail: ex.Message
            );
        }
    }
}