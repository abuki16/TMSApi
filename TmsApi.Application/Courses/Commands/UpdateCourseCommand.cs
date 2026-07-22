using MediatR;

namespace TmsApi.Application.Courses.Commands.UpdateCourse;

public record UpdateCourseCommand(
    int Id,
    string Code,
    string Title,
    int MaxCapacity) : IRequest<bool>;