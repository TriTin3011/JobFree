using FluentValidation;
using FluentValidation.Results;

namespace JobFree.Application.Common.Validation;

/// <summary>
/// Quản lý việc thực thi tuần tự tất cả các <see cref="IValidator{T}"/> đã đăng ký cho một request DTO.
/// </summary>
/// <typeparam name="T">Kiểu request DTO cần kiểm tra tính hợp lệ.</typeparam>
public sealed class RequestValidation<T>(IEnumerable<IValidator<T>> validators) where T : class
{
    public async Task ValidateAsync(T request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var failures = new List<ValidationFailure>();

        // Sequential execution allows validators to share scoped dependencies safely.
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }
}
