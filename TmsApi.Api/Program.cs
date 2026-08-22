using System.Text;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using TmsApi.Api.Authorization;
using TmsApi.Api.Filters;
using TmsApi.Api.Hubs;
using TmsApi.Api.Middlewares;
using TmsApi.Api.Notifications;
using TmsApi.Api.Options;
using TmsApi.Api.RateLimiting;
using TmsApi.Application.Interfaces;
using TmsApi.Application.Notifications;
using TmsApi.Application.Transcripts;
using TmsApi.Infrastructure.Identity;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;
using TmsApi.Infrastructure.Transcripts;
using TmsApi.Infrastructure.Worker;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. REGISTER SERVICES
// ==========================================

builder.Services.AddControllers(options =>
{
    options.Filters.Add<AuditLogFilter>();
});

builder.Services.AddHealthChecks();

builder.Services.AddSignalR();

// API Versioning Configuration
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version")
    );
})

.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ProblemDetails and Global Exception Handling Framework
builder.Services.AddExceptionHandler<TmsApi.Api.ExceptionHandlers.GlobalExceptionHandler>();
builder.Services.AddProblemDetails(); 

builder.Services.AddAntiforgery(options =>
{
options.HeaderName = "X-XSRF-TOKEN";
});

// OpenAPI / Swagger Configuration for Multiple API Versions
builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer((document, context, ct) => {
    document.Info.Version = "v1";
    document.Info.Title = "TMS API V1";
    return Task.CompletedTask;
}));

builder.Services.AddOpenApi("v2", options => options.AddDocumentTransformer((document, context, ct) => {
    document.Info.Version = "v2";
    document.Info.Title = "TMS API V2";
    return Task.CompletedTask;
}));

 // 
builder.Services.AddRateLimiter(options => 
{ 
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, 
string>(httpContext => 
    { 
        var (partitionKey, tier) = ApiKeyResolver.Resolve(httpContext); 
 
        return tier switch 
        { 
            ApiKeyTier.Paid => RateLimitPartition.GetTokenBucketLimiter( 
                partitionKey: $"paid:{partitionKey}", 
                factory: _ => new TokenBucketRateLimiterOptions 
                { 
                    TokenLimit = 200, 
                    TokensPerPeriod = 100, 
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10), 
                    QueueLimit = 0, 
                    AutoReplenishment = true 
                }), 
            ApiKeyTier.Free => RateLimitPartition.GetTokenBucketLimiter( 
                partitionKey: $"free:{partitionKey}", 
                factory: _ => new TokenBucketRateLimiterOptions 
                { 
                    TokenLimit = 30, 
                    TokensPerPeriod = 10, 
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10), 
                    QueueLimit = 0, 
                    AutoReplenishment = true 
                }), 
            _ => RateLimitPartition.GetTokenBucketLimiter( 
                partitionKey: $"anon:{partitionKey}", 
                factory: _ => new TokenBucketRateLimiterOptions 
                { 
                    TokenLimit = 10, 
                    TokensPerPeriod = 5, 
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10), 
                    QueueLimit = 0, 
                    AutoReplenishment = true 
                }) 
        }; 
    }); 
 
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests; 
    options.OnRejected = async (context, ct) => 
    { 
        var retryAfter = "10"; 
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var 
ts)) 
            retryAfter = ((int)ts.TotalSeconds).ToString(); 
 
        context.HttpContext.Response.Headers.RetryAfter = retryAfter; 
        context.HttpContext.Response.ContentType = 
