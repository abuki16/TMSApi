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

    public record UpdateEnrollmentGradeRequest(decimal Grade);

    [HttpPut("enrollments/{id:int}")]
    public async Task<IActionResult> UpdateEnrollmentGrade(int id, [FromBody] UpdateEnrollmentGradeRequest request)
    {
        var enrollment = await _context.Enrollments.FindAsync(id);
        if (enrollment == null) return NotFound(new { message = "Enrollment not found." });

        enrollment.Grade = Math.Round(request.Grade, 2);

        // Recalculate student GPA
        var student = await _context.Students
            .Include(s => s.Enrollments)
            .FirstOrDefaultAsync(s => s.Id == enrollment.StudentId);

        if (student != null)
        {
            var graded = student.Enrollments.Where(e => e.Grade.HasValue).ToList();
            if (graded.Count > 0)
            {
                student.GPA = Math.Round(graded.Average(e => e.Grade!.Value), 2);
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true, enrollmentId = id, grade = enrollment.Grade, gpa = student?.GPA });
    }
}

public class GradeDto
{
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public int Score { get; set; }
}