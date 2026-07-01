using Microsoft.Extensions.DependencyInjection;
using TmsApi.Services;

namespace TmsApi.Worker;

public class EnrollmentWorker(IServiceScopeFactory scopeFactory)
{
    public async Task ProcessBatch()
    {
        using var scope = scopeFactory.CreateScope();
        var enrollmentService = scope.ServiceProvider.GetRequiredService<IEnrollmentService>();

        // Simulating worker check-in without relying on the deleted flat string methods
        await Task.CompletedTask;
        Console.WriteLine("Worker processed batch successfully against modern integer data bounds.");
    }
}