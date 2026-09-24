using JobFree.Api;
using JobFree.Api.Realtime;
using JobFree.Application;
using JobFree.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
// The global handler logs a sanitized record instead of the middleware's raw exception.
builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None);

var s3ServiceUrl = builder.Configuration["S3:ServiceUrl"];
if (!builder.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(s3ServiceUrl))
{
    throw new InvalidOperationException("An S3-compatible local endpoint is only allowed in Development.");
}

var rabbitMqAddressStr = builder.Configuration["RabbitMq:Address"];
var rabbitMqUri = string.IsNullOrWhiteSpace(rabbitMqAddressStr)
    ? new Uri("rabbitmq://127.0.0.1/")
    : new Uri(rabbitMqAddressStr);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Database"))
        ? "Host=127.0.0.1;Port=15432;Database=jobfree;Username=postgres;Password=postgres"
        : builder.Configuration.GetConnectionString("Database")!,
    string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Redis"))
        ? "127.0.0.1:6379"
        : builder.Configuration.GetConnectionString("Redis")!,
    rabbitMqUri,
    string.IsNullOrWhiteSpace(builder.Configuration["RabbitMq:Username"])
        ? "guest"
        : builder.Configuration["RabbitMq:Username"]!,
    string.IsNullOrWhiteSpace(builder.Configuration["RabbitMq:Password"])
        ? "guest"
        : builder.Configuration["RabbitMq:Password"]!,
    string.IsNullOrWhiteSpace(builder.Configuration["S3:Region"])
        ? "us-east-1"
        : builder.Configuration["S3:Region"]!,
    string.IsNullOrWhiteSpace(s3ServiceUrl) ? null : new Uri(s3ServiceUrl),
    builder.Configuration["S3:HealthCheckBucket"]);
builder.Services.AddPresentation(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
if (builder.Configuration.GetValue<bool>("Http:RedirectToHttps"))
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseJobFreeWebSockets();

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "JobFree API v1");
    });
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    ResultStatusCodes =
    {
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable
    },
    ResponseWriter = (context, report) => context.Response.WriteAsJsonAsync(new
    {
        status = report.Status.ToString(),
        checks = report.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString())
    }, context.RequestAborted)
}).AllowAnonymous();

if (builder.Configuration.GetValue<bool>("Telemetry:Prometheus:Enabled"))
{
    app.MapPrometheusScrapingEndpoint("/metrics").AllowAnonymous();
}

app.Run();

/// <summary>
/// Điểm khởi chạy chính (Entry Point) của ứng dụng Web API JobFree.
/// Xuất bản class partial để phục vụ Integration Testing với WebApplicationFactory.
/// </summary>
public partial class Program;
