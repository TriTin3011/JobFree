using FluentValidation;
using JobFree.Application.Common.Validation;
using Xunit;

namespace JobFree.Application.Tests;

/// <summary>
/// Các Unit Test cho bộ kiểm tra tính hợp lệ request (RequestValidation pipeline).
/// </summary>
public sealed class RequestValidationTests
{
    [Fact]
    public async Task Valid_request_passes_async_validators()
    {
        var invoked = false;
        var validator = new InlineValidator<Input>();
        validator.RuleFor(input => input.Value).MustAsync((_, token) =>
        {
            invoked = true;
            return Task.FromResult(!token.IsCancellationRequested);
        });

        await new RequestValidation<Input>([validator]).ValidateAsync(new Input("valid"));

        Assert.True(invoked);
    }

    [Fact]
    public async Task Failures_from_all_validators_are_preserved()
    {
        var first = new InlineValidator<Input>();
        first.RuleFor(input => input.Value).NotEmpty();
        var second = new InlineValidator<Input>();
        second.RuleFor(input => input.Value).MinimumLength(3);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            new RequestValidation<Input>([first, second]).ValidateAsync(new Input("")));

        Assert.Equal(2, exception.Errors.Count());
    }

    [Fact]
    public async Task Cancellation_is_forwarded_to_async_validator()
    {
        using var cancellation = new CancellationTokenSource();
        var validator = new InlineValidator<Input>();
        validator.RuleFor(input => input.Value).MustAsync((_, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new RequestValidation<Input>([validator]).ValidateAsync(new Input("valid"), cancellation.Token));
    }

    [Fact]
    public async Task Already_cancelled_request_does_not_pass_without_validators()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new RequestValidation<Input>([]).ValidateAsync(new Input("valid"), new CancellationToken(true)));
    }

    [Fact]
    public async Task Null_input_is_rejected()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => new RequestValidation<Input>([]).ValidateAsync(null!));
    }

    private sealed record Input(string Value);
}
