namespace JobFree.Api.Realtime;

/// <summary>
/// Các phương thức mở rộng (Extension Methods) để đăng ký và cấu hình tính năng WebSockets giao tiếp real-time.
/// </summary>
public static class WebSocketExtensions
{
    public static IServiceCollection AddJobFreeWebSockets(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WebSocketOptions>().Bind(configuration.GetSection("WebSockets"));
        return services;
    }

    public static WebApplication UseJobFreeWebSockets(this WebApplication app)
    {
        // SKELETON: no upgrade endpoint until path, authentication and message protocol are approved.
        app.UseWebSockets();
        return app;
    }
}
