using MediatR;
using TmsApi.Application.DTOs;

namespace TmsApi.Application.Enrollments.Queries;

public record GetEnrollmentsQuery() : IRequest<List<EnrollmentResponseDto>>;