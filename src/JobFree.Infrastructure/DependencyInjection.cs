using Amazon;
using Amazon.S3;
using JobFree.Infrastructure.Health;
using JobFree.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace JobFree.Infrastructure;

/// <summary>
/// Đăng ký các dịch vụ thuộc Infrastructure layer (PostgreSQL / EF Core, Redis Cache, RabbitMQ via MassTransit, AWS S3 Client, Health Checks).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string databaseConnectionString,
        string redisConnectionString,
        Uri rabbitMqAddress,
        string rabbitMqUsername,
        string rabbitMqPassword,
        string s3Region,
        Uri? s3ServiceUrl = null,
        string? s3HealthCheckBucket = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseConnectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(redisConnectionString);
        ArgumentNullException.ThrowIfNull(rabbitMqAddress);
        if (rabbitMqAddress.Scheme is not ("rabbitmq" or "rabbitmqs") ||
            !string.IsNullOrEmpty(rabbitMqAddress.UserInfo))
        {
            throw new ArgumentException("Use a rabbitmq(s) address without embedded credentials.", nameof(rabbitMqAddress));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(rabbitMqUsername);
        ArgumentException.ThrowIfNullOrWhiteSpace(rabbitMqPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(s3Region);

        services.AddDbContext<JobFreeDbContext>(options =>
            options.UseNpgsql(databaseConnectionString, postgres => postgres.UseNetTopologySuite()));

        var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
        redisOptions.AbortOnConnectFail = false;
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));
        services.AddStackExchangeRedisCache(_ => { });
        services.AddOptions<RedisCacheOptions>().Configure<IConnectionMultiplexer>((options, connection) =>
            options.ConnectionMultiplexerFactory = () => Task.FromResult(connection));
        services.AddHealthChecks().AddCheck<DependencyHealthCheck>(
            "database-cache", tags: ["ready"], timeout: TimeSpan.FromSeconds(5));

        services.AddMassTransit(registration =>
        {
            registration.UsingRabbitMq((_, bus) =>
            {
                bus.Host(rabbitMqAddress, host =>
                {
                    host.Username(rabbitMqUsername);
                    host.Password(rabbitMqPassword);
                });
                // SKELETON: no business consumers, message topology or reliability policies yet.
            });
        });

        services.AddSingleton<IAmazonS3>(_ =>
        {
            var config = new AmazonS3Config();
            if (s3ServiceUrl is null)
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(s3Region);
            }
            else
            {
                config.ServiceURL = s3ServiceUrl.AbsoluteUri;
                config.AuthenticationRegion = s3Region;
                config.ForcePathStyle = true;
            }

            // Credentials come from the SDK chain: environment locally, task role on ECS.
            return new AmazonS3Client(config);
        });
        services.AddHealthChecks().Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
            "storage",
            provider => new S3HealthCheck(provider.GetRequiredService<IAmazonS3>(), s3HealthCheckBucket),
            failureStatus: null,
            tags: ["ready"],
            timeout: TimeSpan.FromSeconds(5)));
        return services;
    }
}
