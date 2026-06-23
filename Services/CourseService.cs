using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Data;
using TmsApi.Entities;

namespace TmsApi.Services;

public interface ICourseService
{
    Task<Course> CreateAsync(Course course);
    Task<Course?> GetByIdAsync(string code);
    Task<IReadOnlyList<Course>> GetAllAsync();
    Task<Course?> UpdateAsync(string code, string title, int capacity);
    Task<bool> DeleteAsync(string code);
}

public class CourseService(TmsDbContext dbContext, ILogger<CourseService> logger) : ICourseService
{
    public async Task<Course> CreateAsync(Course course)
    {
        var exists = await dbContext.Courses.AnyAsync(c => c.Code == course.Code);
        if (exists)
        {
            logger.LogWarning("Create failed: Course with code {CourseCode} already exists.", course.Code);
            throw new InvalidOperationException($"Course with code {course.Code} already exists.");
        }

        // Map Controller model data to Entity database shape
        var dbCourse = new Course
        {
            Code = course.Code,
            Title = course.Title,
            Capacity = course.Capacity
            // PostgreSQL automatically generates and tracks the internal surrogate key 'Id' here
        };

        await dbContext.Courses.AddAsync(dbCourse);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Successfully created course: {CourseCode}", course.Code);
        return course;
    }

    public async Task<Course?> GetByIdAsync(string code)
    {
        var c = await dbContext.Courses.FirstOrDefaultAsync(c => c.Code == code);
        if (c == null) return null;

        // Map back to Controller Model type exactly like StudentService
        return new Course 
        { 
            Code = c.Code, 
            Title = c.Title, 
            Capacity = c.Capacity 
        };
    }

    public async Task<IReadOnlyList<Course>> GetAllAsync()
    {
        var dbList = await dbContext.Courses.ToListAsync();
        
        // Map list database entities back to interface collection definitions
        return dbList.Select(c => new Course 
        { 
            Code = c.Code, 
            Title = c.Title, 
            Capacity = c.Capacity 
        }).ToList();
    }

    public async Task<Course?> UpdateAsync(string code, string title, int capacity)
    {
        var course = await dbContext.Courses.FirstOrDefaultAsync(c => c.Code == code);
        if (course == null)
        {
            logger.LogWarning("Update failed: Course {CourseCode} not found.", code);
            return null;
        }

        try
        {
            course.Title = title;
            course.Capacity = capacity;

            await dbContext.SaveChangesAsync();

            logger.LogInformation("Successfully updated course: {CourseCode}", code);
            return new  Course 
            { 
                Code = course.Code, 
                Title = course.Title, 
                Capacity = course.Capacity 
            };
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            logger.LogError(ex, "Validation error updating course {CourseCode}.", code);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string code)
    {
        var course = await dbContext.Courses.FirstOrDefaultAsync(c => c.Code == code);
        if (course == null) return false;

        dbContext.Courses.Remove(course);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Deleted course record: {CourseCode}", code);
        return true;
    }
}