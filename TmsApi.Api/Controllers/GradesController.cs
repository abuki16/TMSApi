using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GradesController : ControllerBase
{
    private readonly TmsDbContext _context;

    public GradesController(TmsDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> SubmitGrade([FromBody] GradeDto dto)
    {
        var grade = new Grade
        {
            StudentId = dto.StudentId,
            CourseId = dto.CourseId,
            Score = dto.Score
        };

        _context.Grades.Add(grade);
        await _context.SaveChangesAsync();

        return Ok(new 
        { 
            id = $"REC-{grade.Id}", 
            success = true 
        });
    }
}

public class GradeDto
{
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public int Score { get; set; }
}