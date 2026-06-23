using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Services;
using TmsApi.Data;
using TmsApi.Entities;

namespace TmsApi.Controllers;

public record CreateCourseRequest(string Code, string Title, int Capacity);
public record UpdateCourseRequest(string Title, int Capacity);

[ApiController]
[Route("api/courses")]
public class CoursesController(ICourseService courseService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var courses = await courseService.GetAllAsync();
        return Ok(courses);
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetById(string code)
    {
        var course = await courseService.GetByIdAsync(code);
        return course is not null ? Ok(course) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCourseRequest request)
    {
        try
        {
            var course = new Course 
            { 
                Code = request.Code, 
                Title = request.Title, 
                Capacity = request.Capacity 
            };
            var result = await courseService.CreateAsync(course);
            return CreatedAtAction(nameof(GetById), new { code = result.Code }, result);
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

    [HttpPut("{code}")]
    public async Task<IActionResult> Update(string code, [FromBody] UpdateCourseRequest request)
    {
        try
        {
            var updated = await courseService.UpdateAsync(code, request.Title, request.Capacity);
            return updated is not null ? Ok(updated) : NotFound();
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code)
    {
        var deleted = await courseService.DeleteAsync(code);
        return deleted ? NoContent() : NotFound();
    }
}