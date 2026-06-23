using Microsoft.Extensions.DependencyInjection;
using TmsApi.Services;
namespace TmsApi.Worker;

public class EnrollmentWorker
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EnrollmentWorker(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task ProcessBatch()
    {
        using var scope = _scopeFactory.CreateScope();

        var enrollmentService =
            scope.ServiceProvider.GetRequiredService<IEnrollmentService>();

        // Simulate worker activity
        var enrollments = await enrollmentService.GetAllAsync();

        Console.WriteLine(
            $"Worker processed {enrollments.Count} enrollments.");
    }
}
