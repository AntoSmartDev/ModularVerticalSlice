using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Catalog;
using ModularVerticalSlice.Application.Shared.Composition;
using ModularVerticalSlice.Application.Delivery.BookingConfirmation;
using ModularVerticalSlice.Application.Modules.Payments;
using ModularVerticalSlice.Application.Shared.Security;
using ModularVerticalSlice.Persistence;
using ModularVerticalSlice.WebApi.Infrastructure.Authentication;
using ModularVerticalSlice.WebApi.Infrastructure.Authorization;
using ModularVerticalSlice.WebApi.Infrastructure.Correlation;
using ModularVerticalSlice.WebApi.Infrastructure.HealthChecks;
using ModularVerticalSlice.WebApi.Infrastructure.Observability;
using ModularVerticalSlice.WebApi;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

IApplicationBoundary[] boundaries =
[
    new CatalogModule(),
    new BookingsModule(),
    new PaymentsModule(),
    new BookingConfirmationDeliveryModule()
];

builder.Host.UseWolverine(options =>
{
    options.ConfigureApplicationMessaging(builder.Configuration);
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddCorrelation();
builder.Services.AddApplicationHealthChecks();
builder.Services.AddWebApiObservability(builder.Configuration, builder.Environment);
builder.Services.AddWebApiAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddWebApiAuthorization();
builder.Services.AddProblemDetails();
builder.Services.AddApplicationBoundaries(builder.Configuration, boundaries);
builder.Services.AddPersistence();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCorrelationId();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapApplicationHealthEndpoints();

app.MapApplicationBoundaries(boundaries);
app.MapGet("/", () => Results.Text("ModularVerticalSlice.WebApi"));

app.Run();

/// <summary>
/// Exposes the implicit Program entry point so integration tests can host the
/// application through <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
