using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.UnitTests.SharedKernel;

/// <summary>
/// Verifies the baseline behavior of <see cref="Result" /> and <see cref="Result{TValue}" />.
/// </summary>
public class ResultTests
{
    /// <summary>
    /// Verifies that a successful result has no error.
    /// </summary>
    [Fact]
    public void ResultSuccess_Should_Have_No_Error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    /// <summary>
    /// Verifies that a failed result exposes the expected error.
    /// </summary>
    [Fact]
    public void ResultFailure_Should_Have_Error()
    {
        var error = Error.Conflict(
            "Catalog.NotEnoughTickets",
            "Not enough tickets are available.");

        var result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    /// <summary>
    /// Verifies that inconsistent result state is rejected.
    /// </summary>
    [Fact]
    public void Result_Should_Reject_Invalid_State()
    {
        Assert.Throws<ArgumentException>(
            () => TestResult.Create(true, Error.Failure("General.Invalid", "Failure")));

        Assert.Throws<ArgumentException>(
            () => TestResult.Create(false, Error.None));
    }

    /// <summary>
    /// Verifies that a successful generic result exposes its value.
    /// </summary>
    [Fact]
    public void ResultOfT_Should_Return_Value_When_Success()
    {
        Result<string> result = Result.Success("booking-123");

        Assert.True(result.IsSuccess);
        Assert.Equal("booking-123", result.Value);
    }

    /// <summary>
    /// Verifies that accessing the value of a failed generic result throws.
    /// </summary>
    [Fact]
    public void ResultOfT_Should_Throw_When_Accessing_Value_On_Failure()
    {
        Result<string> result = Error.NotFound(
            "Bookings.BookingNotFound",
            "The requested booking was not found.");

        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    /// <summary>
    /// Verifies that implicit conversions reduce boilerplate for success and failure.
    /// </summary>
    [Fact]
    public void ResultOfT_Should_Support_Implicit_Conversions()
    {
        Result<int> success = 42;
        Result<int> failure = Error.Validation(
            "Catalog.InvalidQuantity",
            "Quantity is invalid.");

        Assert.True(success.IsSuccess);
        Assert.Equal(42, success.Value);
        Assert.True(failure.IsFailure);
        Assert.Equal(ErrorType.Validation, failure.Error.Type);
    }

    private sealed class TestResult : Result
    {
        private TestResult(bool isSuccess, Error error)
            : base(isSuccess, error)
        {
        }

        public static TestResult Create(bool isSuccess, Error error) =>
            new(isSuccess, error);
    }
}