"application/problem+json"; 
        await context.HttpContext.Response.WriteAsJsonAsync(new 
ProblemDetails 
        { 
            Title = "Rate limit exceeded", 
            Detail = $"Too many requests. Retry after {retryAfter} seconds.", 
            Status = StatusCodes.Status429TooManyRequests, 
            Type = "https://tms.local/errors/rate_limit_exceeded" 
        }, ct); 
    };
    options.AddConcurrencyLimiter("transcripts", opt => 
    { 
        opt.PermitLimit = 5;          // Maximum of 5 in-flight transcripts at once
        opt.QueueLimit = 20;          // Queue up to 20 more waiting requests
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst; 
    }); 
    
    options.AddTokenBucketLimiter("search", opt => 
{ 
    opt.TokenLimit = 10;                     // Max burst capacity
    opt.TokensPerPeriod = 5;                 // Refill amount
    opt.ReplenishmentPeriod = TimeSpan.FromSeconds(10); // Refill interval
    opt.QueueLimit = 2;                      // Small queue for overflow
});
   options.AddFixedWindowLimiter("AuthLimiter", opt =>
{
    opt.PermitLimit = 5;
    opt.Window = TimeSpan.FromMinutes(1);
    opt.QueueLimit = 0;
});
}); 


builder.Services.AddIdentityCore<TmsUser>(options =>
{
    // Enterprise Password Policy
    options.Password.RequiredLength = 12;
    options.Password.RequireUppercase = true;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = true;

    // Brute-Force Lockout Protection
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<TmsDbContext>();

// Production-only leave commented in lab
// builder.Services.AddStackExchangeRedisCache(options =>
// {
//     options.Configuration = builder.Configuration.GetConnectionString("Redis");
//     options.InstanceName = "tms:";
// });

//Register Hybrid Cache
builder.Services.AddHybridCache(options =>
{
options.DefaultEntryOptions = new HybridCacheEntryOptions
{
Expiration = TimeSpan.FromMinutes(10),
LocalCacheExpiration = TimeSpan.FromMinutes(2)
};
});

// Domain & Infrastructure Service Registration
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IAssessmentService, AssessmentService>();
builder.Services.AddScoped<IAssessmentResultService, AssessmentResultService>();
builder.Services.AddScoped<CertificateService>();
builder.Services.AddScoped<ICachedCourseService, CachedCourseService>();

// Hosted Background Services
builder.Services.AddSingleton<EnrollmentWorker>();
// Transcript servce 
builder.Services.AddSingleton<ITranscriptStatusStore, InMemoryTranscriptStatusStore>();

builder.Services.AddSingleton(Channel.CreateBounded<TranscriptRequest>(
new BoundedChannelOptions(100)
{
FullMode = BoundedChannelFullMode.Wait
}));

builder.Services.AddSingleton<ITranscriptNotificationService, SignalRTranscriptNotificationService>();

builder.Services.AddHostedService<TranscriptWorker>();

// Database Context Registration
builder.Services.AddDbContext<TmsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TmsDatabase")));

// MediatR & FluentValidation Pipeline Configuration
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(TmsApi.Application.Enrollments.Commands.EnrollStudentHandler).Assembly));

builder.Services.AddValidatorsFromAssembly(typeof(TmsApi.Application.Enrollments.Commands.EnrollStudentValidator).Assembly);

// Pipeline Behaviors (Order is critical: Logging wraps Validation)
builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(TmsApi.Application.Behaviors.LoggingBehavior<,>));
builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(TmsApi.Application.Behaviors.ValidationBehavior<,>));

// // Authentication and Security
// builder.Services.AddAuthentication("Training")
//     .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);
// 1. Register TokenService dependency injection
builder.Services.AddScoped<TokenService>();

// 2. Configure Authentication with multiple schemes
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
})
.AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);

builder.Services.AddAuthorization();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CanEditCourse", policy =>
        policy.Requirements.Add(new CourseInstructorRequirement()));

builder.Services.AddSingleton<IAuthorizationHandler, CourseInstructorHandler>();

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});


// Options Validation on Startup
builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});
// Load allowed origins from appsettings.Development.json
var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];

