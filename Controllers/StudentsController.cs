using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Services;
using TmsApi.Data;
using TmsApi.Entities;

namespace TmsApi.Controllers;
public record CreateStudentRequest(int Id, string Name, int Age, decimal GPA);
public record UpdateStudentRequest(string Name, int Age, decimal GPA);

[ApiController]
[Route("api/students")]
public class StudentsController(IStudentService studentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var students = await studentService.GetAllAsync();
        return Ok(students);
    }

    // Exercise 3 Task 1: GET /api/students/paged?pageNumber=1
    [HttpGet("paged")]
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

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var student = await studentService.GetByIdAsync(id);
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
            var updated = await studentService.UpdateAsync(id, request.Name, request.Age, request.GPA);
            return updated is not null ? Ok(updated) : NotFound();
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
}