using System.Diagnostics;
using JobFree.Api.Extensions;
using JobFree.Api.Filters;
using JobFree.Api.Middleware;
using JobFree.Api.Realtime;

namespace JobFree.Api;

/// <summary>
/// Đăng ký các dịch vụ thuộc Presentation layer (API Core, Health Checks, Exception Handling, CORS, WebSockets, Telemetry).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString()
                ?? context.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddScoped(typeof(ValidationFilter<>));
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "JobFree API",
                Version = "v1",
                Description = "API Documentation cho hệ thống nền tảng JobFree"
            });
        });

        // SKELETON: configure a concrete scheme and policies when the identity design is approved.
        services.AddAuthentication();
        services.AddAuthorization();
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (origins.Length > 0)
            {
                policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
            }
        }));
        services.AddJobFreeWebSockets(configuration);
        services.AddJobFreeTelemetry(configuration);
        return services;
    }
}
