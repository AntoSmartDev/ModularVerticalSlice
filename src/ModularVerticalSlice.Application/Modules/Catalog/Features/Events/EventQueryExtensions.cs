using System.Linq.Expressions;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;

namespace ModularVerticalSlice.Application.Modules.Catalog.Features.Events;

internal static class EventQueryExtensions
{
    public static IQueryable<Event> UpcomingOnly(
        this IQueryable<Event> query,
        DateTimeOffset now) =>
        query.Where(x => x.Date >= now);

    public static IQueryable<EventReadModel> ToReadModels(this IQueryable<Event> query) =>
        query.Select(ToReadModelExpression());

    private static Expression<Func<Event, EventReadModel>> ToReadModelExpression() =>
        entity => new EventReadModel(
            entity.Id,
            entity.Title,
            entity.Date,
            entity.TicketPrice);
}
