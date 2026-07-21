using MediatR;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Courses.Queries;

public class GetCoursesHandler(ICachedCourseService cachedCourseService) 
    : IRequestHandler<GetCoursesQuery, List<CourseResponseDto>>
{
    public async Task<List<CourseResponseDto>> Handle(GetCoursesQuery request, CancellationToken ct)
    {
        return await cachedCourseService.GetAllCoursesAsync(ct);
    }
}