using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.Application.Modules.Catalog.Contracts;

/// <summary>
/// Exposes Catalog-owned ticket reservation inside the shared local transaction.
/// </summary>
/// <remarks>
/// This contract is intentionally in-process. It does not save changes or represent
/// a transport-ready boundary; the caller owns the shared transaction.
/// </remarks>
public interface ITicketReservation
{
    /// <summary>
    /// Reserves tickets on the tracked Catalog event.
    /// </summary>
    Task<Result> ReserveAsync(
        Guid eventId,
        int quantity,
        Guid bookingId,
        CancellationToken cancellationToken);
}
