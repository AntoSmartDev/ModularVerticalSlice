using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.UnitTests.SharedKernel;

/// <summary>
/// Verifies the baseline behavior of <see cref="Error" /> factory methods.
/// </summary>
public class ErrorTests
{
    /// <summary>
    /// Verifies that <see cref="Error.None" /> represents the absence of an error.
    /// </summary>
    [Fact]
    public void ErrorNone_Should_Represent_The_Absence_Of_An_Error()
    {
        Assert.Equal(string.Empty, Error.None.Code);
        Assert.Equal(string.Empty, Error.None.Description);
        Assert.Equal(ErrorType.None, Error.None.Type);
        Assert.Null(Error.None.ValidationErrors);
    }

    /// <summary>
    /// Verifies that <see cref="Error.Failure(string, string)" /> creates a generic failure.
    /// </summary>
    [Fact]
    public void ErrorFailure_Should_Create_Generic_Failure()
    {
        var error = Error.Failure(
            "Bookings.UnknownFailure",
            "The booking operation failed.");

        Assert.Equal("Bookings.UnknownFailure", error.Code);
        Assert.Equal("The booking operation failed.", error.Description);
        Assert.Equal(ErrorType.Failure, error.Type);
        Assert.Null(error.ValidationErrors);
    }

    /// <summary>
    /// Verifies that validation errors preserve grouped validation details.
    /// </summary>
    [Fact]
    public void ErrorValidation_Should_Create_Error_With_Details()
    {
        IReadOnlyDictionary<string, string[]> validationErrors =
            new Dictionary<string, string[]>
            {
                ["Quantity"] = ["Quantity must be greater than zero."],
                ["EventId"] = ["EventId is required."]
            };

        var error = Error.Validation(
            "Catalog.InvalidTicketQuantity",
            "The request is invalid.",
            validationErrors);

        Assert.Equal("Catalog.InvalidTicketQuantity", error.Code);
        Assert.Equal("The request is invalid.", error.Description);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Same(validationErrors, error.ValidationErrors);
        Assert.NotNull(error.ValidationErrors);
        Assert.Equal(
            "Quantity must be greater than zero.",
            error.ValidationErrors!["Quantity"][0]);
    }

    /// <summary>
    /// Verifies that specialized factory methods create the expected <see cref="ErrorType" />.
    /// </summary>
    /// <param name="expectedType">The expected error type created by the factory method.</param>
    [Theory]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Forbidden)]
    public void ErrorFactory_Methods_Should_Create_Expected_Type(ErrorType expectedType)
    {
        var error = expectedType switch
        {
            ErrorType.NotFound => Error.NotFound("Catalog.EventNotFound", "Event was not found."),
            ErrorType.Conflict => Error.Conflict("Catalog.NotEnoughTickets", "Not enough tickets are available."),
            ErrorType.Unauthorized => Error.Unauthorized("Auth.MissingUser", "The caller is not authenticated."),
            ErrorType.Forbidden => Error.Forbidden("Auth.ForbiddenAction", "The caller is not allowed."),
            _ => throw new InvalidOperationException("Unexpected error type for this test.")
        };

        Assert.Equal(expectedType, error.Type);
        Assert.NotEmpty(error.Code);
        Assert.NotEmpty(error.Description);
    }
}