// Register the CORS policy in the Dependency Injection container
builder.Services.AddCors(options =>
{
    options.AddPolicy("TmsClient", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials() // Vital for HttpOnly auth cookies in Session 2
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});



Console.WriteLine("Payments:GatewayUrl = " + builder.Configuration["Payments:GatewayUrl"]);
Console.WriteLine("Payments:MaxDepositBirr = " + builder.Configuration["Payments:MaxDepositBirr"]);


var app = builder.Build();

// Make sure UseCors is placed before authorization/controllers middleware

// app.UseCors("AllowAngular");




// ==========================================
// 2. MIDDLEWARE PIPELINE CONFIGURATION
// ==========================================

// Global Exception Handler must sit right at the top
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
    await DataSeeder.SeedAsync(context);
}

app.UseRouting();

app.UseCors("TmsClient");

app.UseRateLimiter();
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    
    // Updated CSP to allow external fonts and API calls needed by Scalar UI
    context.Response.Headers.Append(
        "Content-Security-Policy",
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; font-src 'self' https: data:; connect-src 'self' https://api.scalar.com;"
    );
    
    await next();
});
app.MapHealthChecks("/health/live").DisableRateLimiting();
app.MapHealthChecks("/health/ready").DisableRateLimiting();

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true || context.Request.Cookies.ContainsKey("tms_auth"))
    {
        var antiforgery = context.RequestServices
            .GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
        var tokens = antiforgery.GetAndStoreTokens(context);
        
        context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!,
            new CookieOptions
            {
                HttpOnly = false, // MUST be false so Angular JavaScript can read it!
                Secure = !builder.Environment.IsDevelopment(),
                SameSite = SameSiteMode.Strict
            });
    }
    await next(context);
});


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("TMS API")
               .WithOpenApiRoutePattern("/openapi/v1.json");
    });
}

app.UseMiddleware<V1DeprecationMiddleware>();

app.MapControllers();

app.MapHub<TmsHub>("/hubs/tms").RequireCors("TmsClient");
// ==========================================
// 3. MINIMAL ENDPOINTS
// ==========================================

app.MapGet("/api/error", () =>
{
    throw new TmsDatabaseException("Simulated database failure for ProblemDetails testing");
});

app.MapGet("/api/assessments/results", () => Results.Ok(new
{
    courseCode = "CS-101",
    studentId = "S-001",
    letterGrade = "A"
}))
.RequireAuthorization();

app.MapGet("/api/enrollments/worker-smoke", async (EnrollmentWorker worker) =>
{
    await worker.ProcessBatch();
    return Results.Ok("processed");
});

app.MapGet("/api/assessments/results1", (HttpContext context) =>
{
    return Results.Ok(new
    {
        User = context.User.Identity?.Name,
        IsAuthenticated = context.User.Identity?.IsAuthenticated,
        CourseCode = "CS-101",
        StudentId = "S-001",
        LetterGrade = "A"
    });
});

app.MapGet("/payment-options", () =>
{
    return Results.Ok(new[]
    {
        new { Method = "CreditCard", Description = "Pay securely with Visa/Mastercard" },
        new { Method = "PayPal", Description = "Use your PayPal account" },
        new { Method = "BankTransfer", Description = "Direct transfer from your bank" }
    });
});

app.MapGet("/api/students/paged", async (TmsDbContext db, int pageNumber = 1, CancellationToken cancellationToken = default) =>
{
    int pageSize = 20;
    if (pageNumber < 1) pageNumber = 1;

    var pagedStudents = await db.Students
        .AsNoTracking()
        .OrderBy(s => s.Id)
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);

    return Results.Ok(pagedStudents);
});

app.MapGet("/api/reports/nplusone-trap", async (TmsDbContext db, CancellationToken cancellationToken) =>
{
    var students = await db.Students.AsNoTracking().ToListAsync(cancellationToken);

    foreach (var s in students)
    {
        var count = await db.Enrollments
            .AsNoTracking()
            .CountAsync(e => e.StudentId == s.Id, cancellationToken);

        Console.WriteLine($"[N+1 Loop Log] {s.Name}: {count} enrollments");
    }

    return Results.Ok(new { Message = "Check your IDE terminal log to see the 1 + N SQL statements fired!" });
});

