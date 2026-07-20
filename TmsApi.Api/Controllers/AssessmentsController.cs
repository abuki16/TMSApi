using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;
using TmsApi.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TmsApi.Infrastructure.Services;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/assessments")]
[Tags("Assessment Definitions")]
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
    [EndpointSummary("List assessment definitions for a course")]
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
    [EndpointSummary("Get one assessment definition with HATEOAS links")]
    public async Task<IActionResult> GetAssessment(int courseId, int id)
    {
        var assessment = await assessmentService.GetByIdAsync(id);
        if (assessment is null || assessment.CourseId != courseId) return NotFound();

        var selfPath = linkGenerator.GetPathByName(HttpContext, nameof(GetAssessment), new { courseId, id });
        var updatePath = linkGenerator.GetPathByName(HttpContext, "UpdateAssessmentMaxScore", new { courseId, id });
        var deletePath = linkGenerator.GetPathByName(HttpContext, "DeleteAssessment", new { courseId, id });
        var coursePath = linkGenerator.GetPathByName(HttpContext, "GetCourseById", new { id = courseId });

        var links = new List<LinkDto>
        {
            new(selfPath ?? "", "self", "GET"),
            new(updatePath ?? "", "update_max_score", "PATCH"),
            new(deletePath ?? "", "delete", "DELETE"),
            new(coursePath ?? "", "course_details", "GET")
        };

        var detailDto = new AssessmentDetailDto
        {
            Id = assessment.Id,
            Title = assessment.Title,
            MaxScore = (decimal)assessment.MaxScore,
            Weight = (decimal)assessment.Weight,
            CourseId = assessment.CourseId,
            Links = links
        };

        return Ok(detailDto);
    }

    // Action 3: POST /api/courses/{courseId}/assessments
    [HttpPost]
    [EndpointSummary("Create an assessment definition for a course")]
    [EndpointDescription("Registers a new assessment type (e.g., Midterm, Final Project) for a course curriculum.")]
    [ProducesResponseType(typeof(AssessmentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAssessment(int courseId, CreateAssessmentRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var course = await courseService.GetByIdAsync(courseId, default);
        if (course is null) return NotFound();

        var existingAssessments = await assessmentService.GetByCourseAsync(courseId);
        
        // Block duplicate assignments in the same course (e.g. duplicate midterms)
        var isDuplicate = existingAssessments.Any(a => 
             ((a.Title.Contains("midterm", StringComparison.OrdinalIgnoreCase) && 
              request.Title.Contains("midterm", StringComparison.OrdinalIgnoreCase)) ||
             a.Title.Equals(request.Title, StringComparison.OrdinalIgnoreCase)));

        if (isDuplicate)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Academic Constraint Violation",
                detail: $"An assessment matching or resembling '{request.Title}' already exists in this course curriculum."
            );
        }

        var assessmentEntity = new Assessment
        {
            Title = request.Title,
            MaxScore = request.MaxScore,
            Weight = request.Weight,
            CourseId = courseId
        };

        try
        {
            var result = await assessmentService.CreateAssessmentAsync(assessmentEntity);

            var responseDto = new AssessmentResponseDto(
                result.Id,
                result.Title,
                (decimal)result.MaxScore,
                (decimal)result.Weight,
                result.CourseId
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

    // Action 4: PATCH /api/courses/{courseId}/assessments/{id}/max-score
    [HttpPatch("{id:int}/max-score", Name = "UpdateAssessmentMaxScore")]
    [ProducesResponseType(typeof(AssessmentResponseDto), StatusCodes.Status200OK)] 
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Update the maximum possible score for an assessment definition")]
    public async Task<IActionResult> UpdateMaxScore(int courseId, int id, [FromBody] UpdateAssessmentMaxScoreRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var updated = await assessmentService.UpdateScoreAsync(id, request.MaxScore);
            if (updated is null || updated.CourseId != courseId) return NotFound();

            var responseDto = new AssessmentResponseDto(
                updated.Id,
                updated.Title,
                (decimal)updated.MaxScore,
                (decimal)updated.Weight,
                updated.CourseId
            );

            return Ok(responseDto);
        }
        catch (ArgumentException ex) 
        { 
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Validation Error", detail: ex.Message); 
        }
    }

    // Action 5: DELETE /api/courses/{courseId}/assessments/{id}
    [HttpDelete("{id:int}", Name = "DeleteAssessment")]
    [EndpointSummary("Delete an assessment definition")]
    [EndpointDescription("Permanently removes an assessment definition from the course. This action cannot be undone.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAssessment(int courseId, int id)
    {
        var assessment = await assessmentService.GetByIdAsync(id);
        if (assessment is null || assessment.CourseId != courseId) return NotFound();

        await assessmentService.DeleteAssessmentAsync(id);

        return NoContent();
    }
}

// 2 assessmwents
//  1st  - the assessment definition
// 2 nd ass - the students assessment result