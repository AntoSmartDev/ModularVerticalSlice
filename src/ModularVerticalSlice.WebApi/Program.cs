using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Catalog;
using ModularVerticalSlice.Application.Shared.Modules;
using ModularVerticalSlice.Application.Modules.Notifications;
using ModularVerticalSlice.Application.Modules.Payments;
using ModularVerticalSlice.Application.Shared.Security;
using ModularVerticalSlice.Persistence;
using ModularVerticalSlice.WebApi.Infrastructure.Authentication;
using ModularVerticalSlice.WebApi.Infrastructure.Observability;
using ModularVerticalSlice.WebApi;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

IModule[] modules =
[
    new CatalogModule(),
    new BookingsModule(),
    new PaymentsModule(),
    new NotificationsModule()
];

builder.Host.UseWolverine(options =>
{
    options.ConfigureApplicationMessaging(builder.Configuration);
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
builder.Services.AddCorrelation();
builder.Services.AddWebApiAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddWebApiAuthorization();
builder.Services.AddProblemDetails();
builder.Services.AddApplicationModules(builder.Configuration, modules);
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

app.MapApplicationModules(modules);
app.MapGet("/", () => Results.Text("ModularVerticalSlice.WebApi"));

app.Run();

/// <summary>
/// Exposes the implicit Program entry point so integration tests can host the
/// application through <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
