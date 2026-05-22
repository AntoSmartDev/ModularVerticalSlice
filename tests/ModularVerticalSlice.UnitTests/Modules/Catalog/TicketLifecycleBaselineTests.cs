using ModularVerticalSlice.Modules.Catalog.Domain;
using ModularVerticalSlice.Modules.Catalog.Messages;
using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.UnitTests.Modules.Catalog;

/// <summary>
/// Verifies the baseline ticket lifecycle abstractions exposed by the Catalog module.
/// </summary>
public class TicketLifecycleBaselineTests
{
    /// <summary>
    /// Verifies that reservations succeed when enough tickets are available.
    /// </summary>
    [Fact]
    public void CanReserve_Should_Succeed_When_Enough_Tickets_Are_Available()
    {
        var result = TicketReservationPolicy.CanReserve(10, 3);

        Assert.True(result.IsSuccess);
        Assert.Equal(Error.None, result.Error);
    }

    /// <summary>
    /// Verifies that non-positive quantities are rejected as validation failures.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CanReserve_Should_Fail_When_Quantity_Is_Not_Positive(int requestedQuantity)
    {
        var result = TicketReservationPolicy.CanReserve(10, requestedQuantity);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Catalog.InvalidQuantity", result.Error.Code);
    }

    /// <summary>
    /// Verifies that negative availability is rejected as invalid state input.
    /// </summary>
    [Fact]
    public void CanReserve_Should_Fail_When_Availability_Is_Negative()
    {
        var result = TicketReservationPolicy.CanReserve(-1, 1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Catalog.InvalidAvailability", result.Error.Code);
    }

    /// <summary>
    /// Verifies that reservations fail with a conflict when requested quantity exceeds availability.
    /// </summary>
    [Fact]
    public void CanReserve_Should_Fail_When_Not_Enough_Tickets_Are_Available()
    {
        var result = TicketReservationPolicy.CanReserve(2, 3);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Catalog.NotEnoughTickets", result.Error.Code);
    }

    /// <summary>
    /// Verifies that reserve and release commands expose the same stable booking coordination shape.
    /// </summary>
    [Fact]
    public void Ticket_Lifecycle_Commands_Should_Expose_Stable_Coordination_Shape()
    {
        var eventId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var reserve = new ReserveTicketsCommand(eventId, 4, bookingId);
        var release = new ReleaseTicketsCommand(eventId, 4, bookingId);

        Assert.Equal(eventId, reserve.EventId);
        Assert.Equal(4, reserve.Quantity);
        Assert.Equal(bookingId, reserve.BookingId);

        Assert.Equal(eventId, release.EventId);
        Assert.Equal(4, release.Quantity);
        Assert.Equal(bookingId, release.BookingId);
    }
}
