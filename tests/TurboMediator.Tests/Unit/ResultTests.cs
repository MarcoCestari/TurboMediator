using FluentAssertions;
using TurboMediator.Results;
using Xunit;

namespace TurboMediator.Tests;

/// <summary>
/// Tests for the Result pattern types.
/// </summary>
public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = Result.Success(42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_WithException_ShouldCreateFailedResult()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");

        // Act
        var result = Result.Failure<int>(exception);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(exception);
    }

    [Fact]
    public void Failure_WithMessage_ShouldCreateFailedResult()
    {
        // Act
        var result = Result.Failure<int>("Something went wrong");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ResultException>();
        result.Error.Message.Should().Be("Something went wrong");
    }

    [Fact]
    public void Value_OnFailure_ShouldThrowException()
    {
        // Arrange
        var result = Result.Failure<int>("Error");

        // Act & Assert
        var action = () => { var _ = result.Value; };
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Error_OnSuccess_ShouldThrowException()
    {
        // Arrange
        var result = Result.Success(42);

        // Act & Assert
        var action = () => { var _ = result.Error; };
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Match_OnSuccess_ShouldCallOnSuccessFunction()
    {
        // Arrange
        var result = Result.Success(42);

        // Act
        var output = result.Match(
            onSuccess: value => $"Value: {value}",
            onFailure: error => $"Error: {error.Message}");

        // Assert
        output.Should().Be("Value: 42");
    }

    [Fact]
    public void Match_OnFailure_ShouldCallOnFailureFunction()
    {
        // Arrange
        var result = Result.Failure<int>("Test error");

        // Act
        var output = result.Match(
            onSuccess: value => $"Value: {value}",
            onFailure: error => $"Error: {error.Message}");

        // Assert
        output.Should().Be("Error: Test error");
    }

    [Fact]
    public void Map_OnSuccess_ShouldTransformValue()
    {
        // Arrange
        var result = Result.Success(5);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(10);
    }

    [Fact]
    public void Map_OnFailure_ShouldPropagateError()
    {
        // Arrange
        var result = Result.Failure<int>("Error");

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        mapped.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Bind_OnSuccess_ShouldChainResults()
    {
        // Arrange
        var result = Result.Success(10);

        // Act
        var bound = result.Bind(x => x > 5
            ? Result.Success(x * 2)
            : Result.Failure<int>("Value too small"));

        // Assert
        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be(20);
    }

    [Fact]
    public void Bind_OnFailure_ShouldPropagateError()
    {
        // Arrange
        var result = Result.Failure<int>("Initial error");

        // Act
        var bound = result.Bind(x => Result.Success(x * 2));

        // Assert
        bound.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GetValueOrDefault_OnSuccess_ShouldReturnValue()
    {
        // Arrange
        var result = Result.Success(42);

        // Act
        var value = result.GetValueOrDefault(-1);

        // Assert
        value.Should().Be(42);
    }

    [Fact]
    public void GetValueOrDefault_OnFailure_ShouldReturnDefault()
    {
        // Arrange
        var result = Result.Failure<int>("Error");

        // Act
        var value = result.GetValueOrDefault(-1);

        // Assert
        value.Should().Be(-1);
    }

    [Fact]
    public void GetValueOrThrow_OnSuccess_ShouldReturnValue()
    {
        // Arrange
        var result = Result.Success(42);

        // Act
        var value = result.GetValueOrThrow();

        // Assert
        value.Should().Be(42);
    }

    [Fact]
    public void GetValueOrThrow_OnFailure_ShouldThrow()
    {
        // Arrange
        var exception = new InvalidOperationException("Test error");
        var result = Result.Failure<int>(exception);

        // Act & Assert
        var action = () => result.GetValueOrThrow();
        action.Should().Throw<InvalidOperationException>().WithMessage("Test error");
    }

    [Fact]
    public void ImplicitConversion_FromValue_ShouldCreateSuccess()
    {
        // Act
        Result<int> result = 42;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void ImplicitConversion_FromException_ShouldCreateFailure()
    {
        // Arrange
        var exception = new InvalidOperationException("Error");

        // Act
        Result<int> result = exception;

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(exception);
    }

    [Fact]
    public void Try_OnSuccess_ShouldReturnSuccessResult()
    {
        // Act
        var result = Result.Try(() => 42);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Try_OnException_ShouldReturnFailureResult()
    {
        // Act
        var result = Result.Try<int>(() => throw new InvalidOperationException("Error"));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task TryAsync_OnSuccess_ShouldReturnSuccessResult()
    {
        // Act
        var result = await Result.TryAsync(() => ValueTask.FromResult(42));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task TryAsync_OnException_ShouldReturnFailureResult()
    {
        // Act
        Func<ValueTask<int>> throwFunc = () => throw new InvalidOperationException("Error");
        var result = await Result.TryAsync(throwFunc);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<InvalidOperationException>();
    }

    // Tests for Result<TValue, TError>
    [Fact]
    public void TypedError_Success_ShouldCreateSuccessfulResult()
    {
        // Act
        var result = Result.Success<string, ValidationError>("value");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("value");
    }

    [Fact]
    public void TypedError_Failure_ShouldCreateFailedResult()
    {
        // Arrange
        var error = new ValidationError("Field is required", "FieldName");

        // Act
        var result = Result.Failure<string, ValidationError>(error);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void TypedError_MapError_ShouldTransformError()
    {
        // Arrange
        var result = Result.Failure<string, ValidationError>(new ValidationError("Error", "Field"));

        // Act
        var mapped = result.MapError(e => $"{e.FieldName}: {e.Message}");

        // Assert
        mapped.IsFailure.Should().BeTrue();
        mapped.Error.Should().Be("Field: Error");
    }
}

// Test helper types
public record ValidationError(string Message, string FieldName);
