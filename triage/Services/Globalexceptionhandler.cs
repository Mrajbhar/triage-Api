using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace triage.Services
{
   
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly IProblemDetailsService _problemDetails;
        private readonly ILogger<GlobalExceptionHandler> _log;

        public GlobalExceptionHandler(IProblemDetailsService problemDetails,
                                      ILogger<GlobalExceptionHandler> log)
        {
            _problemDetails = problemDetails;
            _log = log;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
        {
            var (status, title) = Map(ex);
            if (status is null) return false;  

            _log.LogWarning(ex, "Mapped {Exception} to {Status}.", ex.GetType().Name, status);

            ctx.Response.StatusCode = status.Value;
            return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = ctx,
                Exception = ex,
                ProblemDetails = new ProblemDetails { Status = status, Title = title },
            });
        }

      
        private static (int? status, string? title) Map(Exception ex) => ex switch
        {
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "The record was modified by someone else. Reload and try again."),

            DbUpdateException { InnerException: SqlException sql } => sql.Number switch
            {
                2627 or 2601 => (StatusCodes.Status409Conflict, "That record already exists."),
                547 => (StatusCodes.Status400BadRequest, "The request referenced a missing or invalid related record."),
                _ => ((int?)null, (string?)null),
            },

            _ => ((int?)null, (string?)null),
        };
    }
}