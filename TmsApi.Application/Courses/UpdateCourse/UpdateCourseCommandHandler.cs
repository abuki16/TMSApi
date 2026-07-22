using MediatR;
using TmsApi.Application.Courses.Commands.UpdateCourse;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Courses.UpdateCourse;

public class UpdateCourseHandler(
    ICourseService service,
    ICachedCourseService cachedService)
    : IRequestHandler<UpdateCourseCommand, bool>
{
    public async Task<bool> Handle(UpdateCourseCommand command, CancellationToken ct)
    {
        await service.UpdateAsync(command.Id, new UpdateCourseRequest(command.Code, command.Title, command.MaxCapacity), ct);
        await cachedService.InvalidateCourseCacheAsync(ct);
        return true;
    }
}