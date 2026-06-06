namespace ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;

/// <summary>
/// Configures the BookingLifecycle orchestration baseline.
/// </summary>
public sealed class BookingLifecycleOptions
{
    /// <summary>
    /// Gets or sets how long a newly created booking may wait for payment.
    /// </summary>
    public TimeSpan PaymentWindow { get; set; } = TimeSpan.FromMinutes(15);
}
