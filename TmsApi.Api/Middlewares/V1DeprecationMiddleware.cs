using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Design;

namespace TmsApi.Api.Middlewares
{
    public class V1DeprecationMiddleware(RequestDelegate next)
    {
        private static readonly DateTimeOffset SunsetDate =
            new(2026, 12, 31, 0, 0, 0, TimeSpan.Zero);

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                // Ensure we only intercept actual v1 routes
                if (context.Request.Path.StartsWithSegments("/api/v1"))
                {
                    context.Response.Headers["Deprecation"] = "true";
                    context.Response.Headers["Sunset"] = SunsetDate.ToString("R");
                    
                    // Dynamic calculation to route the developer to the equivalent v2 endpoint
                    var remainingPath = context.Request.Path.Value?[7..];
                    context.Response.Headers["Link"] =
                        $"<{context.Request.Scheme}://{context.Request.Host}/api/v2{remainingPath}>; rel=\"successor-version\"";
                }
                return Task.CompletedTask;
            });

            await next(context);
        }
    }
}