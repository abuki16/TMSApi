using MediatR;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Enrollments.Queries;

public class GetEnrollmentsHandler(IEnrollmentService repo) 
    : IRequestHandler<GetEnrollmentsQuery, List<EnrollmentResponseDto>>
{
    public async Task<List<EnrollmentResponseDto>> Handle(
        GetEnrollmentsQuery request, 
        CancellationToken ct)
    {
        return await repo.GetAllAsync(ct);
    }
}