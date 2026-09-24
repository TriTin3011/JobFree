using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using JobFree.Infrastructure.Health;
using JobFree.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using Xunit;

namespace JobFree.Infrastructure.Tests;

/// <summary>
/// Các Unit/Integration Test kiểm tra việc đăng ký và khởi tạo dịch vụ hạ tầng (DbContext, Redis, RabbitMQ, S3 Health Check).
/// </summary>
public sealed class ProviderRegistrationTests
{
    [Fact]
    public void Context_uses_postgresql_and_postgis_without_business_entities()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<JobFreeDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        var model = context.GetService<IDesignTimeModel>().Model;
        Assert.Empty(model.GetEntityTypes());
        Assert.Contains(model.GetPostgresExtensions(), extension => extension.Name == "postgis");
    }

    [Fact]
    public void Context_is_scoped_and_provider_clients_are_singletons()
    {
        var services = CreateServices();
        Assert.Equal(ServiceLifetime.Singleton, Assert.Single(services, item => item.ServiceType == typeof(IAmazonS3)).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, Assert.Single(services, item => item.ServiceType == typeof(IConnectionMultiplexer)).Lifetime);

        using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var context = first.ServiceProvider.GetRequiredService<JobFreeDbContext>();
        Assert.Same(context, first.ServiceProvider.GetRequiredService<JobFreeDbContext>());
        Assert.NotSame(context, second.ServiceProvider.GetRequiredService<JobFreeDbContext>());
    }

    [Fact]
    public void Missing_connection_configuration_fails_before_network_access()
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddInfrastructure(
            "", "localhost:6379", new Uri("rabbitmq://localhost/"), "test", "test", "us-east-1"));
    }

    [Fact]
    public void Broker_uri_cannot_embed_credentials()
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddInfrastructure(
            "Host=localhost;Database=test", "localhost:6379", new Uri("rabbitmq://user:password@localhost/"),
            "test", "test", "us-east-1"));
    }

    [Fact]
    public async Task Storage_probe_does_not_require_creating_a_bucket()
    {
        using var client = new StorageStub();
        var result = await new S3HealthCheck(client).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.True(client.ListedBuckets);
        Assert.Null(client.RequestedBucket);
    }

    [Fact]
    public async Task Configured_bucket_probe_avoids_listing_all_buckets()
    {
        using var client = new StorageStub();
        var result = await new S3HealthCheck(client, "configured-test-bucket").CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.False(client.ListedBuckets);
        Assert.Equal("configured-test-bucket", client.RequestedBucket);
    }

    [Fact]
    public async Task Storage_failure_does_not_expose_provider_exception()
    {
        using var client = new StorageStub { Fail = true };
        var result = await new S3HealthCheck(client).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Null(result.Exception);
        Assert.DoesNotContain("secret", result.Description!);
    }

    private static IServiceCollection CreateServices() => new ServiceCollection()
        .AddLogging()
        .AddInfrastructure("Host=localhost;Database=test", "localhost:6379",
            new Uri("rabbitmq://localhost/"), "test", "test", "us-east-1");

    private sealed class StorageStub() : AmazonS3Client(new BasicAWSCredentials("test", "test"),
        new AmazonS3Config { ServiceURL = "http://localhost:9000", ForcePathStyle = true })
    {
        public bool ListedBuckets { get; private set; }
        public string? RequestedBucket { get; private set; }
        public bool Fail { get; init; }

        public override Task<ListBucketsResponse> ListBucketsAsync(ListBucketsRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ListedBuckets = true;
            return Fail
                ? Task.FromException<ListBucketsResponse>(new AmazonS3Exception("secret provider detail"))
                : Task.FromResult(new ListBucketsResponse());
        }

        public override Task<GetBucketLocationResponse> GetBucketLocationAsync(GetBucketLocationRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedBucket = request.BucketName;
            return Task.FromResult(new GetBucketLocationResponse());
        }
    }
}
