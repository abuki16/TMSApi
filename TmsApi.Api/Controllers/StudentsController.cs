using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Design;
using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Services;

namespace TmsApi.Api.Controllers;

public record CreateStudentRequest(int Id, string Name, int Age, decimal GPA);
//public record UpdateStudentRequest(string Name, int Age, decimal GPA);
public record UpdateStudentRequest(string Name, int Age, decimal GPA, uint Version);
[ApiController]
[Route("api/students")]
// Injecting TmsDbContext directly into the primary constructor here
public class StudentsController(IStudentService studentService, TmsDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var students = await studentService.GetAllAsync();
        return Ok(students);
    }

    // Exercise 3 Task 1: GET /api/students/paged?pageNumber=1
    [HttpGet("search-paged")]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        var result = await studentService.GetPagedStudentsAsync(pageNumber, cancellationToken);
        return Ok(result);
    }

    // Exercise 3 Task 2: GET /api/students/top-courses
    [HttpGet("top-courses")]
    public async Task<IActionResult> GetTopCourses(CancellationToken cancellationToken = default)
    {
        var result = await studentService.GetTopCoursesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var student = await studentService.GetByIdAsync(id.ToString());
        return student is not null ? Ok(student) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStudentRequest request)
    {
        try
        {
            var student = new Student 
            { 
                Id = request.Id, 
                Name = request.Name, 
                Age = request.Age, 
                GPA = request.GPA ,
                RegistrationNumber = $"TMS-2026-{request.Id}"
            };
            var result = await studentService.CreateAsync(student);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
[HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateStudentRequest request)
    {
        try
        {
            var studentId = int.Parse(id);
            // 1. Fetch the student from the database context directly
            var student = await context.Students.FirstOrDefaultAsync(s => s.Id == studentId);
            
            if (student is null) return NotFound();

            // 2. Feed the client's version token back into EF Core tracking mechanics
            context.Entry(student).Property(s => s.Version).OriginalValue = request.Version;

            // 3. Mutate fields
            student.Name = request.Name;
            student.Age = request.Age;
            student.GPA = request.GPA;

            // 4. Save changes (triggers your dynamic shadow audit stamp and version checking)
            await context.SaveChangesAsync();
            return Ok(student);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Exercise 8 Target: Caught concurrent edit conflict!
            return Conflict(new { message = "Concurrency conflict caught! Another client has updated this student record in the background. Please reload your data." });
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
    
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await studentService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
    
    // Exercise 7 Part A: Intentional N+1 using DB Context Directly
    [HttpGet("test-n-plus-one")]
    public async Task<IActionResult> TestNPlusOne(CancellationToken cancellationToken)
    {
        Console.WriteLine("--- [START] EXERCISE 7 PART A: INTENTIONAL N+1 ---");

        // Uses the database context parameter "context" directly
        var students = await context.Students.AsNoTracking().ToListAsync(cancellationToken);
        
        foreach (var s in students)
        {
            // Query enrollment count for this student inside the loop 
            // This should produce 1 + N SQL statements. Count them in the log
            var count = await context.Enrollments
                .AsNoTracking()
                .CountAsync(e => e.StudentId == s.Id, cancellationToken);
                
            Console.WriteLine($"{s.Name}: {count} enrollments");
        }

        Console.WriteLine("--- [END] EXERCISE 7 PART A: INTENTIONAL N+1 ---");

        
    
    // PART B FIX: SINGLE QUERY WITH PROJECTION (Shaping)
    
              Console.WriteLine("--- [START] EXERCISE 7 PART B: PROJECTION FIX ---");
        
        // EF translates s.Enrollments.Count into a SQL subquery statement
        var report = await context.Students
            .AsNoTracking()
            .Select(s => new
            {
                s.Name,
                EnrollmentCount = s.Enrollments.Count
            })
            .ToListAsync(cancellationToken);

        foreach (var r in report)
        {
            Console.WriteLine($"[Part B Projection] {r.Name}: {r.EnrollmentCount} enrollments");
        }

        Console.WriteLine("--- [END] EXERCISE 7 PART B: PROJECTION FIX ---\n");
    
    return Ok("Exercise 7 Part A and Part B executed successfully. Check your console logs to compare the SQL queries!");
    }

[HttpGet("temp-delete-12")]
public async Task<IActionResult> TempDelete12()
{
    var student = await context.Students.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == 12);
    if (student == null) return NotFound("Student 12 not found in database.");
    
    student.IsDeleted = true;
    await context.SaveChangesAsync();
    return Ok("Student 12 soft-deleted successfully via EF Core!");
}
[HttpPost("seed-old-enrollments")]
[HttpGet("seed-old-enrollments")]
public async Task<IActionResult> SeedOldEnrollments()
{
    // Ensure Student 12 has two old enrollments dated in 2025
    var oldEnrollment1 = new Enrollment { StudentId = 12, CourseId = 1, EnrolledAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsArchived = false };
    var oldEnrollment2 = new Enrollment { StudentId = 12, CourseId = 2, EnrolledAt = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc), IsArchived = false };

    context.Enrollments.AddRange(oldEnrollment1, oldEnrollment2);
    await context.SaveChangesAsync();

    return Ok("Two old enrollments from 2025 inserted into the database!");
}

    // Exercise 9 Task 1: Admin Route to see ALL students including soft-deleted ones
[HttpGet("admin/all-students")]
public async Task<IActionResult> GetAdminStudents(CancellationToken cancellationToken)
{
    // IgnoreQueryFilters bypasses the !IsDeleted rule
    var allStudents = await context.Students
        .IgnoreQueryFilters()
        .AsNoTracking()
        .ToListAsync(cancellationToken);

    return Ok(allStudents);
}

// Exercise 9 Task 2: Bulk Archive Old Enrollments using ExecuteUpdateAsync
[HttpPost("enrollments/bulk-archive")]
public async Task<IActionResult> BulkArchiveEnrollments([FromQuery] int daysOld, CancellationToken cancellationToken)
{
    var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);

    // High Performance Bulk Update: Modifies rows directly in the database without loading them into memory
    int rowsAffected = await context.Enrollments
        .Where(e => e.EnrolledAt < cutoffDate && !e.IsArchived)
        .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsArchived, true), cancellationToken);

    return Ok(new { message = "Bulk archive completed successfully.", rowsUpdated = rowsAffected });
}
    
}