using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using TmsApi.Application.DTOs;
using MediatR;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Enrollments.Queries;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.RateLimiting;
using TmsApi.Application.Courses.Queries;

namespace TmsApi.Api.Controllers.V2;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("2.0")]
[Tags("Enrollments")]
[Produces("application/problem+json")]
public class EnrollmentsController(IMediator mediator) : ControllerBase
{
    // POST /api/v2/enrollments
    [HttpPost(Name = "EnrollStudent")]
    [ProducesResponseType(typeof(EnrollmentCreated), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Enrol a student in a course")]
    public async Task<IActionResult> Enroll(
        [FromBody] EnrollStudentCommand command,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);

        return result.Match<IActionResult>(
            onSuccess: created => CreatedAtAction(
                nameof(GetSchedule),
                new { version = "2.0", studentId = created.StudentId },
                created),
            onFailure: error =>
            {
                var status = error.Code switch
                {
                    "course_not_found" => StatusCodes.Status404NotFound,
                    "course_full" or "already_enrolled" => StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status400BadRequest
                };

                return Problem(
                    statusCode: status,
                    title: "Enrollment rejected",
                    detail: error.Message,
                    type: $"https://tms.local/errors/{error.Code}");
            });
    }

    // GET /api/v2/enrollments/{studentId}/schedule
    [HttpGet("{studentId:int}/schedule", Name = nameof(GetSchedule))]
    [ProducesResponseType(typeof(ScheduleDto), StatusCodes.Status200OK)]
    [EndpointSummary("Get schedule for a student")]
    public async Task<IActionResult> GetSchedule(
        [FromRoute] int studentId,
        CancellationToken ct)
    {
        var schedule = await mediator.Send(new GetStudentScheduleQuery(studentId), ct);
        return Ok(schedule);
    }

    [HttpGet("search")]
    [EnableRateLimiting("search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [EndpointSummary("Search courses")]
    public async Task<IActionResult> SearchCourses(
[FromQuery] string? term, CancellationToken ct)
    {
        var results = await mediator.Send(new SearchCoursesQuery(term), ct);
        return Ok(results);
    }
    // GET /api/v2/enrollments
    [HttpGet(Name = "GetAllEnrollments")]
    [ProducesResponseType(typeof(IEnumerable<EnrollmentResponseDto>), StatusCodes.Status200OK)]
    [EndpointSummary("Get all enrollments")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var enrollments = await mediator.Send(new GetEnrollmentsQuery(), ct);
        return Ok(enrollments);
    }
    // POST /api/v2/enrollments/{id}/approve
[HttpPost("{id:int}/approve", Name = "ApproveEnrollment")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[EndpointSummary("Approve a student enrollment")]
public async Task<IActionResult> Approve(int id, CancellationToken ct)
{
    // Call your mediator command or service to update the enrollment status in the database
    var result = await mediator.Send(new ApproveEnrollmentCommand(id), ct);
    
    return result.IsSuccess ? Ok() : BadRequest(result.Error);
}
}


// [ApiController]
// [Route("api/v{version:apiVersion}/enrollments")]
// [ApiVersion("2.0")]
// [Tags("Enrollments")]
// [Produces("application/problem+json")]
// public class EnrollmentsController(
//     ICourseService courseService,
//     IEnrollmentService enrollmentService,
//     IMediator mediator) : ControllerBase
// {
//     // Action 1: GET /api/v2/courses/{courseId}/enrollments
//     [HttpGet(Name = "ListCourseEnrollments")]
//     [ProducesResponseType(typeof(IReadOnlyList<EnrollmentResponseDto>), StatusCodes.Status200OK)]
//     [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
//     [EndpointSummary("List enrolments for a course")]
//     public async Task<IActionResult> GetEnrollments(int courseId, CancellationToken ct)
//     {
//         var course = await courseService.GetByIdAsync(courseId, ct);
//         if (course is null) return NotFound();

//         var enrollments = await enrollmentService.GetByCourseAsync(courseId, ct);
//         return Ok(enrollments);
//     }

//     // Action 2: GET /api/v2/courses/{courseId}/enrollments/{id}
//     [HttpGet("{id:int}", Name = nameof(GetEnrollment))]
//     [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status200OK)]
//     [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
//     [EndpointSummary("Get one enrolment for a course")]
//     public async Task<IActionResult> GetEnrollment(int courseId, int id, CancellationToken ct)
//     {
//         var enrollment = await enrollmentService.GetByIdAsync(courseId, id, ct);
//         return enrollment is not null ? Ok(enrollment) : NotFound();
//     }

//     // Action 3: POST /api/v2/courses/{courseId}/enrollments
//     [HttpPost]
//     [ProducesResponseType(typeof(EnrollmentCreated), StatusCodes.Status201Created)]
//     [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
//     [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
//     [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
//     [EndpointSummary("Enrol a student in a course")]
//    public async Task<IActionResult> Enroll(
//         [FromBody] EnrollStudentCommand command, 
//         CancellationToken ct)
//     {
//         var result = await mediator.Send(command, ct);

//         return result.Match<IActionResult>(
//             onSuccess: created => CreatedAtAction(
//                 nameof(GetSchedule),
//                 new { studentId = created.StudentId },
//                 created),
//             onFailure: error =>
//             {
//                 var status = error.Code switch
//                 {
//                     "course_not_found" => StatusCodes.Status404NotFound,
//                     "course_full" or "already_enrolled" => StatusCodes.Status409Conflict,
//                     _ => StatusCodes.Status400BadRequest
//                 };

//                 return Problem(
//                     statusCode: status,
//                     title: "Enrollment rejected",
//                     detail: error.Message,
//                     type: $"https://tms.local/errors/{error.Code}");
//             });
//     }

//     // Action 4: GET /api/v2/courses/{courseId}/enrollments/{studentId}/schedule
//     [HttpGet("{studentId}/schedule")]
//     [ProducesResponseType(typeof(ScheduleDto), StatusCodes.Status200OK)]
//     [EndpointSummary("Get schedule for a student")]
//     public async Task<IActionResult> GetSchedule(int studentId, CancellationToken ct)
//     {
//         var schedule = await mediator.Send(new GetStudentScheduleQuery(studentId), ct);
//         return Ok(schedule);
//     }
// }
// public async Task<IActionResult> EnrollStudent(int courseId, EnrollStudentRequest request, CancellationToken ct)
// {
//     if (!ModelState.IsValid) return BadRequest(ModelState);

//     // 1. Look up the parent course first. If null, return 404 NotFound.
//     var course = await courseService.GetByIdAsync(courseId, ct);
//     if (course is null)
//     {
//         return Problem(
//             statusCode: StatusCodes.Status404NotFound,
//             title: "Course Not Found",
//             detail: $"Course with ID {courseId} does not exist."
//         );
//     }

//     // 2. Look up the student using ToString() to match the string signature. 
//     var student = await studentService.GetByIdAsync(request.StudentId.ToString());
//     if (student is null)
//     {
//         return Problem(
//             statusCode: StatusCodes.Status404NotFound,
//             title: "Student Not Found",
//             detail: $"Student with ID {request.StudentId} does not exist in the system."
//         );
//     }

//     // 3. Prevent duplicate enrollment in the same course
//     // Checks the existing enrollments for this course to see if the student is already registered
//     var existingEnrollments = await enrollmentService.GetByCourseAsync(courseId, ct);

//     // This checks if any existing enrollment item shares the incoming StudentId
//     bool isAlreadyEnrolled = false;
//     foreach (var item in existingEnrollments)
//     {
//         if (item.StudentId == request.StudentId)
//         {
//             isAlreadyEnrolled = true;
//             break;
//         }
//     }

//     if (isAlreadyEnrolled)
//     {
//         return Problem(
//             statusCode: StatusCodes.Status409Conflict,
//             title: "Duplicate Enrollment",
//             detail: $"Student with ID {request.StudentId} is already enrolled in this course."
//         );
//     }

//     // 4. Check capacity limits next. If full, return 409 Conflict.
//     if (course.EnrollmentCount >= course.MaxCapacity)
//     {
//         return Problem(
//             statusCode: StatusCodes.Status409Conflict,
//             title: "Course is full",
//             detail: $"Course '{course.Title}' has reached its maximum capacity of {course.MaxCapacity}."
//         );
//     }

//     // 5. Otherwise, safely proceed with creation
//     try
//     {
//         var enrollment = await enrollmentService.CreateAsync(courseId, request, ct);

//         return CreatedAtAction(
//             nameof(GetEnrollment),
//             new { courseId, id = enrollment.Id },
//             enrollment);
//     }
//     catch (InvalidOperationException ex)
//     {
//         return Problem(
//             statusCode: StatusCodes.Status409Conflict,
//             title: "Enrollment Failure",
//             detail: ex.Message
//         );
//     }
// }

