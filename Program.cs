using TmsApi.Services;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Scalar.AspNetCore;
using TmsApi.Worker;
using TmsApi.Options;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Entities;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Filters;

  

var builder = WebApplication.CreateBuilder(args);


// 1. REGISTER SERVICES
builder.Services.AddControllers(options =>
{
options.Filters.Add<AuditLogFilter>();
});

//API
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    
    // Combine both URL-segment and Header-based version reading
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

// Exercise 6: Register the ProblemDetails service framework
builder.Services.AddProblemDetails(); 

builder.Services.AddOpenApi();

// Keep this service Scoped to avoid ValidateScopes startup exceptions

//builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
//builder.Services.AddSingleton<EnrollmentWorker>();

// Change to Singleton so controllers and background worker share the exact same in-memory dictionaries
// =======================================================
// CHANGE THESE LIFETIMES FROM SINGLETON TO SCOPED/TRANSIENT
// =======================================================
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IAssessmentService, AssessmentService>();
builder.Services.AddScoped<IAssessmentResultService, AssessmentResultService>();
// If EnrollmentWorker is a BackgroundService, leave it as Singleton or change it to Transient
builder.Services.AddSingleton<EnrollmentWorker>();


// Register TmsDbContext scoped for incoming HTTP requests
builder.Services.AddDbContext<TmsDbContext>(options =>
options.UseNpgsql(builder.Configuration.GetConnectionString("TmsDatabase")));

// Register our training scheme mock services
builder.Services
    .AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddAuthorization();

// Strongly typed PaymentOptions with validation
builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

Console.WriteLine("Payments:GatewayUrl = " +
                  builder.Configuration["Payments:GatewayUrl"]);

Console.WriteLine("Payments:MaxDepositBirr = " +
                  builder.Configuration["Payments:MaxDepositBirr"]);


// // Exercise 6: Middleware configuration (Must be at the very top of the pipeline)
// app.UseExceptionHandler(); 
// app.UseStatusCodePages(); 

// app.UseMiddleware<RequestLoggingMiddleware>();

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();

// 1. GLOBAL ERROR HANDLING (Runs in both Development and Production)
app.UseExceptionHandler();
app.UseStatusCodePages();

// 2. CONFIGURE PIPELINE MIDDLEWARE (ORDER MATTERS)

app.UseHttpsRedirection();



if (app.Environment.IsDevelopment())
{
using var scope = app.Services.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
await DataSeeder.SeedAsync(context);
}


// Step 1: Routing must happen first so the app matches the URL to an endpoint
app.UseRouting();

// Step 2: Authentication checks WHO you are (processes our TrainingAuthHandler)
app.UseAuthentication();

// Step 3: Authorization checks IF you have permission to access the matched route
app.UseAuthorization();


// Exercise 7: 
if (app.Environment.IsDevelopment())
{
    // OpenAPI document
    app.MapOpenApi();
    // Interactive API explorer
    app.MapScalarApiReference();
}
//m7
app.UseMiddleware<TmsApi.Api.Middleware.V1DeprecationMiddleware>();

app.MapControllers();

// Exercise 6: Test error route that intentionally throws a database exception
app.MapGet("/api/error", () =>
{
    throw new TmsDatabaseException("Simulated database failure for ProblemDetails testing");
});

// Step 4: Map the endpoint and explicitly lock it down
app.MapGet("/api/assessments/results", () => Results.Ok(new
{
    courseCode = "CS-101",
    studentId = "S-001",
    letterGrade = "A"
}))
.RequireAuthorization(); // <-- This forces it to participate in security

