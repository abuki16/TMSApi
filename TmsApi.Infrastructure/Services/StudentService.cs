using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.Interfaces; // Required to see the interface
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

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
        return await dbContext.Students.ToListAsync();
    }

    public async Task<Student?> UpdateAsync(string id, string name, int age, decimal gpa)
    {
        if (!int.TryParse(id, out int parsedId)) return null;
        var student = await dbContext.Students.FirstOrDefaultAsync(s => s.Id == parsedId);
        if (student == null) return null;

        student.Name = name;
        student.GPA = gpa;
        await dbContext.SaveChangesAsync();
        return student;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        if (!int.TryParse(id, out int parsedId)) return false;
        var student = await dbContext.Students.FirstOrDefaultAsync(s => s.Id == parsedId);
        if (student == null) return false;

        student.IsDeleted = true; 
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<IReadOnlyList<Student>> GetPagedStudentsAsync(int pageNumber, CancellationToken cancellationToken)
    {
        const int pageSize = 20; 
        if (pageNumber < 1) pageNumber = 1;
        
        return await dbContext.Students
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken); 
    }

    public async Task<IReadOnlyList<TopCourseSummary>> GetTopCoursesAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Courses
            .Select(c => new TopCourseSummary(c.Title, c.Enrollments.Count))
            .OrderByDescending(x => x.EnrollmentCount)
            .Take(5)
            .ToListAsync(cancellationToken);
    }
}