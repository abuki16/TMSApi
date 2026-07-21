using Asp.Versioning;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Scalar.AspNetCore;
using TmsApi.Api.Filters;
using TmsApi.Api.Middlewares;
using TmsApi.Api.Options;
using TmsApi.Api.Worker;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. REGISTER SERVICES
// ==========================================

builder.Services.AddControllers(options =>
{
    options.Filters.Add<AuditLogFilter>();
});

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

// Authentication and Security
builder.Services.AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);

builder.Services.AddAuthorization();

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

Console.WriteLine("Payments:GatewayUrl = " + builder.Configuration["Payments:GatewayUrl"]);
Console.WriteLine("Payments:MaxDepositBirr = " + builder.Configuration["Payments:MaxDepositBirr"]);

var app = builder.Build();

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

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseMiddleware<V1DeprecationMiddleware>();

app.MapControllers();

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
            new() { RegistrationNumber = "TMS-2026-0001", Name = "Alice Smith", GPA = 3.8m, IsActive = true },
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