app.MapGet("/api/enrollments/worker-smoke",
    async (EnrollmentWorker worker) =>
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
// Exercise 3: Test Paginated Students Roster
app.MapGet("/api/students/paged", async (TmsDbContext db, int pageNumber = 1, CancellationToken cancellationToken = default) =>
{
    // Strict pagination window constraints as expressed in the module
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

// Exercise 7 B  Intentional N+1 Problem for Learning
app.MapGet("/api/reports/nplusone-trap", async (TmsDbContext db, CancellationToken cancellationToken) =>
{
    // 1. Fires 1 initial query to get all active, non-deleted students
    var students = await db.Students.AsNoTracking().ToListAsync(cancellationToken);

    foreach (var s in students)
    {
        // 2. Fires an extra query back to PostgreSQL for EACH individual student in the loop
        var count = await db.Enrollments
            .AsNoTracking()
            .CountAsync(e => e.StudentId == s.Id, cancellationToken);

        Console.WriteLine($"[N+1 Loop Log] {s.Name}: {count} enrollments");
    }

    return Results.Ok(new { Message = "Check your IDE terminal log to see the 1 + N SQL statements fired!" });
});

// Exercise 7B: Fixed with a Single Shaped Query
app.MapGet("/api/reports/nplusone-fixed", async (TmsDbContext db, CancellationToken cancellationToken) =>
{
    // Fix: Single query with projection using a SQL subquery join
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

// Exercise 8: Simulating a Concurrency Conflict (GUARANTEED CONFLICT EXCEPTION)
app.MapPost("/api/students/test-concurrency", async (TmsDbContext db) =>
{
    // 1. Load the student records
    var studentClerkA = await db.Students.FirstOrDefaultAsync();
    if (studentClerkA == null) return Results.NotFound("No students found to test with.");
    
    var studentClerkB = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentClerkA.Id);
    if (studentClerkB == null) return Results.NotFound("Clerk B record could not be loaded.");

    // 2. Clerk A updates the student's name and saves successfully
    studentClerkA.Name = "Updated By Clerk A";
    await db.SaveChangesAsync(); // This increments the real xmin version token in PostgreSQL!

    // Clear the tracker so EF Core forgets about Clerk A's instance in memory
    db.ChangeTracker.Clear();

    // 3. Clerk B tries to update using their old state
    try
    {
        studentClerkB.Name = "Updated By Clerk B";
        
        // 🔥 FORCE DISCREPANCY: Manually set an obsolete Version token so PostgreSQL's xmin check fails immediately
        studentClerkB.Version = 0; 

        db.Students.Update(studentClerkB); 
        await db.SaveChangesAsync(); // This will now definitively throw the DbUpdateConcurrencyException!
        
        return Results.Ok("Save succeeded unexpectedly.");
    }
    catch (DbUpdateConcurrencyException ex)
    {
        Console.WriteLine($"[Concurrency Conflict Caught Successfully!] {ex.Message}");
        return Results.Conflict(new { Message = "Concurrency exception caught successfully! Your data is safe.", Error = ex.GetType().Name });
    }
});

// Exercise 9A: High-Performance Bulk Archive via ExecuteUpdateAsync
app.MapPost("/api/enrollments/bulk-archive", async (TmsDbContext db) =>
{
    // Ex 9A High performance: Generates a single SQL UPDATE statement directly on the database server
    int affectedRows = await db.Enrollments
        .Where(e => e.Grade != null && e.Grade < 2.0m) 
        .ExecuteUpdateAsync(setter => setter.SetProperty(e => e.IsArchived, true));

    return Results.Ok(new { Message = "Bulk archival complete!", RowsAffected = affectedRows });
});

// Exercise 9B: Demonstration of Bypassing Global Filters with IgnoreQueryFilters()
app.MapGet("/api/students/all-with-deleted", async (TmsDbContext db) =>
{
    // Normal query (hides soft-deleted students)
    var activeCount = await db.Students.CountAsync();

    // Overridden query (bypasses HasQueryFilter to retrieve everything)
    var totalCountWithDeleted = await db.Students
        .IgnoreQueryFilters()
        .CountAsync();

    return Results.Ok(new {
        ActiveStudentsCount = activeCount,
        TotalIncludingSoftDeleted = totalCountWithDeleted
    });
});


// Seed test data at startup
// Seed test data at startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
    context.Database.Migrate(); // Applies any pending PostgreSQL migrations automatically

    if (!context.Students.Any())
    {
        // 1. Seed Students using explicit Database Entities matching your StudentService tracking
        var students = new List<TmsApi.Entities.Student>
        {
            new() { RegistrationNumber = "TMS-2026-0001", Name = "Alice Smith", GPA = 3.8m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0002", Name = "Bob Jones", GPA = 2.9m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0003", Name = "Charlie Brown", GPA = 3.4m, IsActive = false },
            new() { RegistrationNumber = "TMS-2026-0004", Name = "Diana Prince", GPA = 3.9m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0005", Name = "Evan Wright", GPA = 2.5m, IsActive = true }
        };
        context.Students.AddRange(students);

        // 2. Seed Courses using explicit Database Entities matching your CourseService tracking
        var courses = new List<TmsApi.Entities.Course>
        {
            new() { Code = "CS-101", Title = "Introduction to Computer Science", MaxCapacity = 30 },
            new() { Code = "CS-201", Title = "Data Structures and Algorithms", MaxCapacity = 25 },
            new() { Code = "MAT-101", Title = "Calculus I", MaxCapacity = 40 }
        };
        context.Courses.AddRange(courses);
        context.SaveChanges(); // Persist to database so PostgreSQL populates primary key IDs

        // 3. Seed Enrollments using explicit Database Entities matching your Enrollment entity schema
        var enrollments = new List<TmsApi.Entities.Enrollment>
        {
            new() { StudentId = students[0].Id, CourseId = courses[0].Id, Grade = 4.0m },
            new() { StudentId = students[0].Id, CourseId = courses[1].Id, Grade = 3.6m },
            new() { StudentId = students[1].Id, CourseId = courses[0].Id, Grade = 2.8m },
            new() { StudentId = students[3].Id, CourseId = courses[1].Id, Grade = 3.9m }
        };
        context.Enrollments.AddRange(enrollments);
        context.SaveChanges(); // Saves relationships safely to PostgreSQL
    }
}

app.Run();