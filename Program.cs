using TmsApi.Services;
//using TmsApi.Configuration;
using Microsoft.AspNetCore.Authentication;
using Scalar.AspNetCore;
using TmsApi.Worker;
using TmsApi.Options;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Entities;
var builder = WebApplication.CreateBuilder(args);


// 1. REGISTER SERVICES
builder.Services.AddControllers();

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

// If EnrollmentWorker is a BackgroundService, leave it as Singleton or change it to Transient
builder.Services.AddTransient<EnrollmentWorker>();


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


// Exercise 7: 
if (app.Environment.IsDevelopment())
{
    // OpenAPI document
    app.MapOpenApi();

    // Interactive API explorer
    app.MapScalarApiReference();
}
else
{
    // Production: don't expose stack traces
    app.UseExceptionHandler();
}

app.UseStatusCodePages();
app.UseMiddleware<RequestLoggingMiddleware>();

// 2. CONFIGURE PIPELINE MIDDLEWARE (ORDER MATTERS)

app.UseHttpsRedirection();

// Step 1: Routing must happen first so the app matches the URL to an endpoint
app.UseRouting();

// Step 2: Authentication checks WHO you are (processes our TrainingAuthHandler)
app.UseAuthentication();

// Step 3: Authorization checks IF you have permission to access the matched route
app.UseAuthorization();

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

app.MapControllers();

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
            new() { Code = "CS-101", Title = "Introduction to Computer Science", Capacity = 30 },
            new() { Code = "CS-201", Title = "Data Structures and Algorithms", Capacity = 25 },
            new() { Code = "MAT-101", Title = "Calculus I", Capacity = 40 },
            new() { Code = "c#-01",Title = "C# Fundamental",Capacity =30}
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