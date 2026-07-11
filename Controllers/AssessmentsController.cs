using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Services;
using TmsApi.Entities;

namespace TmsApi.Controllers;

public record CreateAssessmentRequest(
    string Title, 
    decimal MaxScore, 
    decimal ScoreObtained, 
    decimal Weight, 
    int CourseId, 
    int StudentId
);

public record UpdateAssessmentScoreRequest(
    decimal ScoreObtained
);

[ApiController]
[Route("api/assessments")]
public class AssessmentsController(IAssessmentService assessmentService) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var assessment = await assessmentService.GetByIdAsync(id);
        return assessment is not null ? Ok(assessment) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAssessmentRequest request)
    {
        try
        {
            var assessment = new Assessment
            {
                Title = request.Title,
                MaxScore = request.MaxScore,
                ScoreObtained = request.ScoreObtained,
                Weight = request.Weight,
                CourseId = request.CourseId,
                StudentId = request.StudentId
            };

            var result = await assessmentService.CreateAssessmentAsync(assessment);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}/score")]
    public async Task<IActionResult> UpdateScore(int id, [FromBody] UpdateAssessmentScoreRequest request)
    {
        try
        {
            var updatedAssessment = await assessmentService.UpdateScoreAsync(id, request.ScoreObtained);
            return updatedAssessment is not null ? Ok(updatedAssessment) : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}