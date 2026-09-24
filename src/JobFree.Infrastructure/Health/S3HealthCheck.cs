using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace JobFree.Infrastructure.Health;

/// <summary>
/// Kiểm tra kết nối và tính sẵn sàng của dịch vụ lưu trữ đối tượng S3/MinIO (Object Storage).
/// </summary>
public sealed class S3HealthCheck(IAmazonS3 storage, string? bucketName = null) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(bucketName))
            {
                // Local bootstrap can verify credentials/connectivity without creating a bucket.
                await storage.ListBucketsAsync(new ListBucketsRequest(), cancellationToken);
            }
            else
            {
                await storage.GetBucketLocationAsync(new GetBucketLocationRequest
                {
                    BucketName = bucketName
                }, cancellationToken);
            }

            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("Object storage is unavailable.");
        }
    }
}
