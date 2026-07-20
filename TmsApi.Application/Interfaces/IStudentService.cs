using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TmsApi.Domain.Entities;

namespace TmsApi.Application.Interfaces;

public record TopCourseSummary(string CourseTitle, int EnrollmentCount);

public interface IStudentService
{
    Task<Student> CreateAsync(Student student);
    Task<Student?> GetByIdAsync(string id);
    Task<IReadOnlyList<Student>> GetAllAsync();
    Task<Student?> UpdateAsync(string id, string name, int age, decimal gpa);
    Task<bool> DeleteAsync(string id);
    Task<IReadOnlyList<Student>> GetPagedStudentsAsync(int pageNumber, CancellationToken cancellationToken);
    Task<IReadOnlyList<TopCourseSummary>> GetTopCoursesAsync(CancellationToken cancellationToken);
}