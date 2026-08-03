using MediatR;
using TmsApi.Application.Common;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Enrollments.Commands;

public class ApproveEnrollmentCommandHandler(
    IEnrollmentService enrollmentService)
    : IRequestHandler<ApproveEnrollmentCommand, Result<Unit, EnrollmentError>>
{
    public async Task<Result<Unit, EnrollmentError>> Handle(
        ApproveEnrollmentCommand command, 
        CancellationToken ct)
    {
        var enrollment = await enrollmentService.GetEntityByIdAsync(command.Id, ct);
        if (enrollment is null)
        {
            return Result<Unit, EnrollmentError>.Failure(
                EnrollmentError.NotFound(command.Id));
        }

        enrollment.Status = "Approved";
        await enrollmentService.UpdateAsync(enrollment, ct);

        return Result<Unit, EnrollmentError>.Success(Unit.Value);
    }
}