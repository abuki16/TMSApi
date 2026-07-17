using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;
using TmsApi.Application.Interfaces;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Design;
using TmsApi.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/assessments/{assessmentId:int}/results")]
[Tags("Assessment Results")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class AssessmentResultsController(
    IAssessmentResultService resultService,
    IAssessmentService assessmentService,
    LinkGenerator linkGenerator) : ControllerBase
{
    // Action 1: GET /api/assessments/{assessmentId}/results
    [HttpGet(Name = "ListAssessmentResults")]
    [ProducesResponseType(typeof(IReadOnlyList<AssessmentResultResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("List student grades for an assessment definition")]
    public async Task<IActionResult> GetResults(int assessmentId)
    {
        var assessment = await assessmentService.GetByIdAsync(assessmentId);
        if (assessment is null) return NotFound();

        var results = await resultService.GetByAssessmentAsync(assessmentId);
        return Ok(results);
    }

    // Action 2: GET /api/assessments/{assessmentId}/results/{id}
    [HttpGet("{id:int}", Name = nameof(GetResult))]
    [ProducesResponseType(typeof(AssessmentResultDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get an individual student's grade record")]
    public async Task<IActionResult> GetResult(int assessmentId, int id)
    {
        var result = await resultService.GetByIdAsync(id);
        if (result is null || result.AssessmentId != assessmentId) return NotFound();

        var selfPath = linkGenerator.GetPathByName(HttpContext, nameof(GetResult), new { assessmentId, id });
        var updatePath = linkGenerator.GetPathByName(HttpContext, "UpdateStudentScore", new { assessmentId, id });
        var deletePath = linkGenerator.GetPathByName(HttpContext, "DeleteGradeRecord", new { assessmentId, id });

        var links = new List<LinkDto>
        {
            new(selfPath ?? "", "self", "GET"),
            new(updatePath ?? "", "update_score", "PATCH"),
            new(deletePath ?? "", "delete", "DELETE")
        };

        var detailDto = new AssessmentResultDetailDto
        {
            Id = result.Id,
            Title = result.Title,
            ScoreObtained = result.ScoreObtained,
            Weight = result.Weight,
            AssessmentId = result.AssessmentId,
            StudentId = result.StudentId,
            Links = links
        };

        return Ok(detailDto);
    }

    // Action 3: POST /api/assessments/{assessmentId}/results
    [HttpPost]
    [EndpointSummary("Grade an enrolled student")]
    [ProducesResponseType(typeof(AssessmentResultResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateResult(int assessmentId, [FromBody] GradeStudentRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var assessment = await assessmentService.GetByIdAsync(assessmentId);
        if (assessment is null) return NotFound();

        var entity = new AssessmentResult
        {
            Title = request.Title,
            ScoreObtained = request.ScoreObtained,
            Weight = request.Weight,
            AssessmentId = assessmentId,
            StudentId = request.StudentId
        };

        try
        {
            var created = await resultService.CreateResultAsync(entity);
            var loadedResult = await resultService.GetByIdAsync(created.Id);

            if (loadedResult is null)
            {
                return Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Database Sync Error",
                    detail: "The grading record was saved but could not be re-loaded."
                );
            }

            var responseDto = new AssessmentResultResponseDto(
                loadedResult.Id,
                loadedResult.Title,
                loadedResult.ScoreObtained,
                loadedResult.Weight,
                loadedResult.AssessmentId,
                loadedResult.StudentId,
                loadedResult.Student?.Name ?? "Enrolled Student"
            );

            return CreatedAtAction(nameof(GetResult), new { assessmentId, id = created.Id }, responseDto);
        }
        catch (ArgumentException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest, 
                title: "Validation Error", 
                detail: ex.Message
            );
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict, 
                title: "Double Grading Constraint", 
                detail: ex.Message
            );
        }
    }

    // Action 4: PATCH /api/assessments/{assessmentId}/results/{id}/score
    [HttpPatch("{id:int}/score", Name = "UpdateStudentScore")]
    [ProducesResponseType(typeof(AssessmentResultResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Update a student's score outcome")]
    public async Task<IActionResult> UpdateScore(int assessmentId, int id, [FromBody] UpdateAssessmentResultScoreRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var updated = await resultService.UpdateScoreAsync(id, request.ScoreObtained);
            if (updated is null || updated.AssessmentId != assessmentId) return NotFound();

            var loadedResult = await resultService.GetByIdAsync(updated.Id);
            if (loadedResult is null) return NotFound();

            var responseDto = new AssessmentResultResponseDto(
                loadedResult.Id,
                loadedResult.Title,
                loadedResult.ScoreObtained,
                loadedResult.Weight,
                loadedResult.AssessmentId,
                loadedResult.StudentId,
                loadedResult.Student?.Name ?? "Enrolled Student"
            );

            return Ok(responseDto);
        }
        catch (ArgumentException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest, 
                title: "Validation Error", 
                detail: ex.Message
            );
        }
    }

    // Action 5: DELETE /api/assessments/{assessmentId}/results/{id}
    [HttpDelete("{id:int}", Name = "DeleteGradeRecord")]
    [EndpointSummary("Delete a student's grade record")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteResult(int assessmentId, int id)
    {
        var result = await resultService.GetByIdAsync(id);
        if (result is null || result.AssessmentId != assessmentId) return NotFound();

        await resultService.DeleteResultAsync(id);
        return NoContent();
    }
}