app.MapGet("/api/reports/nplusone-fixed", async (TmsDbContext db, CancellationToken cancellationToken) =>
{
    var report = await db.Students
        .AsNoTracking()
        .Select(s => new
        {
            s.Name,
            EnrollmentCount = s.Enrollments.Count
        })
        .ToListAsync(cancellationToken);

    foreach (var r in report)
    {
        Console.WriteLine($"[Fixed Log] {r.Name}: {r.EnrollmentCount} enrollments");
    }

    return Results.Ok(report);
});

app.MapPost("/api/students/test-concurrency", async (TmsDbContext db) =>
{
    var studentClerkA = await db.Students.FirstOrDefaultAsync();
    if (studentClerkA == null) return Results.NotFound("No students found to test with.");
    
    var studentClerkB = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentClerkA.Id);
    if (studentClerkB == null) return Results.NotFound("Clerk B record could not be loaded.");

    studentClerkA.Name = "Updated By Clerk A";
    await db.SaveChangesAsync();

    db.ChangeTracker.Clear();

    try
    {
        studentClerkB.Name = "Updated By Clerk B";
        studentClerkB.Version = 0; 

        db.Students.Update(studentClerkB); 
        await db.SaveChangesAsync();
        
        return Results.Ok("Save succeeded unexpectedly.");
    }
    catch (DbUpdateConcurrencyException ex)
    {
        Console.WriteLine($"[Concurrency Conflict Caught Successfully!] {ex.Message}");
        return Results.Conflict(new { Message = "Concurrency exception caught successfully! Your data is safe.", Error = ex.GetType().Name });
    }
});

app.MapPost("/api/enrollments/bulk-archive", async (TmsDbContext db) =>
{
    int affectedRows = await db.Enrollments
        .Where(e => e.Grade != null && e.Grade < 2.0m) 
        .ExecuteUpdateAsync(setter => setter.SetProperty(e => e.IsArchived, true));

    return Results.Ok(new { Message = "Bulk archival complete!", RowsAffected = affectedRows });
});

app.MapGet("/api/students/all-with-deleted", async (TmsDbContext db) =>
{
    var activeCount = await db.Students.CountAsync();
    var totalCountWithDeleted = await db.Students.IgnoreQueryFilters().CountAsync();

    return Results.Ok(new {
        ActiveStudentsCount = activeCount,
        TotalIncludingSoftDeleted = totalCountWithDeleted
    });
});

// Runtime Migrations & Data Seeding
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
    context.Database.Migrate();

    if (!context.Students.Any())
    {
        var students = new List<TmsApi.Domain.Entities.Student>
        {
            new() { RegistrationNumber = "TMS-2026-0001", Name = "Liya Kebede", GPA = 3.8m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0002", Name = "Bob Jones", GPA = 2.9m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0003", Name = "Charlie Brown", GPA = 3.4m, IsActive = false },
            new() { RegistrationNumber = "TMS-2026-0004", Name = "Diana Prince", GPA = 3.9m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0005", Name = "Evan Wright", GPA = 2.5m, IsActive = true }
        };
        context.Students.AddRange(students);

        var courses = new List<TmsApi.Domain.Entities.Course>
        {
            new() { Code = "CS-101", Title = "Introduction to Computer Science", MaxCapacity = 30 },
            new() { Code = "CS-201", Title = "Data Structures and Algorithms", MaxCapacity = 25 },
            new() { Code = "MAT-101", Title = "Calculus I", MaxCapacity = 40 }
        };
        context.Courses.AddRange(courses);
        context.SaveChanges();

        var enrollments = new List<TmsApi.Domain.Entities.Enrollment>
        {
            new() { StudentId = students[0].Id, CourseId = courses[0].Id, Grade = 4.0m },
            new() { StudentId = students[0].Id, CourseId = courses[1].Id, Grade = 3.6m },
            new() { StudentId = students[1].Id, CourseId = courses[0].Id, Grade = 2.8m },
            new() { StudentId = students[3].Id, CourseId = courses[1].Id, Grade = 3.9m }
        };
        context.Enrollments.AddRange(enrollments);
        context.SaveChanges();
    }
}

app.Run();