// using MediatR;
// using TmsApi.Infrastructure.Persistence;
// namespace TmsApi.Application.Courses.SearchCourses;

// // The Query object
// public record SearchCoursesQuery(string? Term) : IRequest<IEnumerable<object>>;

// // The Handler that executes the query using your DbContext
// public class SearchCoursesQueryHandler : IRequestHandler<SearchCoursesQuery, IEnumerable<object>>
// {
//     private readonly TmsDbContext _context;

//     public SearchCoursesQueryHandler(TmsDbContext context)
//     {
//         _context = context;
//     }

//     public async Task<IEnumerable<object>> Handle(SearchCoursesQuery request, CancellationToken ct)
//     {
//         var baseQuery = _context.Courses.AsNoTracking();

//         if (!string.IsNullOrWhiteSpace(request.Term))
//         {
//             baseQuery = baseQuery.Where(c => c.Title.Contains(request.Term) || c.Code.Contains(request.Term));
//         }

//         return await baseQuery
//             .Select(c => new
//             {
//                 c.Id,
//                 c.Code,
//                 c.Title,
//                 c.MaxCapacity,
//                 EnrollmentCount = c.Enrollments.Count
//             })
//             .ToListAsync(ct);
//     }
// }