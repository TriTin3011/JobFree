using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentValidation;
using JobFree.Api.Filters;
using JobFree.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace JobFree.Api.Tests;

/// <summary>
/// Các Integration Test kiểm tra tính vận hành của API (Health Checks /live, /ready, Metrics, Validation Filter, Global Exception Handler).
/// </summary>
public sealed class OperationalEndpointTests
{
    [Fact]
    public async Task Liveness_succeeds_when_dependencies_are_unavailable()
    {
        using var factory = new ApiFactory(HealthStatus.Unhealthy);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        var readiness = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
        Assert.DoesNotContain("secret", await readiness.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Readiness_succeeds_when_dependencies_are_healthy()
    {
        using var factory = new ApiFactory(HealthStatus.Healthy);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Metrics_are_available_when_explicitly_enabled()
    {
        using var factory = new ApiFactory(HealthStatus.Healthy);
        using var client = factory.CreateClient();
        await client.GetAsync("/health/live");
        var metrics = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, metrics.StatusCode);
        Assert.Contains("# TYPE", await metrics.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details()
    {
        using var factory = new ApiFactory(HealthStatus.Healthy);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/not-a-route");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Invalid_input_is_rejected_before_handler_runs()
    {
        await using var app = await CreateEdgeTestAppAsync();
        using var client = app.GetTestClient();
        var response = await client.PostAsJsonAsync("/test/input", new Input(""));
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(problem.GetProperty("errors").TryGetProperty("Value", out _));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Valid_input_reaches_handler()
    {
        await using var app = await CreateEdgeTestAppAsync();
        using var client = app.GetTestClient();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/test/input", new Input("valid"))).StatusCode);
    }

    [Fact]
    public async Task Malformed_json_uses_the_same_error_envelope()
    {
        await using var app = await CreateEdgeTestAppAsync();
        using var client = app.GetTestClient();
        using var body = new StringContent("{", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/test/input", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("JsonException", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/plain")]
    public async Task Unexpected_errors_do_not_expose_details(string accept)
    {
        await using var app = await CreateEdgeTestAppAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        var response = await client.GetAsync("/test/error");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("secret", body);
        Assert.Contains("traceId", body);
    }

    private static async Task<WebApplication> CreateEdgeTestAppAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddApplication();
        builder.Services.AddPresentation(builder.Configuration);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        var validator = new InlineValidator<Input>();
        validator.RuleFor(input => input.Value).NotEmpty();
        builder.Services.AddSingleton<IValidator<Input>>(validator);
        var app = builder.Build();
        app.UseExceptionHandler();
        // These routes exist only in the test host, never in the production API.
        app.MapPost("/test/input", (Input input) => Results.NoContent()).AddEndpointFilter<ValidationFilter<Input>>();
        app.MapGet("/test/error", (Func<IResult>)(() => throw new InvalidOperationException("secret internal detail")));
        await app.StartAsync();
        return app;
    }

    public sealed record Input(string Value);

    private sealed class ApiFactory(HealthStatus dependencyStatus) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Database", "Host=localhost;Database=test");
            builder.UseSetting("ConnectionStrings:Redis", "localhost:6379");
            builder.UseSetting("RabbitMq:Address", "rabbitmq://localhost/");
            builder.UseSetting("RabbitMq:Username", "test");
            builder.UseSetting("RabbitMq:Password", "test");
            builder.UseSetting("S3:Region", "us-east-1");
            builder.UseSetting("Telemetry:Prometheus:Enabled", "true");
            builder.ConfigureTestServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                var busServices = services.Where(service => service.ServiceType == typeof(IHostedService)
                    && service.ImplementationType?.Name == "MassTransitHostedService").ToArray();
                Assert.NotEmpty(busServices);
                foreach (var service in busServices)
                {
                    services.Remove(service);
                }

                services.Configure<HealthCheckServiceOptions>(options =>
                {
                    options.Registrations.Clear();
                    options.Registrations.Add(new HealthCheckRegistration("test-dependency",
                        _ => new DependencyStub(dependencyStatus), null, null));
                });
            });
        }
    }

    private sealed class DependencyStub(HealthStatus status) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(new HealthCheckResult(status, "secret provider detail"));
    }
}
