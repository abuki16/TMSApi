using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using TmsApi.Data;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/registrar")]
public class RegistrarController(TmsDbContext context) : ControllerBase
{
    // Query 1: How many active students have a GPA >= 3.0?
    [HttpGet("active-high-gpa-count")]
    public async Task<IActionResult> GetActiveHighGpaCount()
    {
        var count = await context.Students
            .Where(s => s.IsActive && s.GPA >= 3.0m)
            .CountAsync();
            
        return Ok(new { Count = count });
    }

    // Query 2: Which courses have the most enrollments, sorted descending?
    [HttpGet("courses-by-enrollments")]
    public async Task<IActionResult> GetCoursesByEnrollments()
    {
        var list = await context.Courses
            .Select(c => new
            {
                c.Title,
                EnrollmentCount = c.Enrollments.Count
            })
            .OrderByDescending(x => x.EnrollmentCount)
            .ToListAsync();
            
        return Ok(list);
    }

    // Query 3: What is the average GPA per course?
    [HttpGet("average-gpa-per-course")]
    public async Task<IActionResult> GetAverageGpaPerCourse()
    {
        var list = await context.Courses
            .Select(c => new
            {
                CourseTitle = c.Title,
                AverageGpa = c.Enrollments.Any() 
                    ? c.Enrollments.Average(e => e.Student.GPA) 
                    : 0.0m
            })
            .ToListAsync();

        return Ok(list);
    }

    // Query 4: Which students have zero enrollments?
    [HttpGet("unenrolled-students")]
    public async Task<IActionResult> GetUnenrolledStudents()
    {
        var list = await context.Students
            .Where(s => !s.Enrollments.Any())
            .Select(s => s.Name)
            .ToListAsync();
            
        return Ok(list);
    }
}