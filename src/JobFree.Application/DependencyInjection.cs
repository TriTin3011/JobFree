using FluentValidation;
using JobFree.Application.Common.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace JobFree.Application;

/// <summary>
/// Đăng ký các dịch vụ thuộc Application layer (FluentValidation validators, Request Validation pipeline).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // SKELETON: feature slices belong under Features/<Feature>; no business use cases yet.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, ServiceLifetime.Scoped);
        services.AddScoped(typeof(RequestValidation<>));
        return services;
    }
}
