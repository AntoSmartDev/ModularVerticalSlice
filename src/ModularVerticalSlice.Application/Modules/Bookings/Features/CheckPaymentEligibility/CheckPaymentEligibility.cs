using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.SharedKernel;
using Wolverine.Attributes;

namespace ModularVerticalSlice.Application.Modules.Bookings.Features.CheckPaymentEligibility;

/// <summary>
/// Answers whether Bookings still considers a booking eligible for payment.
/// </summary>
public sealed class CheckPaymentEligibilityHandler(IBookingReadDbContextSlice readDb)
{
    /// <summary>
    /// Returns success only while the target booking exists and remains pending.
    /// </summary>
    [WolverineHandler]
    public async Task<Result> HandleCheckBookingPaymentEligibility(
        CheckBookingPaymentEligibilityQuery query,
        CancellationToken cancellationToken)
    {
        var bookingStatus = await readDb.Bookings
            .Where(x => x.Id == query.BookingId)
            .Select(x => (BookingStatus?)x.Status)
            .SingleOrDefaultAsync(cancellationToken);

        if (bookingStatus is null)
        {
            return Result.Failure(
                Error.NotFound(
                    "Bookings.BookingNotFound",
                    "The target booking was not found."));
        }

        if (bookingStatus != BookingStatus.Pending)
        {
            return Result.Failure(
                Error.Conflict(
                    "Bookings.BookingNotPayable",
                    "The target booking no longer accepts payment."));
        }

        return Result.Success();
    }
}
