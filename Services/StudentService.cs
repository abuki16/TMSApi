using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Data;
using TmsApi.Entities;

namespace TmsApi.Services;

// DTO to hold your SQL-aggregated course summary results
public record TopCourseSummary(string CourseTitle, int EnrollmentCount);

public interface IStudentService
{
    Task<Student> CreateAsync(Student student);
    Task<Student?> GetByIdAsync(string id);
    Task<IReadOnlyList<Student>> GetAllAsync();
    Task<Student?> UpdateAsync(string id, string name, int age, decimal gpa);
    Task<bool> DeleteAsync(string id);
    
    // Exercise 3: Paged student roster endpoint using a CancellationToken
    Task<IReadOnlyList<Student>> GetPagedStudentsAsync(int pageNumber, CancellationToken cancellationToken);
    
    // Exercise 3: Top 5 courses by enrollment counts using a CancellationToken
    Task<IReadOnlyList<TopCourseSummary>> GetTopCoursesAsync(CancellationToken cancellationToken);
}

public class StudentService(TmsDbContext dbContext, ILogger<StudentService> logger) : IStudentService
{
    public async Task<Student> CreateAsync(Student student)
    {
        var exists = await dbContext.Students.AnyAsync(s => s.Id == student.Id);
        if (exists)
        {
            logger.LogWarning("Create failed: Student with ID {StudentId} already exists.", student.Id);
            throw new InvalidOperationException($"Student with ID {student.Id} already exists.");
        }

        var dbStudent = new Student
        {
            Id = student.Id,
            RegistrationNumber = $"REG-{student.Id}", 
            Name = student.Name,
            GPA = student.GPA,
            IsActive = true,
            IsDeleted = false
        };

        await dbContext.Students.AddAsync(dbStudent);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Successfully created student: {StudentId}", student.Id);
        return student;
    }

    public async Task<Student?> GetByIdAsync(string id)
    {
        if (!int.TryParse(id, out int parsedId)) return null;
        return await dbContext.Students.FirstOrDefaultAsync(s => s.Id == parsedId);
    }

    public async Task<IReadOnlyList<Student>> GetAllAsync()
    {
        var dbList = await dbContext.Students.ToListAsync();
        return dbList.Select(s => new Student
        {
            Id = s.Id,
            Name = s.Name,
            RegistrationNumber = s.RegistrationNumber,
            Age = 20, 
            GPA = s.GPA
        }).ToList();
    }

    public async Task<Student?> UpdateAsync(string id, string name, int age, decimal gpa)
    {
        if (!int.TryParse(id, out int parsedId)) return null;
        var student = await dbContext.Students.FirstOrDefaultAsync(s => s.Id == parsedId);
        if (student == null)
        {
            logger.LogWarning("Update failed: Student {StudentId} not found.", id);
            return null;
        }

        try
        {
            student.Name = name;
            student.GPA = gpa;

            await dbContext.SaveChangesAsync();

            logger.LogInformation("Successfully updated student: {StudentId}", id);
            return student;
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            logger.LogError(ex, "Validation error updating student {StudentId}.", id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        if (!int.TryParse(id, out int parsedId)) return false;
        var student = await dbContext.Students.FirstOrDefaultAsync(s => s.Id == parsedId);
        if (student == null) return false;

        // dbContext.Students.Remove(student);
        // await dbContext.SaveChangesAsync();
        student.IsDeleted = true; 
    await dbContext.SaveChangesAsync();

        logger.LogInformation("Deleted student record: {StudentId}", id);
        return true;
    }

    // Exercise 3 Task 1 Implementation
    public async Task<IReadOnlyList<Student>> GetPagedStudentsAsync(int pageNumber, CancellationToken cancellationToken)
{
    const int pageSize = 20; 
    if (pageNumber < 1) pageNumber = 1;
    int itemsToSkip = (pageNumber - 1) * pageSize;

    return await dbContext.Students
        .OrderBy(s => s.Name) 
        .ThenBy(s => s.Id) // Guarantees a stable sort if names match
        .Skip(itemsToSkip)
        .Take(pageSize)
        .ToListAsync(cancellationToken); 
}

    //     return dbList.Select(s => new Student
    //     {
    //         Id = s.Id,
    //         Name = s.Name,
    //         RegistrationNumber = s.RegistrationNumber,
    //         Age = 20,
    //         GPA = s.GPA
    //     }).ToList();
    // }

  
    // Exercise 3 Task 2 Implementation
    public async Task<IReadOnlyList<TopCourseSummary>> GetTopCoursesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Courses
            .Select(c => new
            {
                Title = c.Title,
                Count = c.Enrollments.Count
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .Select(x => new TopCourseSummary(x.Title, x.Count))
            .ToListAsync(cancellationToken);
    }
}