using JobFree.Application.Common.Validation;

namespace JobFree.Api.Filters;

/// <summary>
/// Endpoint Filter tự động thực hiện validate request object trước khi chạy handler chính trong Minimal API.
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu DTO/Request cần validate.</typeparam>
public sealed class ValidationFilter<T>(RequestValidation<T> validation) : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<T>().SingleOrDefault()
            ?? throw new BadHttpRequestException("A request body is required.");

        await validation.ValidateAsync(request, context.HttpContext.RequestAborted);
        return await next(context);
    }
}
