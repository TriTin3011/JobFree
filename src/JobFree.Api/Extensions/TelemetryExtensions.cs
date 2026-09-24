using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace JobFree.Api.Extensions;

/// <summary>
/// Các phương thức mở rộng (Extension Methods) cấu hình OpenTelemetry (Tracing, Metrics, Prometheus, OTLP Exporter).
/// </summary>
public static class TelemetryExtensions
{
    public static IServiceCollection AddJobFreeTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var telemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("JobFree.Api"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = false;
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health")
                        && !context.Request.Path.StartsWithSegments("/metrics");
                })
                .AddHttpClientInstrumentation(options => options.RecordException = false))
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (configuration.GetValue<bool>("Telemetry:Prometheus:Enabled"))
                {
                    metrics.AddPrometheusExporter();
                }
            });

        // Export traces/metrics through OTLP only when a collector has been configured.
        if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }

        return services;
    }
}
