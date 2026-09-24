using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JobFree.Api.Middleware;

/// <summary>
/// Bộ xử lý ngoại lệ toàn cục (Global Exception Handler) chuẩn hóa mọi lỗi unhandled thành định dạng RFC 7807 Problem Details.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (httpContext.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        ProblemDetails problem = exception switch
        {
            ValidationException validation => new ValidationProblemDetails(validation.Errors
                .GroupBy(failure => failure.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred."
            },
            BadHttpRequestException badRequest => new ProblemDetails
            {
                Status = badRequest.StatusCode,
                Title = "The request could not be processed."
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred."
            }
        };

        var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        problem.Extensions["traceId"] = traceId;
        httpContext.Response.StatusCode = problem.Status!.Value;

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            // Log identifiers only; exception messages may contain credentials or personal data.
            logger.LogError("Unhandled exception {ExceptionType}; trace {TraceId}", exception.GetType().Name, traceId);
        }

        if (!await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem
        }))
        {
            await httpContext.Response.WriteAsJsonAsync(problem, options: null,
                contentType: "application/problem+json", cancellationToken: cancellationToken);
        }

        return true;
    }
}
