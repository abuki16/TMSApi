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
        var enrollment = await _context.Enrollments.FirstOrDefaultAsync(e =>
            e.StudentId == dto.StudentId &&
            e.CourseId == dto.CourseId);

        if (enrollment == null || !string.Equals(enrollment.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new 
            { 
                detail = "Cannot submit grade: The student's enrollment in this course has not been approved by an administrator." 
            });
        }

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
    public async Task<IActionResult> UpdateEnrollmentGrade(
        int id, 
        [FromBody] UpdateEnrollmentGradeRequest request)
    {
        if (request.Grade < 0.0m || request.Grade > 4.0m)
        {
            return BadRequest(new 
            { 
                detail = "Invalid grade: Grade must be between 0.00 and 4.00." 
            });
        }

        var enrollment = await _context.Enrollments.FindAsync(id);
        if (enrollment == null)
        {
            return NotFound(new { message = "Enrollment not found." });
        }

        if (!string.Equals(enrollment.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new 
            { 
                detail = "Cannot grade student: The student's enrollment in this course has not been approved by an administrator." 
            });
        }

        enrollment.Grade = Math.Round(request.Grade, 2);

        // Recalculate student GPA
        var student = await _context.Students
            .Include(s => s.Enrollments)
            .FirstOrDefaultAsync(s => s.Id == enrollment.StudentId);

        if (student != null)
        {
            var graded = student.Enrollments
                .Where(e => e.Grade.HasValue)
                .ToList();

            if (graded.Count > 0)
            {
                student.GPA = Math.Round(
                    graded.Average(e => e.Grade!.Value), 
                    2);
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new 
        { 
            success = true, 
            enrollmentId = id, 
            grade = enrollment.Grade, 
            gpa = student?.GPA 
        });
    }
}

public class GradeDto
{
    public int StudentId { get; set; }
    public int CourseId { get; set; }
    public int Score { get; set; }
}