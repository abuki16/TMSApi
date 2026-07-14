using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TmsApi.Dtos;
using TmsApi.Entities;
using TmsApi.Services;
using System;
using System.Collections.Generic;
using System.Linq; 
using System.Threading.Tasks;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/assessments")]
[Tags("Assessments")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class AssessmentsController(
    IAssessmentService assessmentService,
    ICourseService courseService,
    LinkGenerator linkGenerator) : ControllerBase
{
    // Action 1: GET /api/courses/{courseId}/assessments
    [HttpGet(Name = "ListCourseAssessments")]
    [ProducesResponseType(typeof(IReadOnlyList<AssessmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("List assessments for a course")]
    public async Task<IActionResult> GetAssessments(int courseId)
    {
        var course = await courseService.GetByIdAsync(courseId, default);
        if (course is null) return NotFound();

        var assessments = await assessmentService.GetByCourseAsync(courseId); 
        return Ok(assessments);
    }

    // Action 2: GET /api/courses/{courseId}/assessments/{id}
    [HttpGet("{id:int}", Name = nameof(GetAssessment))]
    [ProducesResponseType(typeof(AssessmentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get one assessment with HATEOAS links")]
    public async Task<IActionResult> GetAssessment(int courseId, int id)
    {
        var assessment = await assessmentService.GetByIdAsync(id);
        if (assessment is null || assessment.CourseId != courseId) return NotFound();

        var selfPath = linkGenerator.GetPathByName(HttpContext, nameof(GetAssessment), new { courseId, id });
        var updatePath = linkGenerator.GetPathByName(HttpContext, "UpdateAssessmentScore", new { courseId, id });
        var deletePath = linkGenerator.GetPathByName(HttpContext, "DeleteAssessment", new { courseId, id });
        var coursePath = linkGenerator.GetPathByName(HttpContext, "GetCourseById", new { id = courseId });

        var links = new List<LinkDto>
        {
            new(selfPath ?? "", "self", "GET"),
            new(updatePath ?? "", "update_score", "PATCH"),
            new(deletePath ?? "", "delete", "DELETE"),
            new(coursePath ?? "", "course_details", "GET")
        };

        var detailDto = new AssessmentDetailDto
        {
            Id = assessment.Id,
            Title = assessment.Title,
            MaxScore = (decimal)assessment.MaxScore,
            //ScoreObtained = (decimal)assessment.ScoreObtained,
            Weight = (decimal)assessment.Weight,
            CourseId = assessment.CourseId,
           // StudentId = assessment.StudentId,
            Links = links
        };

        return Ok(detailDto);
    }

    // Action 3: POST /api/courses/{courseId}/assessments
    [HttpPost]
    [EndpointSummary("Create an assessment entry for a student")]
    [EndpointDescription("Registers a new assessment score (e.g., Midterm, Final) for a specific student enrolled in the course.")]
    [ProducesResponseType(typeof(AssessmentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAssessment(int courseId, CreateAssessmentRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var course = await courseService.GetByIdAsync(courseId, default);
        if (course is null) return NotFound();

        var existingAssessments = await assessmentService.GetByCourseAsync(courseId);
        
        // Strict check to block both "Midterm Exam" and "Midterm" together
        var isDuplicate = existingAssessments.Any(a => 
           // a.StudentId == request.StudentId && 
            ((a.Title.Contains("midterm", StringComparison.OrdinalIgnoreCase) && 
              request.Title.Contains("midterm", StringComparison.OrdinalIgnoreCase)) ||
             a.Title.Equals(request.Title, StringComparison.OrdinalIgnoreCase)));

        if (isDuplicate)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Academic Constraint Violation",
                detail: $"An assessment matching or resembling '{request.Title}' already exists for student ID {request.StudentId} in this course."
            );
        }

        var assessmentEntity = new Assessment
        {
            Title = request.Title,
            MaxScore = request.MaxScore,
            //ScoreObtained = request.ScoreObtained,
            Weight = request.Weight,
            CourseId = courseId,
            //StudentId = request.StudentId
        };

        try
        {
            var result = await assessmentService.CreateAssessmentAsync(assessmentEntity);

            // Instantiate utilizing the primary positional constructor
            var responseDto = new AssessmentResponseDto(
                result.Id,
                result.Title,
                (decimal)result.MaxScore,
                //(decimal)result.ScoreObtained,
                (decimal)result.Weight,
                result.CourseId
                //result.StudentId
            );
            return CreatedAtAction(nameof(GetAssessment), new { courseId, id = result.Id }, responseDto);
        }
        catch (ArgumentException ex) 
        { 
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Validation Error", detail: ex.Message); 
        }
        catch (InvalidOperationException ex) 
        { 
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Academic Constraint Violation", detail: ex.Message); 
        }
    }

    // Action 4: PATCH /api/courses/{courseId}/assessments/{id}/score
    [HttpPatch("{id:int}/score", Name = "UpdateAssessmentScore")]
    [ProducesResponseType(typeof(AssessmentResponseDto), StatusCodes.Status200OK)] //  Uses clean DTO for OpenAPI
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Update a score outcome for an assessment record")]
    public async Task<IActionResult> UpdateScore(int courseId, int id, [FromBody] UpdateAssessmentScoreRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var updated = await assessmentService.UpdateScoreAsync(id, request.ScoreObtained);
            if (updated is null || updated.CourseId != courseId) return NotFound();

           var responseDto = new AssessmentResponseDto(
                updated.Id,
                updated.Title,
                (decimal)updated.MaxScore,
               // (decimal)updated.ScoreObtained,
                (decimal)updated.Weight,
                updated.CourseId
               // updated.StudentId
            );

            return Ok(responseDto);
        }
        catch (ArgumentException ex) 
        { 
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Validation Error", detail: ex.Message); 
        }
    }

    // Action 5: DELETE /api/courses/{courseId}/assessments/{id}
    [HttpDelete("{id:int}")]
    [EndpointSummary("Delete an assessment record")]
    [EndpointDescription("Permanently removes an assessment record from the database. This action cannot be undone.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAssessment(int courseId, int id)
    {
        var assessment = await assessmentService.GetByIdAsync(id);
        if (assessment is null || assessment.CourseId != courseId) return NotFound();

        // Perform the deletion inside your database context:
       await assessmentService.DeleteAssessmentAsync(id);

        return NoContent();
    }
}



// 2 assessmwents
//  1st  - the assessment definition
// 2 nd ass - the students assessment result